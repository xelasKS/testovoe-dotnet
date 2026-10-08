using ClientCommissions.Commissions.Contracts;

namespace ClientCommissions.Commissions.Application;

public interface IEventQueryService
{
    Task<IReadOnlyList<EventSummaryResponse>> GetUserEventsAsync(Guid userId, CancellationToken ct);
    Task<EventDetailResponse?> GetEventDetailAsync(Guid eventId, CancellationToken ct);
}
