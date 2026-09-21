using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Users.Api.Migrations
{
    /// <summary>
    /// Brings every stored phone number to the one canonical form, then puts the "one number, one
    /// account" rule into storage as well as the application layer (issue #307).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The canonical form matches <c>TallaEgg.Core.Utilties.PhoneNumbers.Canonical</c>: punctuation
    /// and the plus removed, the international dialling prefix <c>00</c> removed, and a leading
    /// Iranian country code <c>98</c> replaced by <c>0</c> — the prefix only, which is what #297
    /// was about. A number from any other country keeps its country code.
    /// </para>
    /// <para>
    /// <b>The index is created only if the data allows it.</b> This migration runs at service
    /// startup, so an unconditional <c>CREATE UNIQUE INDEX</c> over a table that still holds a
    /// duplicate would throw, the migration would abort, and Users.Api would sit behind its
    /// readiness gate answering 503 — taking the whole stack with it, since the bot and Orders
    /// depend on it. A skipped index is a weaker database, not an outage: the application-layer
    /// rule from #303 still refuses duplicates on the way in and still refuses to resolve one on
    /// the way out.
    /// </para>
    /// <para>
    /// If it is skipped, the fix is to remove the duplicate rows and re-create the index by hand:
    /// <code>
    /// SELECT PhoneNumber, COUNT(*) FROM Users
    ///  WHERE PhoneNumber IS NOT NULL GROUP BY PhoneNumber HAVING COUNT(*) > 1;
    ///
    /// CREATE UNIQUE INDEX IX_Users_PhoneNumber ON Users(PhoneNumber) WHERE PhoneNumber IS NOT NULL;
    /// </code>
    /// </para>
    /// </remarks>
    public partial class CanonicalPhoneNumbersAndUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Persian (U+06F0..) and Arabic-Indic (U+0660..) digits become ASCII, exactly as
            // Utils.ConvertPersianDigitsToEnglish does in the application. This has to come
            // before the strip below, which would otherwise delete them as non-digits and leave
            // the row holding a fragment of a number, or nothing at all. The binary collation
            // makes the comparison exact: under the database's default collation a Persian digit
            // can compare equal to its ASCII counterpart, and the replacement would be a no-op.
            migrationBuilder.Sql(@"
                DECLARE @digit int = 0;
                WHILE @digit < 10
                BEGIN
                    UPDATE Users
                       SET PhoneNumber = REPLACE(REPLACE(
                             PhoneNumber COLLATE Latin1_General_BIN2,
                             NCHAR(0x06F0 + @digit), CHAR(48 + @digit)),
                             NCHAR(0x0660 + @digit), CHAR(48 + @digit))
                     WHERE PhoneNumber IS NOT NULL;
                    SET @digit = @digit + 1;
                END");

            // Every non-digit, one at a time, until none is left — not a fixed list of the
            // punctuation we happen to think of. Canonical strips everything that is not a digit,
            // so a row left holding a stray character this pass did not know about would stay
            // non-canonical forever while every lookup canonicalises, and that account would
            // become unfindable by phone.
            migrationBuilder.Sql(@"
                WHILE EXISTS (SELECT 1 FROM Users WHERE PhoneNumber LIKE '%[^0-9]%')
                BEGIN
                    UPDATE Users
                       SET PhoneNumber = STUFF(PhoneNumber, PATINDEX('%[^0-9]%', PhoneNumber), 1, '')
                     WHERE PhoneNumber LIKE '%[^0-9]%';
                END");

            // The international dialling prefix. An Iranian local number begins 09, never 00.
            migrationBuilder.Sql(@"
                UPDATE Users
                   SET PhoneNumber = SUBSTRING(PhoneNumber, 3, LEN(PhoneNumber))
                 WHERE PhoneNumber LIKE '00%';");

            // Iran's country code to local form — the prefix only. Country codes are prefix-free
            // and 98 is Iran's, so this cannot touch a number from anywhere else.
            migrationBuilder.Sql(@"
                UPDATE Users
                   SET PhoneNumber = '0' + SUBSTRING(PhoneNumber, 3, LEN(PhoneNumber))
                 WHERE PhoneNumber LIKE '98%';");

            // nvarchar(max) cannot be indexed. No phone number is anywhere near 450 characters,
            // so the narrowing is not a truncation risk despite what the scaffolder warns.
            //
            // Raw SQL rather than AlterColumn: this migration's model declares IX_Users_PhoneNumber,
            // and EF's generator therefore drops and recreates the indexes on a column it alters.
            // The index does not exist yet at this point, so that DROP fails with error 3701 and
            // takes the migration — and the service's startup — with it.
            migrationBuilder.Sql("ALTER TABLE Users ALTER COLUMN PhoneNumber nvarchar(450) NULL;");

            // Filtered: the column is nullable and SQL Server treats NULLs as equal in a unique
            // index, so without the filter every account that has not shared a number yet would
            // collide with the next one.
            //
            // Guarded: see the remarks above. A duplicate left in the table costs the index, not
            // the service.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                        SELECT 1 FROM Users
                         WHERE PhoneNumber IS NOT NULL
                         GROUP BY PhoneNumber
                        HAVING COUNT(*) > 1)
                BEGIN
                    CREATE UNIQUE INDEX IX_Users_PhoneNumber
                        ON Users(PhoneNumber)
                     WHERE PhoneNumber IS NOT NULL;
                END
                ELSE
                BEGIN
                    RAISERROR (
                        'IX_Users_PhoneNumber was NOT created: one or more phone numbers are held by more than one account. The application-layer rule still applies. Remove the duplicates and create the index by hand.',
                        10, 1) WITH NOWAIT;
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The index may never have been created — see Up.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_Users_PhoneNumber'
                              AND object_id = OBJECT_ID('dbo.Users'))
                    DROP INDEX IX_Users_PhoneNumber ON Users;");

            // Raw SQL for the same reason as in Up.
            migrationBuilder.Sql("ALTER TABLE Users ALTER COLUMN PhoneNumber nvarchar(max) NULL;");

            // The numbers themselves are not put back. Their canonical form is the correct one,
            // and which of several equivalent spellings a row arrived in is not recorded anywhere.
        }
    }
}
