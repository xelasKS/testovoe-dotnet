namespace ClientCommissions.Wallet.Contracts;

public sealed record PayoutResponse(Guid CommissionId, decimal Amount, DateTime CreatedAt);
