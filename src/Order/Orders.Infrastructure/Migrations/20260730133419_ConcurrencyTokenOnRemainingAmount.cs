using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Infrastructure.Migrations
{
    /// <summary>
    /// Deliberately empty, and deliberately kept.
    ///
    /// Marking Order.RemainingAmount as a concurrency token (issue #74) changes how EF writes
    /// — every UPDATE gains "WHERE RemainingAmount = &lt;the value read&gt;" — but not the
    /// schema, so there is no DDL to emit. The migration exists to keep the model snapshot in
    /// step with the model; without it the next migration would carry this change as a
    /// surprise, and EF would report pending model changes.
    /// </summary>
    public partial class ConcurrencyTokenOnRemainingAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
