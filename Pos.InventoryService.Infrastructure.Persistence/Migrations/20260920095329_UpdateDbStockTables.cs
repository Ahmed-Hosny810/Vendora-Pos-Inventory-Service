using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.InventoryService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDbStockTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LowStockThreshold",
                schema: "inventory",
                table: "StockMovements",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "BalanceRowVersionAtCount",
                schema: "inventory",
                table: "StockAdjustmentItems",
                type: "varbinary(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_TenantId_ReferenceId",
                schema: "inventory",
                table: "StockReservations",
                columns: new[] { "TenantId", "ReferenceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockReservations_TenantId_ReferenceId",
                schema: "inventory",
                table: "StockReservations");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "BalanceRowVersionAtCount",
                schema: "inventory",
                table: "StockAdjustmentItems");
        }
    }
}
