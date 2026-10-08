namespace ClientCommissions.Commissions.Domain.Entities;

public class ProfitEvent
{
    public Guid Id { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public EventStatus Status { get; private set; } = EventStatus.Received;
    public DateTime? ClaimedAt { get; private set; }
    public decimal Profit { get; private set; }
    public Guid ExternalId { get; private set; }
    public Guid UserId { get; private set; }

    public ProfitEvent(decimal profit, Guid externalId, Guid userId)
    {
        Id = Guid.NewGuid();
        Profit = profit;
        ExternalId = externalId;
        UserId = userId;
    }

    // claim: помечаем событие "взятым в работу" и фиксируем момент,
    // чтобы зависшие Processing (воркер упал) можно было переподхватить по таймауту
    public void MarkProcessing() => (Status, ClaimedAt) = (EventStatus.Processing, DateTime.UtcNow);
    public void MarkProcessed() => Status = EventStatus.Processed;
}
