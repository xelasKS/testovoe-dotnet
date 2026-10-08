namespace ClientCommissions.Commissions.Domain.Calculations;
using ClientCommissions.Commissions.Domain.Entities;
public interface ICommissionEngine
{
    List<CommissionResult> Calculate(decimal profit, ICommissionScheme scheme, IReadOnlyList<AncestorLink> chain);
}
