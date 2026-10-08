using ClientCommissions.Commissions.Domain.Entities;

namespace ClientCommissions.Commissions.Application;

public interface ISchemaService
{
    Task<SchemaType> GetCurrentAsync(CancellationToken ct);
    Task SwitchAsync(SchemaType schema, CancellationToken ct);
}
