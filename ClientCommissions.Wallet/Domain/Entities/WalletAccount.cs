namespace ClientCommissions.Wallet.Domain.Entities;

public class WalletAccount
{
    public Guid UserId { get; private set; }
    public decimal Balance { get; private set; }

    private WalletAccount() { }

    public WalletAccount(Guid userId)
    {
        UserId = userId;
        Balance = 0m;
    }

    public void Credit(decimal amount) => Balance += amount;
}
