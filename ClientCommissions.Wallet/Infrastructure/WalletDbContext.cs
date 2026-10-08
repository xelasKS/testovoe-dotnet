using ClientCommissions.Wallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientCommissions.Wallet.Infrastructure;

public sealed class WalletDbContext : DbContext
{
    public DbSet<WalletAccount> Accounts { get; set; } = null!;
    public DbSet<PayoutRecord> Payouts { get; set; } = null!;

    public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WalletAccount>(b =>
        {
            b.HasKey(a => a.UserId);
            b.Property(a => a.UserId).ValueGeneratedNever();
            b.Property(a => a.Balance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PayoutRecord>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Amount).HasPrecision(18, 2);
            b.HasIndex(p => p.CommissionId).IsUnique();
            b.HasIndex(p => p.UserId);
        });
    }
}
