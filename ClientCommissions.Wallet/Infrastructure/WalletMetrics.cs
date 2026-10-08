using Prometheus;

namespace ClientCommissions.Wallet.Infrastructure;

public static class WalletMetrics
{
    public static readonly Counter PayoutsApplied =
        Metrics.CreateCounter("wallet_payouts_applied_total", "Payouts credited to balances");

    public static readonly Counter PayoutsDuplicate =
        Metrics.CreateCounter("wallet_payouts_duplicate_total", "Duplicate payouts ignored by inbox");
}
