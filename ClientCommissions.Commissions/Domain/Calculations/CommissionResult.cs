using ClientCommissions.Commissions.Domain.Entities;

namespace ClientCommissions.Commissions.Domain.Calculations;

public record CommissionResult(Guid UserId, int Level, decimal Amount, SchemaType Schema);
