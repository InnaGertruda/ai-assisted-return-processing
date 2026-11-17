using Microsoft.EntityFrameworkCore;
using ReturnFlow.Domain;

namespace ReturnFlow.Infrastructure.Persistence;

public sealed class ReturnFlowDbContext(DbContextOptions<ReturnFlowDbContext> options)
    : DbContext(options)
{
    public DbSet<DemoSession> DemoSessions => Set<DemoSession>();

    public DbSet<DemoOrder> Orders => Set<DemoOrder>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<ReturnSubmission> ReturnSubmissions => Set<ReturnSubmission>();

    public DbSet<ReturnCase> ReturnCases => Set<ReturnCase>();

    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DemoSession>(entity =>
        {
            entity.ToTable("DemoSessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(64);
            entity.Property(x => x.ScenarioId).HasMaxLength(16);
            entity.Property(x => x.CreatedAt).HasConversion<long>();
            entity.Property(x => x.ExpiresAt).HasConversion<long>();
            entity.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<DemoOrder>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.DemoSessionId });
            entity.Property(x => x.Id).HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).HasMaxLength(64);
            entity.Property(x => x.DisplayNumber).HasMaxLength(32);
            entity.HasIndex(x => x.DemoSessionId);
            entity.HasOne<DemoSession>()
                .WithMany()
                .HasForeignKey(x => x.DemoSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable(
                "OrderItems",
                table => table.HasCheckConstraint(
                    "CK_OrderItems_PurchasedQuantity_Positive",
                    "PurchasedQuantity > 0"));
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.OrderId, x.DemoSessionId });
            entity.Property(x => x.Id).HasMaxLength(80);
            entity.Property(x => x.OrderId).HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).HasMaxLength(64);
            entity.Property(x => x.ProductCode).HasMaxLength(32);
            entity.Property(x => x.ProductName).HasMaxLength(160);
            entity.HasIndex(x => new { x.DemoSessionId, x.OrderId });
            entity.HasOne<DemoOrder>()
                .WithMany()
                .HasForeignKey(x => new { x.OrderId, x.DemoSessionId })
                .HasPrincipalKey(x => new { x.Id, x.DemoSessionId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReturnSubmission>(entity =>
        {
            entity.ToTable("ReturnSubmissions");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.OrderId, x.DemoSessionId });
            entity.Property(x => x.Id).HasMaxLength(80);
            entity.Property(x => x.OrderId).HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).HasMaxLength(64);
            entity.Property(x => x.SubmissionKey).HasMaxLength(120);
            entity.Property(x => x.Message).HasMaxLength(2_000);
            entity.Property(x => x.Channel).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ReceivedAt).HasConversion<long>();
            entity.HasIndex(x => new { x.DemoSessionId, x.SubmissionKey }).IsUnique();
            entity.HasOne<DemoOrder>()
                .WithMany()
                .HasForeignKey(x => new { x.OrderId, x.DemoSessionId })
                .HasPrincipalKey(x => new { x.Id, x.DemoSessionId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReturnCase>(entity =>
        {
            entity.ToTable("ReturnCases");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.OrderId, x.DemoSessionId });
            entity.Property(x => x.Id).HasMaxLength(80);
            entity.Property(x => x.OrderId).HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).HasMaxLength(64);
            entity.Property(x => x.ReturnSubmissionId).HasMaxLength(80);
            entity.Property(x => x.Origin).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.CreatedAt).HasConversion<long>();
            entity.HasIndex(x => x.ReturnSubmissionId).IsUnique();
            entity.HasOne<DemoOrder>()
                .WithMany()
                .HasForeignKey(x => new { x.OrderId, x.DemoSessionId })
                .HasPrincipalKey(x => new { x.Id, x.DemoSessionId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReturnSubmission>()
                .WithMany()
                .HasForeignKey(x => new { x.ReturnSubmissionId, x.OrderId, x.DemoSessionId })
                .HasPrincipalKey(x => new { x.Id, x.OrderId, x.DemoSessionId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReturnItem>(entity =>
        {
            entity.ToTable(
                "ReturnItems",
                table => table.HasCheckConstraint(
                    "CK_ReturnItems_Quantity_Positive",
                    "Quantity > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(80);
            entity.Property(x => x.ReturnCaseId).HasMaxLength(80);
            entity.Property(x => x.OrderId).HasMaxLength(80);
            entity.Property(x => x.OrderItemId).HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).HasMaxLength(64);
            entity.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.DemoSessionId, x.OrderItemId });
            entity.HasOne<ReturnCase>()
                .WithMany()
                .HasForeignKey(x => new { x.ReturnCaseId, x.OrderId, x.DemoSessionId })
                .HasPrincipalKey(x => new { x.Id, x.OrderId, x.DemoSessionId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrderItem>()
                .WithMany()
                .HasForeignKey(x => new { x.OrderItemId, x.OrderId, x.DemoSessionId })
                .HasPrincipalKey(x => new { x.Id, x.OrderId, x.DemoSessionId })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
