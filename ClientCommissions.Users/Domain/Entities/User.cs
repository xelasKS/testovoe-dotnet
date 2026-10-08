namespace ClientCommissions.Users.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public Guid? ReferrerId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private User() { }

    public User(Guid id, Guid? referrerId)
    {
        Id = id;
        ReferrerId = referrerId;
    }
}
