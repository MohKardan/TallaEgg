using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wallet.Api.Migrations
{
    /// <summary>
    /// جدول TradeSettlements را می‌سازد و سطرهای معاملاتی که از قبل تسویه شده‌اند را پر می‌کند.
    ///
    /// این جدول سد یکتایی تسویه است (issue #42): چون TradeId کلید اصلی است، دو تسویهٔ
    /// همزمان از یک معامله دیگر نمی‌توانند هر دو اعمال شوند.
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
            // چرا این بخش الزامی است و نمی‌تواند یک قدم جداگانه باشد:
            // معاملاتی که پیش از این migration تسویه شده‌اند سطری در جدول جدید ندارند.
            // چون مسیر جدید تسویه، «قبلاً تسویه شده؟» را از همین جدول می‌پرسد، بدون
            // backfill یک re-drive روی آن معاملات دوباره تسویه‌شان می‌کرد — یعنی این
            // migration دقیقاً همان تسویهٔ دوگانه‌ای را می‌ساخت که برای رفعش نوشته شده.
            //
            // منبع حقیقت برای «چه چیزی تسویه شده» جدول Transactions است: هر تسویه چهار
            // سطر با Type = Trade (مقدار ۲) و ReferenceId برابر با شناسهٔ معامله می‌نویسد.
            //
            // نکات پیاده‌سازی:
            // • TRY_CAST و نه CAST — اگر ReferenceId قدیمی‌ای وجود داشته باشد که Guid
            //   معتبر نیست، CAST کل migration را می‌شکند؛ TRY_CAST آن را NULL می‌کند و
            //   شرط WHERE کنارش می‌گذارد.
            // • MIN(CreatedAt) به‌عنوان SettledAt — زمان واقعی تسویه، نه زمان اجرای migration.
            // • MAX(...) برای Symbol/مقادیر: هر چهار سطر یک معامله متعلق به یک تسویه‌اند،
            //   ولی چون GROUP BY لازم است از تجمیع استفاده می‌شود.
            // • مقادیر و طرفین معامله در Transactions ذخیره نشده‌اند (فقط مبلغ هر پایه و
            //   کیف پول). بازسازی دقیقشان نیازمند join با دیتابیس سرویس سفارش‌هاست که از
            //   داخل این migration ممکن نیست. چون این ستون‌ها فقط برای حسابرسی‌اند و نه
            //   برای تضمین یکتایی، برای سطرهای قدیمی صفر/خالی می‌مانند و با یک نشانهٔ
            //   صریح در Symbol علامت‌گذاری می‌شوند تا با سطرهای واقعی اشتباه نشوند.
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
