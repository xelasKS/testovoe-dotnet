namespace ClientCommissions.Commissions.Contracts;

public sealed record CommissionLine(
    Guid UserId, int Level, decimal Amount, string Schema, string Status);

public sealed record EventDetailResponse(
    Guid Id,
    Guid UserId,
    decimal Profit,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<CommissionLine> Commissions);
