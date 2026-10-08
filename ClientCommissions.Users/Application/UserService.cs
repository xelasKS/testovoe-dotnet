using ClientCommissions.Shared.Contracts;
using ClientCommissions.Users.Domain.Entities;
using ClientCommissions.Users.Contracts;
using ClientCommissions.Users.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClientCommissions.Users.Application;

public sealed class UserService : IUserService
{
    private readonly UsersDbContext _db;
    private readonly ILogger<UserService> _logger;

    public UserService(UsersDbContext db, ILogger<UserService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<User> CreateAsync(Guid? id, Guid? referrerId, CancellationToken ct)
    {
        if (referrerId is { } refId && !await _db.Users.AnyAsync(u => u.Id == refId, ct))
            throw new ReferrerNotFoundException(refId);

        var user = new User(id ?? Guid.NewGuid(), referrerId);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        UsersMetrics.UsersCreated.Inc();

        _logger.LogInformation("User {UserId} created (referrer {ReferrerId})", user.Id, referrerId);
        return user;
    }

    public async Task<IReadOnlyList<AncestorContract>?> GetAncestorsAsync(
        Guid userId, int maxDepth, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct))
            return null;

        const string sql = @"
            WITH RECURSIVE chain AS (
                SELECT ""Id"", ""ReferrerId"", 0 AS depth
                FROM ""Users""
                WHERE ""Id"" = {0}
                UNION ALL
                SELECT u.""Id"", u.""ReferrerId"", c.depth + 1
                FROM ""Users"" u
                JOIN chain c ON u.""Id"" = c.""ReferrerId""
                WHERE c.depth < {1}
            )
            SELECT u.*
            FROM ""Users"" u
            JOIN chain c ON u.""Id"" = c.""Id""
            WHERE c.depth > 0
            ORDER BY c.depth";

        var ancestors = await _db.Users
            .FromSqlRaw(sql, userId, maxDepth)
            .AsNoTracking()
            .ToListAsync(ct);
        UsersMetrics.AncestorQueries.Inc();
        return ancestors
            .Select((u, i) => new AncestorContract(i + 1, u.Id))
            .ToList();
    }

    public async Task<IReadOnlyList<DescendantResponse>?> GetDescendantsAsync(
        Guid userId, int maxDepth, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(u => u.Id == userId, ct))
            return null;

        const string sql = @"
            WITH RECURSIVE tree AS (
                SELECT ""Id"", 0 AS depth
                FROM ""Users""
                WHERE ""Id"" = {0}
                UNION ALL
                SELECT u.""Id"", t.depth + 1
                FROM ""Users"" u
                JOIN tree t ON u.""ReferrerId"" = t.""Id""
                WHERE t.depth < {1}
            )
            SELECT u.""Id"" AS ""UserId"", t.depth AS ""Depth""
            FROM ""Users"" u
            JOIN tree t ON u.""Id"" = t.""Id""
            WHERE t.depth > 0
            ORDER BY t.depth";

        var rows = await _db.Set<DescendantRow>()
            .FromSqlRaw(sql, userId, maxDepth)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Select(r => new DescendantResponse(r.Depth, r.UserId)).ToList();
    }
}

