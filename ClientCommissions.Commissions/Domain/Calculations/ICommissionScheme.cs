using ClientCommissions.Commissions.Domain.Entities;

namespace ClientCommissions.Commissions.Domain.Calculations;

public interface ICommissionScheme
{
    SchemaType Type { get; }
    decimal GetCoefficient(int level);
}
