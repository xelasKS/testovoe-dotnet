namespace ClientCommissions.Users.Contracts;

public sealed record CreateUserResponse(Guid Id, Guid? ReferrerId);
