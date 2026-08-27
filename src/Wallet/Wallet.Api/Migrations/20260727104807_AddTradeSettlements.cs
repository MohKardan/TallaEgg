using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallet.Api.Migrations
{
    /// <summary>
    /// Creates the TradeSettlements table and backfills rows for trades that already settled.
    ///
    /// This table is the settlement uniqueness barrier (issue #42): because TradeId is the primary
    /// key, two concurrent settlements of one trade can no longer both apply.
    /// </summary>
    public partial class AddTradeSettlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradeSettlements",
                columns: table => new
                {
                    TradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    QuoteQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeSettlements", x => x.TradeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TradeSettlements_SettledAt",
                table: "TradeSettlements",
                column: "SettledAt");

            // ── Backfill ──────────────────────────────────────────────────────────────
            //
            // Why this is mandatory and cannot be a separate step:
            // trades settled before this migration have no row in the new table. Because the new
            // settlement path asks that table "already settled?", without a backfill a re-drive
            // over those trades would settle them a second time — the migration would create
            // exactly the double settlement it exists to prevent.
            //
            // The source of truth for "what has settled" is the Transactions table: each
            // settlement writes four rows with Type = Trade (value 2) and ReferenceId equal to the
            // trade id.
            //
            // Implementation notes:
            // - TRY_CAST rather than CAST: if some legacy ReferenceId is not a valid Guid, CAST
            //   fails the whole migration, whereas TRY_CAST yields NULL and the WHERE clause drops
            //   the row.
            // - MIN(CreatedAt) as SettledAt — the real settlement time, not the migration's.
            // - MAX(...) for Symbol and the amounts: all four rows of a trade belong to one
            //   settlement, but a GROUP BY requires an aggregate.
            // - Amounts and counterparties are not stored in Transactions (only the per-leg amount
            //   and its wallet). Reconstructing them exactly would need a join against the Orders
            //   database, which is not possible from inside this migration. Since these columns are
            //   for audit only and play no part in the uniqueness guarantee, backfilled rows leave
            //   them zero/empty and are tagged explicitly in Symbol so they cannot be mistaken for
            //   real ones.
            migrationBuilder.Sql(@"
INSERT INTO TradeSettlements (TradeId, SettledAt, Symbol, Quantity, QuoteQuantity, BuyerUserId, SellerUserId)
SELECT
    TRY_CAST(t.ReferenceId AS uniqueidentifier)  AS TradeId,
    MIN(t.CreatedAt)                             AS SettledAt,
    'BACKFILLED'                                 AS Symbol,
    0                                            AS Quantity,
    0                                            AS QuoteQuantity,
    '00000000-0000-0000-0000-000000000000'       AS BuyerUserId,
    '00000000-0000-0000-0000-000000000000'       AS SellerUserId
FROM Transactions t
WHERE t.Type = 2                                        -- TransactionType.Trade
  AND t.ReferenceId IS NOT NULL
  AND TRY_CAST(t.ReferenceId AS uniqueidentifier) IS NOT NULL
GROUP BY TRY_CAST(t.ReferenceId AS uniqueidentifier);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeSettlements");
        }
    }
}
