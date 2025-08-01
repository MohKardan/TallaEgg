# راهنمای تنظیم ربات تلگرام

## 🚀 مراحل راه‌اندازی

### 1. ایجاد ربات در تلگرام

1. **شروع چت با @BotFather**:
   - در تلگرام با @BotFather چت کنید
   - دستور `/newbot` را ارسال کنید

2. **تنظیم نام ربات**:
   ```
   Please choose a name for your bot:
   TallaEgg Trading Bot
   ```

3. **تنظیم username ربات**:
   ```
   Please choose a username for your bot:
   tallaegg_trading_bot
   ```

4. **دریافت Token**:
   ```
   Use this token to access the HTTP API:
   1234567890:ABCdefGHIjklMNOpqrsTUVwxyz
   ```

### 2. تنظیم فایل appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "TelegramBotToken": "YOUR_ACTUAL_BOT_TOKEN_HERE",
  "UsersApiUrl": "http://localhost:5136/api",
  "PricesApiUrl": "http://localhost:5135/api",
  "OrderApiUrl": "http://localhost:5135/api"
}
```

### 3. اجرای ربات

```bash
# اجرای ربات
cd TelegramBot/TallaEgg.TelegramBot.Infrastructure
dotnet run
```

## 🔧 تست ربات

### دستورات اصلی:
- `/start [کد_دعوت]` - شروع و ثبت‌نام
- `/menu` - نمایش منوی اصلی

### منوهای ربات:
- 💰 **نقدی**: معاملات نقدی طلا و الماس
- 📈 **آتی**: معاملات آتی طلا و الماس
- 📊 **حسابداری**: موجودی و تاریخچه
- ❓ **راهنما**: راهنمای استفاده

## 🛠️ عیب‌یابی

### مشکل 1: "TelegramBotToken is not configured"
**راه‌حل**: Token معتبر را در `appsettings.json` تنظیم کنید

### مشکل 2: "404 Not Found"
**راه‌حل**: Token را بررسی کنید و مطمئن شوید که درست کپی شده است

### مشکل 3: "Unauthorized"
**راه‌حل**: Token را دوباره از @BotFather دریافت کنید

### مشکل 4: ربات پیام نمی‌فرستد
**راه‌حل**: 
1. مطمئن شوید که ربات فعال است
2. با ربات چت کنید تا شروع شود
3. دستور `/start` را ارسال کنید

## 📋 ویژگی‌های ربات

### ✅ پیاده‌سازی شده:
- ثبت‌نام کاربر با کد دعوت
- تایید شماره تلفن
- نمایش منوهای مختلف
- نمایش قیمت‌ها
- ایجاد سفارش

### 🔄 در حال توسعه:
- اتصال به API های واقعی
- پردازش پرداخت
- گزارش‌گیری
- مدیریت affiliate

## 🔒 امنیت

### نکات مهم:
1. **Token را محفوظ نگه دارید**
2. **Token را در کد قرار ندهید**
3. **از Environment Variables استفاده کنید**
4. **Token را در Git commit نکنید**

### تنظیم Environment Variable:
```bash
# Windows
setx TELEGRAM_BOT_TOKEN "your_token_here"

# Linux/Mac
export TELEGRAM_BOT_TOKEN="your_token_here"
```

## 📞 پشتیبانی

در صورت بروز مشکل:
1. لاگ‌های برنامه را بررسی کنید
2. Token را دوباره بررسی کنید
3. با ادمین تماس بگیرید

## 🎯 مراحل بعدی

1. **اتصال به API های واقعی**
2. **پیاده‌سازی سیستم پرداخت**
3. **اضافه کردن گزارش‌گیری**
4. **بهینه‌سازی عملکرد**
5. **اضافه کردن تست‌ها** 