namespace ClientCommissions.Commissions.Domain.Entities;

public class Outbox
{
    public Guid Id { get; private set; }
    public string Type { get; private set; }
    public string Payload { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public OutboxStatus Status { get; private set; } = OutboxStatus.Unsent;
    public DateTime? ClaimedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    public Outbox(string type, string payload)
    {
        Id = Guid.NewGuid();
        Type = type;
        Payload = payload;
    }

    // claim: Unsent -> Sending + отметка времени для переподхвата зависших
    public void MarkSending() => (Status, ClaimedAt) = (OutboxStatus.Sending, DateTime.UtcNow);
    public void MarkSent() => (Status, SentAt) = (OutboxStatus.Sent, DateTime.UtcNow);
}
