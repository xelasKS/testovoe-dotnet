namespace ClientCommissions.Commissions.Contracts;

public sealed record EventSummaryResponse(
    Guid Id, Guid UserId, decimal Profit, string Status, DateTime CreatedAt);
