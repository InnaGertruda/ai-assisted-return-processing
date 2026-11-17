using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ReturnFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ReturnFlowDbContext))]
[Migration("20261117000000_InitialDemoData")]
public sealed class InitialDemoData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DemoSessions",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ScenarioId = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                ExpiresAt = table.Column<long>(type: "INTEGER", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_DemoSessions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Orders",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                DemoSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                DisplayNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Orders", x => x.Id);
                table.UniqueConstraint("AK_Orders_Id_DemoSessionId", x => new { x.Id, x.DemoSessionId });
                table.ForeignKey(
                    name: "FK_Orders_DemoSessions_DemoSessionId",
                    column: x => x.DemoSessionId,
                    principalTable: "DemoSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OrderItems",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                OrderId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                DemoSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ProductCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ProductName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                PurchasedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrderItems", x => x.Id);
                table.UniqueConstraint(
                    "AK_OrderItems_Id_OrderId_DemoSessionId",
                    x => new { x.Id, x.OrderId, x.DemoSessionId });
                table.CheckConstraint("CK_OrderItems_PurchasedQuantity_Positive", "PurchasedQuantity > 0");
                table.ForeignKey(
                    name: "FK_OrderItems_Orders_OrderId_DemoSessionId",
                    columns: x => new { x.OrderId, x.DemoSessionId },
                    principalTable: "Orders",
                    principalColumns: new[] { "Id", "DemoSessionId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ReturnSubmissions",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                OrderId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                DemoSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                SubmissionKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                Channel = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                ReceivedAt = table.Column<long>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReturnSubmissions", x => x.Id);
                table.UniqueConstraint(
                    "AK_ReturnSubmissions_Id_OrderId_DemoSessionId",
                    x => new { x.Id, x.OrderId, x.DemoSessionId });
                table.ForeignKey(
                    name: "FK_ReturnSubmissions_Orders_OrderId_DemoSessionId",
                    columns: x => new { x.OrderId, x.DemoSessionId },
                    principalTable: "Orders",
                    principalColumns: new[] { "Id", "DemoSessionId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ReturnCases",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                OrderId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                DemoSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ReturnSubmissionId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                Origin = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReturnCases", x => x.Id);
                table.UniqueConstraint(
                    "AK_ReturnCases_Id_OrderId_DemoSessionId",
                    x => new { x.Id, x.OrderId, x.DemoSessionId });
                table.ForeignKey(
                    name: "FK_ReturnCases_Orders_OrderId_DemoSessionId",
                    columns: x => new { x.OrderId, x.DemoSessionId },
                    principalTable: "Orders",
                    principalColumns: new[] { "Id", "DemoSessionId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReturnCases_ReturnSubmissions_ReturnSubmissionId_OrderId_DemoSessionId",
                    columns: x => new { x.ReturnSubmissionId, x.OrderId, x.DemoSessionId },
                    principalTable: "ReturnSubmissions",
                    principalColumns: new[] { "Id", "OrderId", "DemoSessionId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ReturnItems",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                ReturnCaseId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                OrderId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                OrderItemId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                DemoSessionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                State = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReturnItems", x => x.Id);
                table.CheckConstraint("CK_ReturnItems_Quantity_Positive", "Quantity > 0");
                table.ForeignKey(
                    name: "FK_ReturnItems_OrderItems_OrderItemId_OrderId_DemoSessionId",
                    columns: x => new { x.OrderItemId, x.OrderId, x.DemoSessionId },
                    principalTable: "OrderItems",
                    principalColumns: new[] { "Id", "OrderId", "DemoSessionId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ReturnItems_ReturnCases_ReturnCaseId_OrderId_DemoSessionId",
                    columns: x => new { x.ReturnCaseId, x.OrderId, x.DemoSessionId },
                    principalTable: "ReturnCases",
                    principalColumns: new[] { "Id", "OrderId", "DemoSessionId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DemoSessions_ExpiresAt",
            table: "DemoSessions",
            column: "ExpiresAt");
        migrationBuilder.CreateIndex(
            name: "IX_Orders_DemoSessionId",
            table: "Orders",
            column: "DemoSessionId");
        migrationBuilder.CreateIndex(
            name: "IX_OrderItems_DemoSessionId_OrderId",
            table: "OrderItems",
            columns: new[] { "DemoSessionId", "OrderId" });
        migrationBuilder.CreateIndex(
            name: "IX_OrderItems_OrderId_DemoSessionId",
            table: "OrderItems",
            columns: new[] { "OrderId", "DemoSessionId" });
        migrationBuilder.CreateIndex(
            name: "IX_ReturnSubmissions_DemoSessionId_SubmissionKey",
            table: "ReturnSubmissions",
            columns: new[] { "DemoSessionId", "SubmissionKey" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ReturnSubmissions_OrderId_DemoSessionId",
            table: "ReturnSubmissions",
            columns: new[] { "OrderId", "DemoSessionId" });
        migrationBuilder.CreateIndex(
            name: "IX_ReturnCases_OrderId_DemoSessionId",
            table: "ReturnCases",
            columns: new[] { "OrderId", "DemoSessionId" });
        migrationBuilder.CreateIndex(
            name: "IX_ReturnCases_ReturnSubmissionId",
            table: "ReturnCases",
            column: "ReturnSubmissionId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ReturnCases_ReturnSubmissionId_OrderId_DemoSessionId",
            table: "ReturnCases",
            columns: new[] { "ReturnSubmissionId", "OrderId", "DemoSessionId" });
        migrationBuilder.CreateIndex(
            name: "IX_ReturnItems_DemoSessionId_OrderItemId",
            table: "ReturnItems",
            columns: new[] { "DemoSessionId", "OrderItemId" });
        migrationBuilder.CreateIndex(
            name: "IX_ReturnItems_OrderItemId_OrderId_DemoSessionId",
            table: "ReturnItems",
            columns: new[] { "OrderItemId", "OrderId", "DemoSessionId" });
        migrationBuilder.CreateIndex(
            name: "IX_ReturnItems_ReturnCaseId_OrderId_DemoSessionId",
            table: "ReturnItems",
            columns: new[] { "ReturnCaseId", "OrderId", "DemoSessionId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReturnItems");
        migrationBuilder.DropTable(name: "OrderItems");
        migrationBuilder.DropTable(name: "ReturnCases");
        migrationBuilder.DropTable(name: "ReturnSubmissions");
        migrationBuilder.DropTable(name: "Orders");
        migrationBuilder.DropTable(name: "DemoSessions");
    }
}
