using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ReturnFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ReturnFlowDbContext))]
[Migration("20261118000000_AddLiveModelUsageLimits")]
public sealed class AddLiveModelUsageLimits : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LiveModelUsageRecords",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                DemoSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ClientPartition = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                RequestedAt = table.Column<long>(type: "INTEGER", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_LiveModelUsageRecords", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_LiveModelUsageRecords_ClientPartition_RequestedAt",
            table: "LiveModelUsageRecords",
            columns: new[] { "ClientPartition", "RequestedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_LiveModelUsageRecords_DemoSessionId_RequestedAt",
            table: "LiveModelUsageRecords",
            columns: new[] { "DemoSessionId", "RequestedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_LiveModelUsageRecords_RequestedAt",
            table: "LiveModelUsageRecords",
            column: "RequestedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("LiveModelUsageRecords");
}
