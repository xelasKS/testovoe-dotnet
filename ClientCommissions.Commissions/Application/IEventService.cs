using ClientCommissions.Commissions.Contracts;

namespace ClientCommissions.Commissions.Application;

public interface IEventService
{
    Task<IngestResult> IngestAsync(CreateEventRequest request, CancellationToken ct);
}
