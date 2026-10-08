using ClientCommissions.Commissions.Domain.Calculations;

namespace ClientCommissions.Commissions.Application;

public interface IHierarchyProvider
{
    Task<IReadOnlyList<AncestorLink>> GetAncestorsAsync(Guid userId, int maxDepth, CancellationToken ct);
}
