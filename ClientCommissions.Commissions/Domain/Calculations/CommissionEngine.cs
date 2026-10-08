using ClientCommissions.Commissions.Domain.Entities;

namespace ClientCommissions.Commissions.Domain.Calculations;

public sealed class CommissionEngine : ICommissionEngine
{
    public List<CommissionResult> Calculate(decimal profit, ICommissionScheme scheme, IReadOnlyList<AncestorLink> chain)
    {
        var result = new List<CommissionResult>();
        if(profit <= 0) return result;
        foreach (var link in chain)
        {
            var coefficient = scheme.GetCoefficient(link.Level);
            var amount = Math.Round(profit * coefficient / 100m, 2, MidpointRounding.ToEven);
            result.Add(new CommissionResult(link.UserId, link.Level, amount, scheme.Type));
        }
        return result;
    }

}
