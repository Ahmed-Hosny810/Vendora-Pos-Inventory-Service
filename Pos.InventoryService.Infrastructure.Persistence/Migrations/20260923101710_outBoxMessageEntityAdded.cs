using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.InventoryService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class outBoxMessageEntityAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                schema: "inventory",
                table: "LowStockAlerts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_LowStockAlerts_ActiveItem",
                schema: "inventory",
                table: "LowStockAlerts",
                columns: new[] { "TenantId", "BranchId", "ProductId", "ProductVariantId" },
                unique: true,
                filter: "[Status] = N'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_OccurredAt_Id",
                schema: "inventory",
                table: "OutboxMessages",
                columns: new[] { "OccurredAt", "Id" },
                filter: "[PublishedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "inventory");

            migrationBuilder.DropIndex(
                name: "UX_LowStockAlerts_ActiveItem",
                schema: "inventory",
                table: "LowStockAlerts");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                schema: "inventory",
                table: "LowStockAlerts");
        }
    }
}
