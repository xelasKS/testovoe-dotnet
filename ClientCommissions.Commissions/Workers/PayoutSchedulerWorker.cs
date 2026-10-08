using System.Text.Json;
using ClientCommissions.Commissions.Domain.Entities;
using ClientCommissions.Commissions.Infrastructure;
using ClientCommissions.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ClientCommissions.Commissions.Workers;

public sealed class PayoutSchedulerWorker : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PayoutSchedulerWorker> _logger;

    public PayoutSchedulerWorker(IServiceScopeFactory scopeFactory, ILogger<PayoutSchedulerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScheduleAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Payout scheduling failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ScheduleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var unpaid = await db.Commissions
            .FromSqlRaw(
                @"SELECT * FROM ""Commissions"" WHERE ""Status"" = 'Unpaid'
                  ORDER BY ""CreatedAt"" LIMIT {0} FOR UPDATE SKIP LOCKED",
                BatchSize)
            .ToListAsync(ct);

        if (unpaid.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        foreach (var commission in unpaid)
        {
            commission.MarkPaying();
            var payload = JsonSerializer.Serialize(
                new PayoutContract(commission.Id, commission.UserId, commission.Amount));
            db.Outbox.Add(new Outbox("CommissionPayout", payload));
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _logger.LogInformation("Scheduled {Count} commissions for payout", unpaid.Count);
    }
}
