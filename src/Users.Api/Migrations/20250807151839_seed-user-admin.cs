using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Users.Api.Migrations
{
    /// <inheritdoc />
    public partial class seeduseradmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5564f136-b9fb-4719-b4dc-b0833fa24761"),
                column: "Role",
                value: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5564f136-b9fb-4719-b4dc-b0833fa24761"),
                column: "Role",
                value: 0);
        }
    }
}
