using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ReturnFlow.Domain;

namespace ReturnFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ReturnFlowDbContext))]
public sealed class ReturnFlowDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.15");

        modelBuilder.Entity<AiTraceRecord>(entity =>
        {
            entity.ToTable("AiExecutionTraces");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).IsRequired().HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ReceivedAt).IsRequired().HasConversion<long>();
            entity.Property(x => x.PayloadJson).IsRequired().HasMaxLength(32_000);
            entity.HasIndex(x => new { x.DemoSessionId, x.ReceivedAt });
            entity.HasOne<DemoSession>()
                .WithMany()
                .HasForeignKey(x => x.DemoSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiRunIdempotencyRecord>(entity =>
        {
            entity.ToTable("AiRunIdempotencyRecords");
            entity.HasKey(x => new { x.DemoSessionId, x.SubmissionKey });
            entity.Property(x => x.DemoSessionId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.SubmissionKey).IsRequired().HasMaxLength(120);
            entity.Property(x => x.PayloadHash).IsRequired().HasMaxLength(64);
            entity.Property(x => x.AiExecutionTraceId).IsRequired().HasMaxLength(80);
            entity.HasIndex(x => x.AiExecutionTraceId).IsUnique();
            entity.HasOne<DemoSession>().WithMany().HasForeignKey(x => x.DemoSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AiTraceRecord>().WithMany().HasForeignKey(x => x.AiExecutionTraceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DecisionRecord>(entity =>
        {
            entity.ToTable("DecisionRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).IsRequired().HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ReturnSubmissionId).IsRequired().HasMaxLength(80);
            entity.Property(x => x.ReturnCaseId).IsRequired().HasMaxLength(80);
            entity.Property(x => x.AiExecutionTraceId).IsRequired().HasMaxLength(80);
            entity.Property(x => x.Outcome).IsRequired().HasMaxLength(32);
            entity.Property(x => x.RuleReferences).IsRequired().HasMaxLength(512);
            entity.Property(x => x.CreatedAt).IsRequired().HasConversion<long>();
            entity.HasIndex(x => x.DemoSessionId);
            entity.HasIndex(x => x.ReturnCaseId).IsUnique();
            entity.HasIndex(x => x.ReturnSubmissionId);
            entity.HasIndex(x => x.AiExecutionTraceId).IsUnique();
            entity.HasOne<DemoSession>().WithMany().HasForeignKey(x => x.DemoSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReturnSubmission>().WithMany().HasForeignKey(x => x.ReturnSubmissionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ReturnCase>().WithMany().HasForeignKey(x => x.ReturnCaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AiTraceRecord>().WithMany().HasForeignKey(x => x.AiExecutionTraceId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DemoSession>(entity =>
        {
            entity.ToTable("DemoSessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ScenarioId).IsRequired().HasMaxLength(16);
            entity.Property(x => x.CreatedAt).IsRequired().HasConversion<long>();
            entity.Property(x => x.ExpiresAt).IsRequired().HasConversion<long>();
            entity.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<DemoOrder>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.Id, x.DemoSessionId });
            entity.Property(x => x.Id).IsRequired().HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.DisplayNumber).IsRequired().HasMaxLength(32);
            entity.HasIndex(x => x.DemoSessionId);
            entity.HasOne<DemoSession>()
                .WithMany()
                .HasForeignKey(x => x.DemoSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveModelUsageRecord>(entity =>
        {
            entity.ToTable("LiveModelUsageRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).IsRequired().HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ClientPartition).IsRequired().HasMaxLength(64);
            entity.Property(x => x.RequestedAt).IsRequired().HasConversion<long>();
            entity.HasIndex(x => x.RequestedAt);
            entity.HasIndex(x => new { x.DemoSessionId, x.RequestedAt });
            entity.HasIndex(x => new { x.ClientPartition, x.RequestedAt });
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
            entity.Property(x => x.Id).IsRequired().HasMaxLength(80);
            entity.Property(x => x.OrderId).IsRequired().HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ProductCode).IsRequired().HasMaxLength(32);
            entity.Property(x => x.ProductName).IsRequired().HasMaxLength(160);
            entity.Property(x => x.PurchasedQuantity).IsRequired();
            entity.HasIndex(x => new { x.DemoSessionId, x.OrderId });
            entity.HasIndex(x => new { x.OrderId, x.DemoSessionId });
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
            entity.Property(x => x.Id).IsRequired().HasMaxLength(80);
            entity.Property(x => x.OrderId).IsRequired().HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.SubmissionKey).IsRequired().HasMaxLength(120);
            entity.Property(x => x.Message).IsRequired().HasMaxLength(2_000);
            entity.Property(x => x.Channel).IsRequired().HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ReceivedAt).IsRequired().HasConversion<long>();
            entity.HasIndex(x => new { x.DemoSessionId, x.SubmissionKey }).IsUnique();
            entity.HasIndex(x => new { x.OrderId, x.DemoSessionId });
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
            entity.Property(x => x.Id).IsRequired().HasMaxLength(80);
            entity.Property(x => x.OrderId).IsRequired().HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ReturnSubmissionId).IsRequired().HasMaxLength(80);
            entity.Property(x => x.Origin).IsRequired().HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.CreatedAt).IsRequired().HasConversion<long>();
            entity.HasIndex(x => new { x.OrderId, x.DemoSessionId });
            entity.HasIndex(x => x.ReturnSubmissionId).IsUnique();
            entity.HasIndex(x => new { x.ReturnSubmissionId, x.OrderId, x.DemoSessionId });
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
            entity.Property(x => x.Id).IsRequired().HasMaxLength(80);
            entity.Property(x => x.ReturnCaseId).IsRequired().HasMaxLength(80);
            entity.Property(x => x.OrderId).IsRequired().HasMaxLength(80);
            entity.Property(x => x.OrderItemId).IsRequired().HasMaxLength(80);
            entity.Property(x => x.DemoSessionId).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Quantity).IsRequired();
            entity.Property(x => x.State).IsRequired().HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.DemoSessionId, x.OrderItemId });
            entity.HasIndex(x => new { x.OrderItemId, x.OrderId, x.DemoSessionId });
            entity.HasIndex(x => new { x.ReturnCaseId, x.OrderId, x.DemoSessionId });
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
