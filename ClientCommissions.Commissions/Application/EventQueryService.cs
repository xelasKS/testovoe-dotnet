using ClientCommissions.Commissions.Contracts;
using ClientCommissions.Commissions.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClientCommissions.Commissions.Application;

public sealed class EventQueryService : IEventQueryService
{
    private readonly ApplicationContext _db;

    public EventQueryService(ApplicationContext db) => _db = db;

    public async Task<IReadOnlyList<EventSummaryResponse>> GetUserEventsAsync(Guid userId, CancellationToken ct)
    {
        var events = await _db.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        return events
            .Select(e => new EventSummaryResponse(e.Id, e.UserId, e.Profit, e.Status.ToString(), e.CreatedAt))
            .ToList();
    }

    public async Task<EventDetailResponse?> GetEventDetailAsync(Guid eventId, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return null;

        var commissions = await _db.Commissions
            .AsNoTracking()
            .Where(c => c.SourceEventId == eventId)
            .OrderBy(c => c.Level)
            .ToListAsync(ct);

        var lines = commissions
            .Select(c => new CommissionLine(
                c.UserId, c.Level, c.Amount, c.Schema.ToString(), c.Status.ToString()))
            .ToList();

        return new EventDetailResponse(
            ev.Id, ev.UserId, ev.Profit, ev.Status.ToString(), ev.CreatedAt, lines);
    }
}
