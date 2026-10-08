using ClientCommissions.Wallet.Application;
using ClientCommissions.Wallet.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClientCommissions.Wallet.Tests;

public sealed class WalletServiceTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private readonly PostgresFixture _fixture;

    public WalletServiceTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await using var db = _fixture.CreateContext();
        await db.Payouts.ExecuteDeleteAsync(Ct);
        await db.Accounts.ExecuteDeleteAsync(Ct);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static WalletService Service(WalletDbContext db) => new(db, NullLogger<WalletService>.Instance);

    [Fact]
    public async Task First_payout_credits_balance_and_records_history()
    {
        await using var db = _fixture.CreateContext();
        var svc = Service(db);
        var user = Guid.NewGuid();

        var result = await svc.ApplyPayoutAsync(Guid.NewGuid(), user, 10.50m, Ct);

        Assert.Equal(PayoutResult.Applied, result);
        Assert.Equal(10.50m, await svc.GetBalanceAsync(user, Ct));
        Assert.Single(await svc.GetPayoutsAsync(user, Ct));
    }

    [Fact]
    public async Task Duplicate_commission_does_not_double_credit()
    {
        await using var db = _fixture.CreateContext();
        var svc = Service(db);
        var user = Guid.NewGuid();
        var commissionId = Guid.NewGuid();

        var first = await svc.ApplyPayoutAsync(commissionId, user, 30m, Ct);
        var second = await svc.ApplyPayoutAsync(commissionId, user, 30m, Ct);

        Assert.Equal(PayoutResult.Applied, first);
        Assert.Equal(PayoutResult.Duplicate, second);
        Assert.Equal(30m, await svc.GetBalanceAsync(user, Ct));
        Assert.Single(await svc.GetPayoutsAsync(user, Ct));
    }

    [Fact]
    public async Task Multiple_distinct_payouts_accumulate()
    {
        await using var db = _fixture.CreateContext();
        var svc = Service(db);
        var user = Guid.NewGuid();

        await svc.ApplyPayoutAsync(Guid.NewGuid(), user, 10m, Ct);
        await svc.ApplyPayoutAsync(Guid.NewGuid(), user, 20m, Ct);
        await svc.ApplyPayoutAsync(Guid.NewGuid(), user, 5.25m, Ct);

        Assert.Equal(35.25m, await svc.GetBalanceAsync(user, Ct));
        Assert.Equal(3, (await svc.GetPayoutsAsync(user, Ct)).Count);
    }

    [Fact]
    public async Task Payout_to_new_user_creates_account()
    {
        await using var db = _fixture.CreateContext();
        var svc = Service(db);
        var user = Guid.NewGuid();

        Assert.Null(await svc.GetBalanceAsync(user, Ct));

        await svc.ApplyPayoutAsync(Guid.NewGuid(), user, 42m, Ct);

        Assert.Equal(42m, await svc.GetBalanceAsync(user, Ct));
    }

    [Fact]
    public async Task Unknown_user_has_null_balance()
    {
        await using var db = _fixture.CreateContext();
        var svc = Service(db);

        Assert.Null(await svc.GetBalanceAsync(Guid.NewGuid(), Ct));
    }
}
