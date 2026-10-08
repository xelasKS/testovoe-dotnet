using ClientCommissions.Wallet.Domain.Entities;

namespace ClientCommissions.Wallet.Application;

public interface IWalletService
{
    Task<PayoutResult> ApplyPayoutAsync(Guid commissionId, Guid userId, decimal amount, CancellationToken ct);

    Task<decimal?> GetBalanceAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<PayoutRecord>> GetPayoutsAsync(Guid userId, CancellationToken ct);
}
