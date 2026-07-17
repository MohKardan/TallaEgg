خلاصه گزارش ممیزی — TallaEgg

امتیاز کلی: 4.6/10
آمادگی تولید: 30%
تاریخ گزارش: 2026-07-08

یافته‌های بحرانی (Blocker تولید):
- C-1: کلید API hardcode و کامیت‌شده در سورس. نیاز به چرخش فوری و انتقال به Secret Manager/Env.
- C-2: توکن تلگرام و ConnectionStringها در کانفیگ کامیت‌شده. باید باطل و بازتولید شوند.
- C-3: نبود Transaction واحد روی عملیات مالی چندمرحله‌ای در `WalletService.ApplyTradeAsync`.
- C-4: نبود کنترل همزمانی خوش‌بینانه (RowVersion) روی کیف پول‌ها و سفارش‌ها.
- C-5: ترتیب نادرست قفل موجودی و ارسال سفارش به موتور تطبیق (Race condition).
- C-6: ثبت نادرست `MatchingEngineService` باعث دو نمونه و عدم همگام‌سازی می‌شود.
- C-7: غیرفعال بودن TLS validation در HttpClient (ServerCertificateCustomValidationCallback=true).
- C-8: Endpointهای stub/مسترج زنده (مثلاً `MakeTradeAsync`) که نتیجهٔ جعلی برمی‌گردانند.
- C-9: CORS کاملاً باز (AllowAnyOrigin).

نتیجه‌گیری کوتاه:
- ساختار کلی معماری درست و نقاط قوتی دارد، اما لایهٔ مالی و امنیتی فعلاً برای پول واقعی ناامن است.
- اقدام فوری: چرخش اسرار، بستن CORS، فعال‌سازی اعتبارسنجی TLS در محیط‌های غیرتوسعه، و قرنطینهٔ Endpointهای stub.

فایل کامل گزارش ممیزی در `audit/CODE_AUDIT_REPORT.html` موجود است و این خلاصه برگرفته از آن است.
