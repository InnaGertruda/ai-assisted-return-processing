using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ReturnFlow.Infrastructure.Persistence;
using ReturnFlow.Infrastructure.Persistence.Migrations;
using Xunit;

namespace ReturnFlow.UnitTests;

public sealed class MigrationModelTests
{
    [Fact]
    public void Migration_snapshot_matches_the_current_model()
    {
        var options = new DbContextOptionsBuilder<ReturnFlowDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var dbContext = new ReturnFlowDbContext(options);

        var modelDiffer = dbContext.GetService<IMigrationsModelDiffer>();
        var currentModel = dbContext.GetService<IDesignTimeModel>().Model;
        var snapshotModel = dbContext.GetService<IModelRuntimeInitializer>()
            .Initialize(new ReturnFlowDbContextModelSnapshot().Model, designTime: true);
        var differences = modelDiffer.GetDifferences(
            snapshotModel.GetRelationalModel(),
            currentModel.GetRelationalModel());

        Assert.True(
            differences.Count == 0,
            $"Pending migration operations: {string.Join(", ", differences.Select(Describe))}");
    }

    private static string Describe(MigrationOperation operation) => operation switch
    {
        AddColumnOperation value => $"AddColumn({value.Table}.{value.Name})",
        AlterColumnOperation value => $"AlterColumn({value.Table}.{value.Name})",
        CreateIndexOperation value => $"CreateIndex({value.Table}.{value.Name})",
        AddForeignKeyOperation value => $"AddForeignKey({value.Table}.{value.Name})",
        DropForeignKeyOperation value => $"DropForeignKey({value.Table}.{value.Name})",
        _ => operation.GetType().Name,
    };
}
