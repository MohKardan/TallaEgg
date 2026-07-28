using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BuyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SellPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Symbol_IsActive",
                table: "Quotes",
                columns: new[] { "Symbol", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Quotes");
        }
    }
}
