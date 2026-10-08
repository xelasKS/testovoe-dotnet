using ClientCommissions.Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientCommissions.Users.Infrastructure;

public sealed class UsersDbContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;

    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Id).ValueGeneratedNever();
            b.HasIndex(u => u.ReferrerId);
        });

        modelBuilder.Entity<DescendantRow>().HasNoKey().ToView(null);
    }
}
