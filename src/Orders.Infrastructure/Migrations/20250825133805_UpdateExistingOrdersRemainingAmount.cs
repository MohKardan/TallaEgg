using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExistingOrdersRemainingAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing orders to set RemainingAmount = Amount
            migrationBuilder.Sql("UPDATE Orders SET RemainingAmount = Amount WHERE RemainingAmount = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert: set RemainingAmount back to 0
            migrationBuilder.Sql("UPDATE Orders SET RemainingAmount = 0");
        }
    }
}
