using ClientCommissions.Wallet.Domain.Entities;
using ClientCommissions.Wallet.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClientCommissions.Wallet.Application;

public sealed class WalletService : IWalletService
{
    private const string UniqueViolation = "23505";

    private readonly WalletDbContext _db;
    private readonly ILogger<WalletService> _logger;

    public WalletService(WalletDbContext db, ILogger<WalletService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PayoutResult> ApplyPayoutAsync(
        Guid commissionId, Guid userId, decimal amount, CancellationToken ct)
    {
        if (await _db.Payouts.AnyAsync(p => p.CommissionId == commissionId, ct))
        {
            WalletMetrics.PayoutsDuplicate.Inc();
            return PayoutResult.Duplicate;
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
        if (account is null)
        {
            account = new WalletAccount(userId);
            _db.Accounts.Add(account);
        }

        account.Credit(amount);
        _db.Payouts.Add(new PayoutRecord(commissionId, userId, amount));

        try
        {
            await _db.SaveChangesAsync(ct);
            WalletMetrics.PayoutsApplied.Inc();
            _logger.LogInformation(
                "Payout for commission {CommissionId} applied: +{Amount} to {UserId}",
                commissionId, amount, userId);
            return PayoutResult.Applied;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            WalletMetrics.PayoutsDuplicate.Inc();
            _logger.LogInformation("Payout for commission {CommissionId} is a duplicate", commissionId);
            return PayoutResult.Duplicate;
        }
    }

    public async Task<decimal?> GetBalanceAsync(Guid userId, CancellationToken ct)
    {
        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, ct);

        return account?.Balance;
    }

    public async Task<IReadOnlyList<PayoutRecord>> GetPayoutsAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Payouts
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);
    }
}
