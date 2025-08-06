# TallaEgg Trading Bot

ربات تلگرام برای معاملات ارز دیجیتال و طلا با سیستم affiliate marketing

## ساختار پروژه

پروژه به صورت میکروسرویس‌های جداگانه طراحی شده است:

### 1. Users Service (مدیریت کاربران)
- **Users.Core**: مدل‌های کاربر و enum ها
- **Users.Infrastructure**: دسترسی به دیتابیس و repository ها
- **Users.Application**: سرویس‌های کاربر
- **Users.Api**: API برای مدیریت کاربران

### 2. Affiliate Service (سیستم دعوت)
- **Affiliate.Core**: مدل‌های دعوت و کدهای تخفیف
- **Affiliate.Infrastructure**: دسترسی به دیتابیس affiliate
- **Affiliate.Application**: سرویس‌های affiliate
- **Affiliate.Api**: API برای مدیریت دعوت‌ها

### 3. Matching Engine Service (موتور تطبیق معاملات)
- **Matching.Core**: مدل‌های سفارش و معامله
- **Matching.Infrastructure**: دسترسی به دیتابیس معاملات
- **Matching.Application**: موتور تطبیق و منطق معاملات
- **Matching.Api**: API برای مدیریت سفارشات و معاملات

### 4. Wallet Service (کیف پول)
- **Wallet.Core**: مدل‌های کیف پول و تراکنش
- **Wallet.Infrastructure**: دسترسی به دیتابیس کیف پول
- **Wallet.Application**: سرویس‌های کیف پول
- **Wallet.Api**: API برای مدیریت کیف پول

### 5. Orders Service (سفارشات)
- **Orders.Core**: مدل‌های سفارش
- **Orders.Infrastructure**: دسترسی به دیتابیس سفارشات
- **Orders.Application**: سرویس‌های سفارش
- **TallaEgg.Api**: API اصلی برای سفارشات

### 6. Telegram Bot
- **TallaEgg.TelegramBot**: ربات تلگرام با رابط کاربری کامل

## ویژگی‌ها

- سیستم دعوت با کدهای منحصر به فرد
- ثبت‌نام کاربران با کد دعوت
- اشتراک‌گذاری شماره تلفن
- مدیریت وضعیت کاربران
- مدیریت نقش‌ها (ادمین، کاربر)
- ثبت سفارش خرید و فروش توسط ادمین
- معاملات نقدی و آتی
- مشاهده موجودی و تاریخچه
- راهنمای استفاده

### احراز هویت و ثبت‌نام
- ✅ سیستم دعوت با کدهای منحصر به فرد
- ✅ ثبت‌نام کاربران با کد دعوت
- ✅ اشتراک‌گذاری شماره تلفن
- ✅ مدیریت وضعیت کاربران

### منوی اصلی
- 💰 **نقدی**: معاملات نقدی و فوری
- 📈 **آتی**: معاملات آتی و قراردادهای آتی
- 📊 **حسابداری**: مشاهده موجودی و تاریخچه
- ❓ **راهنما**: راهنمای استفاده

### معاملات آتی
- 📈 نمایش آخرین قیمت‌های خرید و فروش
- 🛒 دکمه‌های خرید و فروش
- 🔙 بازگشت به منوی اصلی

### موتور تطبیق معاملات
- ⚡ تطبیق خودکار سفارشات خرید و فروش
- 📊 مدیریت Order Book
- 💰 محاسبه کارمزد معاملات
- 🔄 به‌روزرسانی موجودی کیف پول

### کیف پول
- 💳 مدیریت موجودی‌های مختلف
- 📝 ثبت تراکنش‌ها
- 🔒 قفل موجودی برای سفارشات
- 💸 واریز و برداشت

## نحوه اجرا

### 1. راه‌اندازی دیتابیس‌ها
```bash
# Users Database
dotnet ef database update --project src/Users.Api

# Affiliate Database  
dotnet ef database update --project src/Affiliate.Api

# Matching Database
dotnet ef database update --project src/Matching.Api

# Wallet Database
dotnet ef database update --project src/Wallet.Api

# Orders Database
dotnet ef database update --project src/TallaEgg.Api
```

### 2. اجرای API ها
```bash
# Users API (Port 5136)
cd src/Users.Api
dotnet run

# Affiliate API (Port 5137)
cd src/Affiliate.Api
dotnet run

# Matching API (Port 5138)
cd src/Matching.Api
dotnet run

# Wallet API (Port 5139)
cd src/Wallet.Api
dotnet run

# Orders API (Port 5135)
cd src/TallaEgg.Api
dotnet run
```

### 3. اجرای ربات تلگرام
```bash
cd TelegramBot/TallaEgg.TelegramBot
dotnet run
```

## تنظیمات

### فایل appsettings.json ربات
```json
{
  "TelegramBotToken": "YOUR_BOT_TOKEN",
  "OrderApiUrl": "http://localhost:5135/api/order",
  "UsersApiUrl": "http://localhost:5136/api",
  "AffiliateApiUrl": "http://localhost:5137/api",
  "MatchingApiUrl": "http://localhost:5138/api",
  "WalletApiUrl": "http://localhost:5139/api",
  "PricesApiUrl": "http://localhost:5135/api"
}
```

### فایل‌های appsettings.json API ها
```json
{
  "ConnectionStrings": {
    "UsersDb": "Server=localhost;Database=TallaEggUsers;...",
    "AffiliateDb": "Server=localhost;Database=TallaEggAffiliate;...",
    "MatchingDb": "Server=localhost;Database=TallaEggMatching;...",
    "WalletDb": "Server=localhost;Database=TallaEggWallet;...",
    "OrdersDb": "Server=localhost;Database=TallaEggOrders;..."
  }
}
```

## جریان کار کاربر

1. **شروع**: کاربر با `/start [کد_دعوت]` ربات را شروع می‌کند
2. **تایید کد**: سیستم کد دعوت را بررسی می‌کند
3. **ثبت‌نام**: کاربر در سیستم ثبت‌نام می‌شود
4. **شماره تلفن**: کاربر شماره تلفن خود را به اشتراک می‌گذارد
5. **منوی اصلی**: کاربر به منوی اصلی دسترسی پیدا می‌کند
6. **معاملات**: کاربر می‌تواند قیمت‌ها را ببیند و معامله کند
7. **ادمین**: ادمین می‌تواند سفارش خرید و فروش ثبت کند

## مزایای ساختار جدید

- 🔄 **جداسازی مسئولیت‌ها**: هر سرویس مسئولیت خاص خود را دارد
- 📈 **مقیاس‌پذیری**: هر بخش می‌تواند مستقل توسعه یابد
- 🛠️ **نگهداری آسان**: کد تمیزتر و قابل فهم‌تر
- 🔒 **امنیت بهتر**: دسترسی‌های مختلف برای بخش‌های مختلف
- 🚀 **توسعه سریع‌تر**: تیم‌های مختلف می‌توانند همزمان کار کنند