using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionReferenceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReferenceId",
                table: "Transactions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_WalletId_ReferenceId",
                table: "Transactions",
                columns: new[] { "WalletId", "ReferenceId" },
                unique: true,
                filter: "[ReferenceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_WalletId_ReferenceId",
                table: "Transactions");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceId",
                table: "Transactions",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
