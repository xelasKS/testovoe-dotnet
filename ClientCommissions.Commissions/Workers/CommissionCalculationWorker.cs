using ClientCommissions.Commissions.Application;
using ClientCommissions.Commissions.Domain.Calculations;
using ClientCommissions.Commissions.Domain.Entities;
using ClientCommissions.Commissions.Infrastructure;
using ClientCommissions.Shared;
using Microsoft.EntityFrameworkCore;

namespace ClientCommissions.Commissions.Workers;

public sealed class CommissionCalculationWorker : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaleClaimTimeout = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommissionCalculationWorker> _logger;

    public CommissionCalculationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<CommissionCalculationWorker> logger)
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
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Commission calculation batch failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        var hierarchy = scope.ServiceProvider.GetRequiredService<IHierarchyProvider>();
        var resolver = scope.ServiceProvider.GetRequiredService<ICommissionSchemeResolver>();
        var engine = scope.ServiceProvider.GetRequiredService<ICommissionEngine>();
        
        List<ProfitEvent> events;
        SchemaType currentSchema;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var staleBefore = DateTime.UtcNow - StaleClaimTimeout;
            events = await db.Events
                .FromSqlRaw(
                    @"SELECT * FROM ""Events""
                      WHERE ""Status"" = 'Received'
                         OR (""Status"" = 'Processing' AND ""ClaimedAt"" < {1})
                      ORDER BY ""CreatedAt"" LIMIT {0} FOR UPDATE SKIP LOCKED",
                    BatchSize, staleBefore)
                .ToListAsync(ct);

            if (events.Count == 0)
            {
                await tx.RollbackAsync(ct);
                return;
            }

            foreach (var ev in events)
                ev.MarkProcessing();

            currentSchema = (await db.SchemaSettings.FirstAsync(ct)).Schema;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        var scheme = resolver.Resolve(currentSchema);
        
        var processed = 0;
        foreach (var ev in events)
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["EventId"] = ev.Id }))
            {
                try
                {
                    var chain = await hierarchy.GetAncestorsAsync(ev.UserId, ChainDepth.Max, ct);
                    var results = engine.Calculate(ev.Profit, scheme, chain);

                    db.Commissions.AddRange(results.Select(r =>
                        new Commission(r.Amount, r.UserId, r.Level, r.Schema, ev.Id)));
                    ev.MarkProcessed();
                    processed++;

                    CommissionsMetrics.CommissionsCalculated.Inc(results.Count);
                    _logger.LogInformation(
                        "ProfitEvent processed, {Count} commissions created (schema {Schema})",
                        results.Count, currentSchema);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to calculate commissions for event, will retry after timeout");
                }
            }
        }

        if (processed == 0)
            return;
        await db.SaveChangesAsync(ct);
    }
}
