using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.Infrastructure.Migrations
{
    /// <summary>
    /// Everything the background services need to run on more than one instance (issue #160).
    ///
    /// Two changes, shipped together because they are one mechanism: a per-row claim on outbox
    /// messages, so two processors cannot dispatch the same settlement, and a ServiceLeases table
    /// naming which instance runs each background loop that must have a single writer.
    ///
    /// Both columns are nullable and the table starts empty, so this applies to a running database
    /// with nothing to backfill: an unclaimed message is one with no lease, which is exactly what
    /// every existing row becomes.
    ///
    /// The outbox index gains LeaseExpiresAt because the processor's hot-path query now filters on
    /// it alongside Status and NextAttemptAt.
    /// </summary>
    public partial class AddInstanceCoordinationLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAt",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LeasedBy",
                table: "OutboxMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceLeases",
                columns: table => new
                {
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLeases", x => x.Role);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAt_LeaseExpiresAt",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAt", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceLeases");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAt_LeaseExpiresAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeasedBy",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAt",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAt" });
        }
    }
}
