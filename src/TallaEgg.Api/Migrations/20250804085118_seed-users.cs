using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallaEgg.Api.Migrations
{
    /// <inheritdoc />
    public partial class seedusers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "FirstName", "InvitationCode", "InvitedByUserId", "IsActive", "LastActiveAt", "LastName", "PhoneNumber", "TelegramId", "Username" },
                values: new object[] { new Guid("5564f136-b9fb-4719-b4dc-b0833fa24761"), new DateTime(2025, 8, 4, 12, 13, 43, 123, DateTimeKind.Local).AddTicks(4567), "مدیر", "admin", null, true, null, "کل", null, 0L, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5564f136-b9fb-4719-b4dc-b0833fa24761"));
        }
    }
}
