using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Wallets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Wallets");
        }
    }
}
