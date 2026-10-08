using System.Text.Json;
using ClientCommissions.Commissions.Domain.Entities;
using ClientCommissions.Commissions.Infrastructure;
using ClientCommissions.Shared.Contracts;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClientCommissions.Commissions.Workers;

public sealed class OutboxDispatcherWorker : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _kafka;
    private readonly ILogger<OutboxDispatcherWorker> _logger;

    public OutboxDispatcherWorker(
        IServiceScopeFactory scopeFactory,
        IProducer<string, string> producer,
        IOptions<KafkaOptions> kafka,
        ILogger<OutboxDispatcherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _producer = producer;
        _kafka = kafka.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Outbox dispatch failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
    private async Task DispatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        List<Outbox> pending;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var staleBefore = DateTime.UtcNow - StaleClaimTimeout;
            pending = await db.Outbox
                .FromSqlRaw(
                    @"SELECT * FROM ""Outbox""
                      WHERE ""Status"" = 'Unsent'
                         OR (""Status"" = 'Sending' AND ""ClaimedAt"" < {1})
                      ORDER BY ""CreatedAt"" LIMIT {0} FOR UPDATE SKIP LOCKED",
                    BatchSize, staleBefore)
                .ToListAsync(ct);

            if (pending.Count == 0)
            {
                await tx.RollbackAsync(ct);
                return;
            }

            foreach (var message in pending)
                message.MarkSending();

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        var commissionIds = new List<Guid>();
        var publishedAny = false;
        foreach (var message in pending)
        {
            var payout = JsonSerializer.Deserialize<PayoutContract>(message.Payload)
                         ?? throw new InvalidOperationException($"Invalid outbox payload {message.Id}");

            using (_logger.BeginScope(new Dictionary<string, object> { ["CommissionId"] = payout.CommissionId }))
            {
                try
                {
                    await _producer.ProduceAsync(
                        _kafka.PayoutTopic,
                        new Message<string, string>
                        {
                            Key = payout.UserId.ToString(),
                            Value = message.Payload,
                        },
                        ct);
                    message.MarkSent();
                    commissionIds.Add(payout.CommissionId);
                    publishedAny = true;

                    CommissionsMetrics.PayoutsPublished.Inc();
                    _logger.LogInformation("Payout published: user {UserId}, amount {Amount}",
                        payout.UserId, payout.Amount);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to publish payout {Id}, will retry after timeout", message.Id);
                }
            }
        }

        if (!publishedAny)
            return;
        
        var paid = await db.Commissions
            .Where(c => commissionIds.Contains(c.Id))
            .ToListAsync(ct);
        foreach (var commission in paid)
            commission.MarkPaid();

        await db.SaveChangesAsync(ct);
    }
}
