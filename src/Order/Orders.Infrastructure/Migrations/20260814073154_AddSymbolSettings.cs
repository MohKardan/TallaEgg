using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSymbolSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SymbolSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SymbolSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SymbolSettings_Symbol",
                table: "SymbolSettings",
                column: "Symbol",
                unique: true);

            // Seeded active, not left to the inactive-by-default row a fresh GetOrCreateAsync
            // would produce — these three symbols were already live and tradable the moment
            // before this migration ran. A symbol added after this one starts inactive, same as
            // SymbolSettings.CreateDefault documents, since nothing was trading it before.
            migrationBuilder.InsertData(
                table: "SymbolSettings",
                columns: new[] { "Id", "Symbol", "IsActive", "UpdatedByUserId", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("9a1b1a2e-6b7e-4b2e-8b2e-0a1a1a1a1a01"), "MAUA/IRT", true, Guid.Empty, new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9a1b1a2e-6b7e-4b2e-8b2e-0a1a1a1a1a02"), "SEKE_BAHAR/IRT", true, Guid.Empty, new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("9a1b1a2e-6b7e-4b2e-8b2e-0a1a1a1a1a03"), "BTC/IRT", true, Guid.Empty, new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SymbolSettings");
        }
    }
}
