using ClientCommissions.Users.Application;
using ClientCommissions.Shared.Contracts;
using ClientCommissions.Users.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClientCommissions.Users.Tests;

public sealed class UserServiceTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly PostgresFixture _fixture;

    public UserServiceTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var db = _fixture.CreateContext();
        await db.Users.ExecuteDeleteAsync(Ct);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static UserService Service(UsersDbContext db) => new(db, NullLogger<UserService>.Instance);

    [Fact]
    public async Task Ancestors_returned_in_order_from_immediate_referrer_upward()
    {
        await using var db = _fixture.CreateContext();
        var svc = Service(db);

        var root = await svc.CreateAsync(null, null, Ct);
        var mid = await svc.CreateAsync(null, root.Id, Ct);
        var leaf = await svc.CreateAsync(null, mid.Id, Ct);

        var chain = await svc.GetAncestorsAsync(leaf.Id, 10, Ct);

        Assert.NotNull(chain);
        Assert.Equal(
            new[]
            {
                new AncestorContract(1, mid.Id),
                new AncestorContract(2, root.Id),
            },
            chain);
    }

    [Fact]
    public async Task Root_user_has_no_ancestors()
    {
        await using var db = _fixture.CreateContext();
        var svc = Service(db);

        var root = await svc.CreateAsync(null, null, Ct);

        var chain = await svc.GetAncestorsAsync(root.Id, 10, Ct);

        Assert.NotNull(chain);
        Assert.Empty(chain!);
    }

    [Fact]
    public async Task Unknown_user_returns_null()
    {
        await using var db = _fixture.CreateContext();
        var svc = Service(db);

        var chain = await svc.GetAncestorsAsync(Guid.NewGuid(), 10, Ct);

        Assert.Null(chain);
    }

    [Fact]
    public async Task Create_with_missing_referrer_throws()
    {
        await using var db = _fixture.CreateContext();
        var svc = Service(db);

        await Assert.ThrowsAsync<ReferrerNotFoundException>(
            () => svc.CreateAsync(null, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task MaxDepth_caps_the_chain()
    {
        await using var db = _fixture.CreateContext();
        var svc = Service(db);

        var ids = new List<Guid>();
        Guid? parent = null;
        for (var i = 0; i < 13; i++)
        {
            var u = await svc.CreateAsync(null, parent, Ct);
            ids.Add(u.Id);
            parent = u.Id;
        }

        var chain = await svc.GetAncestorsAsync(ids[^1], 10, Ct);

        Assert.NotNull(chain);
        Assert.Equal(10, chain!.Count);
        Assert.Equal(ids[^2], chain[0].UserId);
        Assert.Equal(1, chain[0].Level);
        Assert.Equal(10, chain[^1].Level);
    }
}
