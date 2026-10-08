namespace ClientCommissions.Shared.Contracts;

public sealed record PayoutContract(Guid CommissionId, Guid UserId, decimal Amount);
