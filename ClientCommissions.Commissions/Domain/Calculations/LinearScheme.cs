using ClientCommissions.Commissions.Domain.Entities;

namespace ClientCommissions.Commissions.Domain.Calculations;

public sealed class LinearScheme : ICommissionScheme
{
    public SchemaType Type => SchemaType.Linear;
    public decimal GetCoefficient(int level) => level;
}
