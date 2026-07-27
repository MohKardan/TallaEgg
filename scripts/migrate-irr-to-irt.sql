/*
    مهاجرت کد واحد پول از IRR (ریال) به IRT (تومان)
    ------------------------------------------------
    چرا: مبالغ ذخیره‌شده در دیتابیس همیشه «تومان» بوده‌اند (قیمت‌ها به تومان وارد
    می‌شوند و هیچ تبدیل ریال/تومانی در کد وجود ندارد)، اما کد دارایی IRR بود و همین
    باعث ابهام می‌شد که عدد ذخیره‌شده ریال است یا تومان.

    این اسکریپت فقط «برچسبِ» دارایی را عوض می‌کند و هیچ مبلغی را تغییر نمی‌دهد.
    هیچ ضرب یا تقسیمی انجام نمی‌شود — اعداد دقیقاً همان می‌مانند.

    اجرا:
        sqlcmd -S "(localdb)\MSSQLLocalDB" -i scripts\migrate-irr-to-irt.sql

    نکته: قبل از اجرا سرویس‌ها را متوقف کنید تا رکوردی وسط کار نوشته نشود.
*/

SET NOCOUNT ON;

-------------------------------------------------------------------------------
PRINT '=== دیتابیس کیف پول: TallaEggWallet ===';
-------------------------------------------------------------------------------
USE TallaEggWallet;
GO

BEGIN TRANSACTION;

    -- کیف پول‌ها
    UPDATE Wallets      SET Asset    = 'IRT' WHERE Asset    = 'IRR';
    PRINT '  Wallets.Asset:            ' + CAST(@@ROWCOUNT AS varchar(10)) + ' ردیف';

    -- تراکنش‌های جدید
    UPDATE Transactions SET Currency = 'IRT' WHERE Currency = 'IRR';
    PRINT '  Transactions.Currency:    ' + CAST(@@ROWCOUNT AS varchar(10)) + ' ردیف';

    -- تراکنش‌های legacy (اگر داده‌ای داشته باشد)
    UPDATE WalletTransactions SET Asset = 'IRT' WHERE Asset = 'IRR';
    PRINT '  WalletTransactions.Asset: ' + CAST(@@ROWCOUNT AS varchar(10)) + ' ردیف';

    -- کیف پول اعتباری ریال، در صورت وجود
    UPDATE Wallets      SET Asset    = 'CREDIT_IRT' WHERE Asset    = 'CREDIT_IRR';
    UPDATE Transactions SET Currency = 'CREDIT_IRT' WHERE Currency = 'CREDIT_IRR';

COMMIT TRANSACTION;
GO

-------------------------------------------------------------------------------
PRINT '=== دیتابیس سفارشات: TallaEggOrders ===';
-------------------------------------------------------------------------------
USE TallaEggOrders;
GO

BEGIN TRANSACTION;

    -- نماد جفت معاملاتی در سفارش‌ها
    UPDATE Orders SET Asset  = 'MAUA/IRT' WHERE Asset  = 'MAUA/IRR';
    PRINT '  Orders.Asset:             ' + CAST(@@ROWCOUNT AS varchar(10)) + ' ردیف';

    -- نماد جفت معاملاتی در معاملات
    UPDATE Trades SET Symbol = 'MAUA/IRT' WHERE Symbol = 'MAUA/IRR';
    PRINT '  Trades.Symbol:            ' + CAST(@@ROWCOUNT AS varchar(10)) + ' ردیف';

    -- payload پیام‌های Outbox شامل نماد است؛ در پیام‌های پردازش‌نشده اصلاح شود
    UPDATE OutboxMessages
       SET Payload = REPLACE(Payload, 'MAUA/IRR', 'MAUA/IRT')
     WHERE Payload LIKE '%MAUA/IRR%';
    PRINT '  OutboxMessages.Payload:   ' + CAST(@@ROWCOUNT AS varchar(10)) + ' ردیف';

COMMIT TRANSACTION;
GO

-------------------------------------------------------------------------------
PRINT '=== بررسی نهایی (باید هیچ IRR باقی نمانده باشد) ===';
-------------------------------------------------------------------------------
USE TallaEggWallet;
GO
SELECT 'Wallets'      AS [جدول], Asset    AS [دارایی], COUNT(*) AS [تعداد] FROM Wallets      GROUP BY Asset
UNION ALL
SELECT 'Transactions' AS [جدول], Currency AS [دارایی], COUNT(*) AS [تعداد] FROM Transactions GROUP BY Currency;
GO

USE TallaEggOrders;
GO
SELECT 'Orders' AS [جدول], Asset  AS [نماد], COUNT(*) AS [تعداد] FROM Orders GROUP BY Asset
UNION ALL
SELECT 'Trades' AS [جدول], Symbol AS [نماد], COUNT(*) AS [تعداد] FROM Trades GROUP BY Symbol;
GO
