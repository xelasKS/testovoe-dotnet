namespace ClientCommissions.Users.Application;

public sealed class ReferrerNotFoundException(Guid referrerId)
    : Exception($"Referrer {referrerId} not found");
