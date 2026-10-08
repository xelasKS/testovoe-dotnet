namespace ClientCommissions.Users.Infrastructure;

public sealed class DescendantRow
{
    public Guid UserId { get; set; }
    public int Depth { get; set; }
}
