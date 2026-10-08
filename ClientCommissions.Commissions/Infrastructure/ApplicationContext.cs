using ClientCommissions.Commissions.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientCommissions.Commissions.Infrastructure;

public sealed class ApplicationContext : DbContext
{
    public DbSet<Commission> Commissions { get; set; } = null!;
    public DbSet<Outbox> Outbox { get; set; } = null!;
    public DbSet<ProfitEvent> Events { get; set; } = null!;
    public DbSet<SchemaSetting> SchemaSettings { get; set; } = null!;

    public ApplicationContext(DbContextOptions<ApplicationContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfitEvent>()
            .HasIndex(e => e.ExternalId)
            .IsUnique();
        modelBuilder.Entity<ProfitEvent>()
            .Property(e => e.Profit)
            .HasPrecision(18, 2);
        modelBuilder.Entity<ProfitEvent>()
            .Property(e => e.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Commission>()
            .Property(c => c.Amount)
            .HasPrecision(18, 2);
        modelBuilder.Entity<Commission>()
            .Property(c => c.Status)
            .HasConversion<string>();
        modelBuilder.Entity<Commission>()
            .Property(c => c.Schema)
            .HasConversion<string>();

        modelBuilder.Entity<Outbox>()
            .Property(o => o.Status)
            .HasConversion<string>();

        modelBuilder.Entity<SchemaSetting>()
            .Property(s => s.Id)
            .ValueGeneratedNever();
        modelBuilder.Entity<SchemaSetting>()
            .Property(s => s.Schema)
            .HasConversion<string>();

        modelBuilder.Entity<SchemaSetting>()
            .HasData(new SchemaSetting(SchemaType.Linear));
    }
}
