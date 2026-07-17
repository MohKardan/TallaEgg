طرح پیاده‌سازی و تقسیم کار برای تیم ۲ نفره — تبدیل قابلیت‌های ربات به وب‌اپ

مفروضات:
- تیم: ۲ نفر (Developer A و Developer B). هر نفر توان کار موازی روی یک سرویس را دارد؛ کارهای بحرانی زوجی انجام می‌شوند.
- هدف کوتاه‌مدت: امن‌سازی و اطمینان از یکپارچگی مالی برای محیط staging و آماده‌سازی مهاجرت به وب‌اپ.
- دوره‌های زمانی: Sprint = 2 هفته.

قدم‌های فوری (روز ۰ — ۱):
- Dev A: ایجاد branch `hotfix/secrets-rotate`، لیست سرچ برای کلیدهای hardcode، و آماده‌سازی دستورالعمل چرخش.
- Dev B: تماس با BotFather و بازتولید توکن ربات (در صورت نیاز)، سپس آماده‌سازی Env/Secret template.
- هردو (نیم‌روز): قفل کردن CORS در کانفیگ محلی و غیرفعال کردن TLS-bypass در branch محلی (بدون merge فوری تا تست).

Sprint 1 (هفته 1-2) — بحرانی‌ترین موارد (تمرکز: یکپارچگی مالی)
- هدف: جلوگیری از «پول گم‌شده» و اطمینان از ترتیب درست قفل/تطبیق.
- Dev A (Backend - تراکنشی):
  - پیاده‌سازی Transaction واحد در `WalletService.ApplyTradeAsync` (بررسی SaveChanges واحد یا IDbContextTransaction).
  - افزودن `RowVersion` و handling برای `DbUpdateConcurrencyException` روی `WalletEntity` و `Order`.
  - نوشتن تست‌های واحد برای failure case (crash بین مراحل) و سناریوهای rollback.
- Dev B (Matching & DI):
  - اصلاح ثبت `MatchingEngineService` (حذف ثبت دوگانه، استفاده از Singleton/HostedService صحیح).
  - اصلاح ترتیب Lock balance قبل از ارسال سفارش و بازبینی `OrderService.CreateOrderAsync`.
  - اضافه کردن Idempotency token handling برای تراکنش‌ها.
- مشترک (Pair): مرج PRها بعد از review و اجرای تست‌های مالی روی staging.

Sprint 2 (هفته 3-4) — معماری، API و کیفیت
- هدف: جدا سازی لایه‌ها، یکدست‌سازی API و آماده‌سازی مهاجرت به وب.
- Dev A:
  - استخراج اینترفیس‌های کلاینت‌ها به Core/Application و پیاده‌سازی در Infrastructure.
  - جایگزینی `new HttpClient()` با `IHttpClientFactory` و Typed Clients.
- Dev B:
  - افزودن Middleware سراسری خطا (`ProblemDetails`) و یکدست‌سازی پاسخ‌ها.
  - پیاده‌سازی FluentValidation برای DTOهای اصلی و Value Objects (`Money`,`Symbol`).
- مشترک:
  - تعریف قرارداد API برای وب‌اپ (OpenAPI/Swagger) — تبدیل endpointهای ربات به مسیرهای RESTful.
  - بازنویسی endpointهای GET تغییردهنده.

Sprint 3 (هفته 5-6) — تست، کشینگ و پاکسازی
- Dev A:
  - اجرای AsNoTracking جایی که لازم است و بهینه‌سازی کوئری‌ها.
  - پیاده‌سازی Outbox ساده یا الگوی Retry/Reconciliation برای تراکنش‌های بین سرویس‌ها.
- Dev B:
  - نوشتن تست‌های یکپارچه برای flows مالی (integration tests با DB در حافظه یا docker-compose).
  - حذف کدهای تکراری و پاکسازی فایل‌ها/پوشه‌های قدیمی.
- مشترک:
  - تهیه Runbook استقرار و playbook برای موارد بحرانی (چگونه پول را بازسازی کنیم، چک‌لیست امنیت).

تقسیم کار روزانه و هماهنگی:
- هر روز ۱۵ دقیقه Standup کوتاه برای همگام‌سازی.
- هر PR باید حداقل یک review توسط نفر دیگر داشته باشد.
- کارهای بحرانی (Transaction, Secrets) با pair-programming یا دو بررسی جداگانه قبل merge.

گام‌های دقیق «همین الآن» (next immediate tasks):
1. بازتولید و چرخش توکن ربات و کلید API (Dev B مسئول اجرا؛ Dev A آماده‌سازی branch و اسکریپت حذف از Git history).
2. ایجاد branch `hotfix/secrets-rotate` و push template کانفیگ که از Env استفاده کند (Dev A).
3. قفل موقت CORS و غیرفعال‌سازی TLS-bypass در branch محلی برای تست (Dev B).
4. برنامه‌ریزی Sprint 1 و هماهنگی بردهای تست روی staging.

ملاحظات تخصیص زمان و ریسک:
- بخش مالی یکپارچگی بالا و نیاز به تست دقیق دارد — تخصیص 60% زمان Sprint1 به Dev A.
- اصلاح DI و matching engine نیاز به کدخوانی و بازبینی عمیق دارد — 40% زمان Sprint1 به Dev B.

نتیجه‌گیری:
این طرح یک مسیر واقع‌گرایانه 6 هفته‌ای (تقریبی) ارائه می‌دهد تا سرویس‌ها را به وضعیت امن و قابل تولید نزدیک کند و هم‌زمان مقدمات تبدیل قابلیت‌های ربات به وب‌اپ را فراهم آورد. توصیه می‌کنم ابتدا کارهای فوری (چرخش اسرار و TLS/CORS) را انجام دهید، سپس وارد Sprint 1 شوید.
