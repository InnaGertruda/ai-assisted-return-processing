using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ReturnFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ReturnFlowDbContext))]
[Migration("20261117010000_AddAiExecutionTraces")]
public sealed class AddAiExecutionTraces : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AiExecutionTraces",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                DemoSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ReceivedAt = table.Column<long>(type: "INTEGER", nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", maxLength: 32000, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AiExecutionTraces", x => x.Id);
                table.ForeignKey(
                    name: "FK_AiExecutionTraces_DemoSessions_DemoSessionId",
                    column: x => x.DemoSessionId,
                    principalTable: "DemoSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AiExecutionTraces_DemoSessionId_ReceivedAt",
            table: "AiExecutionTraces",
            columns: new[] { "DemoSessionId", "ReceivedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "AiExecutionTraces");
}
