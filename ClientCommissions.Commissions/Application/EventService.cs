using ClientCommissions.Commissions.Contracts;
using ClientCommissions.Commissions.Domain.Entities;
using ClientCommissions.Commissions.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClientCommissions.Commissions.Application;

public sealed class EventService : IEventService
{
    private const string UniqueViolation = "23505";

    private readonly ApplicationContext _db;
    private readonly ILogger<EventService> _logger;

    public EventService(ApplicationContext db, ILogger<EventService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IngestResult> IngestAsync(CreateEventRequest request, CancellationToken ct)
    {
        var @event = new ProfitEvent(request.Profit, request.ExternalId, request.UserId);
        _db.Events.Add(@event);

        try
        {
            await _db.SaveChangesAsync(ct);
            CommissionsMetrics.EventsReceived.Inc();
            _logger.LogInformation("ProfitEvent {ExternalId} accepted (eventId {EventId})",
                request.ExternalId, @event.Id);
            return IngestResult.Created;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            CommissionsMetrics.EventsDuplicate.Inc();
            _logger.LogInformation("ProfitEvent {ExternalId} is a duplicate, ignored", request.ExternalId);
            return IngestResult.Duplicate;
        }
    }
}
