using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.InventoryService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class idempotencyKeyAddedToStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                schema: "inventory",
                table: "StockMovements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_StockMovements_IdempotencyItem",
                schema: "inventory",
                table: "StockMovements",
                columns: new[] { "TenantId", "IdempotencyKey", "ProductId", "ProductVariantId" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_StockMovements_IdempotencyItem",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "inventory",
                table: "StockMovements");
        }
    }
}
