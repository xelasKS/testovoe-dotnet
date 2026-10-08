using ClientCommissions.Commissions.Domain.Entities;

namespace ClientCommissions.Commissions.Domain.Calculations;

public interface ICommissionSchemeResolver
{
    ICommissionScheme Resolve(SchemaType type);
}
