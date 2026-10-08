using ClientCommissions.Shared.Contracts;
using ClientCommissions.Users.Contracts;
using ClientCommissions.Users.Domain.Entities;

namespace ClientCommissions.Users.Application;

public interface IUserService
{
    Task<User> CreateAsync(Guid? id, Guid? referrerId, CancellationToken ct);

    Task<IReadOnlyList<AncestorContract>?> GetAncestorsAsync(Guid userId, int maxDepth, CancellationToken ct);

    Task<IReadOnlyList<DescendantResponse>?> GetDescendantsAsync(Guid userId, int maxDepth, CancellationToken ct);
}
