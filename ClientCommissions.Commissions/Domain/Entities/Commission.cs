namespace ClientCommissions.Commissions.Domain.Entities;

public class Commission
{
    public Guid Id { get; private set; }
    public int Level { get; private set; }
    public SchemaType Schema { get; private set; }
    public Guid SourceEventId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; private set; }
    public CommissionStatus Status { get; private set; } = CommissionStatus.Unpaid;
    public decimal Amount { get; private set; }
    public Guid UserId { get; private set; }

    public Commission(decimal amount, Guid userId, int level, SchemaType schema, Guid sourceEventId)
    {
        Amount = amount;
        UserId = userId;
        Level = level;
        Schema = schema;
        SourceEventId = sourceEventId;
        Id = Guid.NewGuid();
    }
    public void MarkPaid() => (Status, PaidAt) = (CommissionStatus.Paid, DateTime.UtcNow);
    public void MarkPaying() => Status = CommissionStatus.Paying;
}
