namespace ClientCommissions.Wallet.Contracts;

public sealed record BalanceResponse(Guid UserId, decimal Balance);
