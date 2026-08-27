# 🧪 راهنمای کامل تست سیستم اطلاع‌رسانی تطبیق معامله

این راهنما شامل روش‌های مختلف تست سیستم اطلاع‌رسانی تطبیق معامله می‌باشد.

## 📋 پیش‌نیازهای تست

### 1. اجرای API

**مرحله اول: توقف instance های قبلی (در صورت وجود)**
```bash
taskkill /F /IM "TallaEgg.TelegramBot.Infrastructure.exe" 2>$null
```

**مرحله دوم: اجرای API**
```bash
cd "TelegramBot\TallaEgg.TelegramBot.Infrastructure"
dotnet run -- --api-only
```

**خروجی موفق باید شامل موارد زیر باشد:**
```
🚀 Starting Telegram Notification API only...
🌐 Base URL: http://localhost:5000
📡 Endpoints موجود:
   POST /api/telegram/notifications/trade-match
   GET  /health
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### 2. تنظیمات appsettings.json
مطمئن شوید که فایل `appsettings.json` شامل موارد زیر باشد:
```json
{
  "TelegramBotToken": "YOUR_BOT_TOKEN",
  "UsersApiUrl": "http://localhost:5001",
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## 🚀 روش‌های تست

### 1. تست دستی با PowerShell

#### تست سلامت سرویس:
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/health" -Method GET
```

#### تست اطلاعیه معتبر:
```powershell
$notification = @{
    tradeId = "123e4567-e89b-12d3-a456-426614174000"
    buyerUserId = "123e4567-e89b-12d3-a456-426614174001"
    sellerUserId = "123e4567-e89b-12d3-a456-426614174002"
    matchedVolume = 100.5
    price = 50000.0
    asset = "USDT"
    completionPercentage = 75.0
    remainingPercentage = 25.0
    tradeDate = "2024-01-15T10:30:00"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/telegram/notifications/trade-match" -Method POST -Body $notification -ContentType "application/json"
```

### 2. تست با اسکریپت PowerShell
```bash
PowerShell -ExecutionPolicy Bypass -File "test-api.ps1"
```

### 3. تست با cURL
```bash
# تست سلامت
curl -X GET http://localhost:5000/health

# تست اطلاعیه
curl -X POST http://localhost:5000/api/telegram/notifications/trade-match \
  -H "Content-Type: application/json" \
  -d '{
    "tradeId": "123e4567-e89b-12d3-a456-426614174000",
    "buyerUserId": "123e4567-e89b-12d3-a456-426614174001",
    "sellerUserId": "123e4567-e89b-12d3-a456-426614174002",
    "matchedVolume": 100.5,
    "price": 50000.0,
    "asset": "USDT",
    "completionPercentage": 75.0,
    "remainingPercentage": 25.0,
    "tradeDate": "2024-01-15T10:30:00"
  }'
```

### 4. تست با Postman
1. ایمپورت Collection زیر در Postman:

```json
{
  "info": {
    "name": "Telegram Notification API Tests",
    "description": "Collection for testing trade match notification API"
  },
  "item": [
    {
      "name": "Health Check",
      "request": {
        "method": "GET",
        "header": [],
        "url": {
          "raw": "{{baseUrl}}/health",
          "host": ["{{baseUrl}}"],
          "path": ["health"]
        }
      }
    },
    {
      "name": "Valid Trade Notification",
      "request": {
        "method": "POST",
        "header": [
          {
            "key": "Content-Type",
            "value": "application/json"
          }
        ],
        "body": {
          "mode": "raw",
          "raw": "{\n  \"tradeId\": \"123e4567-e89b-12d3-a456-426614174000\",\n  \"buyerUserId\": \"123e4567-e89b-12d3-a456-426614174001\",\n  \"sellerUserId\": \"123e4567-e89b-12d3-a456-426614174002\",\n  \"matchedVolume\": 100.5,\n  \"price\": 50000.0,\n  \"asset\": \"USDT\",\n  \"completionPercentage\": 75.0,\n  \"remainingPercentage\": 25.0,\n  \"tradeDate\": \"2024-01-15T10:30:00\"\n}"
        },
        "url": {
          "raw": "{{baseUrl}}/api/telegram/notifications/trade-match",
          "host": ["{{baseUrl}}"],
          "path": ["api", "telegram", "notifications", "trade-match"]
        }
      }
    }
  ],
  "variable": [
    {
      "key": "baseUrl",
      "value": "http://localhost:5000"
    }
  ]
}
```

### 5. تست یونیت

تست‌های ربات در `tests/TallaEgg.AllServices.Tests` هستند — تنها پروژهٔ تست solution، که
همهٔ سرویس‌ها را پوشش می‌دهد:

```bash
dotnet test TallaEgg.sln
```

`TradeNotificationServiceTests` که این راهنما قبلاً به آن اشاره می‌کرد، در پروژهٔ
`TelegramBot/TallaEgg.TelegramBot.Tests` بود که هرگز در solution نبود و در #117 حذف شد.

## 📊 سناریوهای تست

### ✅ تست‌های موفق (200 OK)
1. **اطلاعیه کامل و معتبر**
   - همه فیلدها پر شده
   - مقادیر مثبت و معتبر
   - فرمت تاریخ صحیح

2. **تطبیق جزئی**
   - درصد تکمیل کمتر از 100%
   - درصد باقیمانده مثبت

3. **حجم‌های مختلف**
   - حجم‌های کوچک (0.001)
   - حجم‌های بزرگ (1000000)

### ❌ تست‌های ناموفق (400 Bad Request)
1. **داده‌های نامعتبر**
   - حجم صفر یا منفی
   - قیمت صفر یا منفی
   - Asset خالی

2. **شناسه‌های نامعتبر**
   - GUID خالی (00000000-0000-0000-0000-000000000000)
   - فرمت GUID نامعتبر

3. **داده خالی**
   - JSON خالی {}
   - فیلدهای اجباری خالی

## 🔍 نظارت و عیب‌یابی

### Log ها
لاگ‌های API در کنسول نمایش داده می‌شود:
```
info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/api/telegram/notifications/trade-match
```

### خطاهای رایج
1. **"Connection refused"**
   - API اجرا نشده یا پورت اشتباه

2. **"TelegramBotToken در appsettings.json تعریف نشده است"**
   - توکن تلگرام در تنظیمات موجود نیست

3. **"UsersApiUrl در appsettings.json تعریف نشده است"**
   - آدرس API کاربران تعریف نشده

## 📈 گزارش نتایج

### مثال پاسخ موفق:
```json
{
  "isSuccess": true,
  "message": "اطلاعیه تطبیق معامله با موفقیت به هر دو طرف ارسال شد",
  "data": {
    "buyerNotificationSent": true,
    "sellerNotificationSent": true,
    "buyerError": null,
    "sellerError": null,
    "isFullySuccessful": true,
    "isPartiallySuccessful": false
  }
}
```

### مثال پاسخ خطا:
```json
{
  "isSuccess": false,
  "message": "حجم یا قیمت معامله نامعتبر است",
  "data": null
}
```

## 🎯 اهداف تست

- ✅ **عملکرد صحیح**: API باید به درخواست‌های معتبر پاسخ صحیح دهد
- ✅ **اعتبارسنجی**: داده‌های نامعتبر باید رد شوند
- ✅ **مقاوم‌سازی**: خطاها باید به درستی مدیریت شوند
- ✅ **عملکرد**: پاسخ‌دهی در زمان مناسب
- ✅ **امنیت**: ورودی‌های مخرب نباید سیستم را مختل کند

## 🚨 نکات مهم

1. **محیط تست**: از داده‌های تست استفاده کنید، نه داده‌های واقعی
2. **توکن تلگرام**: از توکن تست استفاده کنید
3. **پورت**: مطمئن شوید پورت 5000 آزاد است
4. **وابستگی‌ها**: API کاربران باید در دسترس باشد
5. **شبکه**: اتصال اینترنت برای ارسال پیام تلگرام ضروری است
