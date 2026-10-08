namespace ClientCommissions.Users.Contracts;

public sealed record CreateUserRequest(Guid? Id, Guid? ReferrerId);
