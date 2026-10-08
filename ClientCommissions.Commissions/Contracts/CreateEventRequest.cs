namespace ClientCommissions.Commissions.Contracts;

public sealed record CreateEventRequest(Guid ExternalId, Guid UserId, decimal Profit);
