namespace ClientCommissions.Wallet.Domain.Entities;

public class PayoutRecord
{
    public Guid Id { get; private set; }
    public Guid CommissionId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private PayoutRecord() { }

    public PayoutRecord(Guid commissionId, Guid userId, decimal amount)
    {
        Id = Guid.NewGuid();
        CommissionId = commissionId;
        UserId = userId;
        Amount = amount;
    }
}
