using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ReturnFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ReturnFlowDbContext))]
[Migration("20261117020000_AddAtomicReturnCommit")]
public sealed class AddAtomicReturnCommit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AiRunIdempotencyRecords",
            columns: table => new
            {
                DemoSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                SubmissionKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                AiExecutionTraceId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AiRunIdempotencyRecords", x => new { x.DemoSessionId, x.SubmissionKey });
                table.ForeignKey(
                    name: "FK_AiRunIdempotencyRecords_AiExecutionTraces_AiExecutionTraceId",
                    column: x => x.AiExecutionTraceId,
                    principalTable: "AiExecutionTraces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AiRunIdempotencyRecords_DemoSessions_DemoSessionId",
                    column: x => x.DemoSessionId,
                    principalTable: "DemoSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DecisionRecords",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                DemoSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ReturnSubmissionId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                ReturnCaseId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                AiExecutionTraceId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                Outcome = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                RuleReferences = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DecisionRecords", x => x.Id);
                table.ForeignKey("FK_DecisionRecords_AiExecutionTraces_AiExecutionTraceId", x => x.AiExecutionTraceId,
                    "AiExecutionTraces", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DecisionRecords_DemoSessions_DemoSessionId", x => x.DemoSessionId,
                    "DemoSessions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DecisionRecords_ReturnCases_ReturnCaseId", x => x.ReturnCaseId,
                    "ReturnCases", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_DecisionRecords_ReturnSubmissions_ReturnSubmissionId", x => x.ReturnSubmissionId,
                    "ReturnSubmissions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_AiRunIdempotencyRecords_AiExecutionTraceId",
            "AiRunIdempotencyRecords", "AiExecutionTraceId", unique: true);
        migrationBuilder.CreateIndex("IX_DecisionRecords_AiExecutionTraceId",
            "DecisionRecords", "AiExecutionTraceId", unique: true);
        migrationBuilder.CreateIndex("IX_DecisionRecords_DemoSessionId",
            "DecisionRecords", "DemoSessionId");
        migrationBuilder.CreateIndex("IX_DecisionRecords_ReturnCaseId",
            "DecisionRecords", "ReturnCaseId", unique: true);
        migrationBuilder.CreateIndex("IX_DecisionRecords_ReturnSubmissionId",
            "DecisionRecords", "ReturnSubmissionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("AiRunIdempotencyRecords");
        migrationBuilder.DropTable("DecisionRecords");
    }
}
