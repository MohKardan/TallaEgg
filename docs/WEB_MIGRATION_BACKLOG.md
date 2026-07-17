بکلاگ مهاجرت قابلیت‌های ربات به وب‌اپ و بازآرایی برای تولید

اولویت‌بندی کلی (فازها):
1) فوری — امنیت و حفاظت از سرمایه (Quick Wins, 1-3 روز)
   - چرخش اسرار: حذف کلیدهای hardcode، انتقال به Env/Secret Manager، بازتولید توکن تلگرام.
   - غیرفعال‌سازی TLS-Bypass در کد و Gate کردن آن فقط به Development.
   - محدودسازی CORS به لیست Originهای مورد نیاز.
   - قرنطینه کردن Endpointهای stub (برگرداندن ۵۰۱/۴۰۳ یا غیر فعال کردن در Prod).

2) بحرانی — یکپارچگی مالی (فاز ۱, 2–3 هفته)
   - تبدیل عملیات‌های چندمرحله‌ای به یک Transaction واحد (ApplyTradeAsync).
   - افزودن Optimistic Concurrency (`RowVersion`) به `WalletEntity` و `Order`.
   - اصلاح ترتیب: قفل موجودی قبل از ثبت/ارسال سفارش به موتور تطبیق.
   - اضافه کردن Idempotency برای تراکنش‌ها (ReferenceId).

3) معماری و تزریق وابستگی (فاز ۲, 1–2 هفته)
   - جداسازی اینترفیس‌ها در Core/Application و پیاده‌سازی در Infrastructure (DIP).
   - اصلاح ثبت `MatchingEngineService` به Singleton/HostedService هماهنگ.
   - Typed HttpClient و حذف `new HttpClient()`های دستی.

4) کیفیت داده و EF Core (فاز ۲, 1 هفته)
   - استفاده از `AsNoTracking` برای خواندن‌های سنگین.
   - منتقل کردن فیلترینگ به DB (LINQ-to-Entities)، حذف فیلتر در حافظه.
   - برداشتن اجرای Migration در Startup و تبدیل آن به pipeline جداگانه.

5) API، اعتبارسنجی و لاگینگ (فاز 3, 1–2 هفته)
   - افزودن Middleware سراسری خطا + `ProblemDetails`.
   - یکدست‌سازی قالب پاسخ و کدهای HTTP.
   - FluentValidation و Value Objects (`Money`, `Symbol`).
   - پاک‌سازی لاگ‌های شکسته و استفاده همگن از `ILogger<T>` و Serilog sinks.

6) تست، استقرار و پاکسازی (فاز 3, 1–2 هفته)
   - پوشش تست واحد و یکپارچه برای flows مالی.
   - تعریف Runbook استقرار و Playbook برای incident مالی.
   - حذف کد مرده و پوشه‌های تکراری از مخزن.

هر آیتم بالا شامل تخمین نسبی و خروجی مورد انتظار است؛ در فایل طرح برنامه (PLAN) تقسیم‌بندی زمان‌بندی و تخصیص‌ها آمده است.
