# TallaEgg Trading Bot - Clean Architecture

ربات تلگرام برای معاملات ارز دیجیتال و طلا با سیستم affiliate marketing - ساختار Clean Architecture

## ساختار Clean Architecture

پروژه به صورت Clean Architecture کامل طراحی شده است:

### 🏗️ لایه‌های معماری

#### 1. **Core Layer** (لایه هسته)
- **مدل‌ها**: موجودیت‌های اصلی سیستم
- **Interface ها**: قراردادهای سرویس‌ها و repository ها
- **Enum ها**: مقادیر ثابت سیستم

#### 2. **Application Layer** (لایه کاربرد)
- **سرویس‌ها**: منطق کسب‌وکار
- **Command/Query**: الگوی CQRS
- **Validation**: اعتبارسنجی داده‌ها

#### 3. **Infrastructure Layer** (لایه زیرساخت)
- **Repository ها**: دسترسی به داده
- **API Client ها**: ارتباط با سرویس‌های خارجی
- **Handler ها**: پردازش رویدادها

### 📁 ساختار پروژه

```
TallaEgg/
├── src/
│   ├── Orders.Core/                    # مدل‌ها و interface های سفارش
│   ├── Orders.Application/              # سرویس‌های سفارش
│   ├── Orders.Infrastructure/          # repository های سفارش
│   ├── TallaEgg.Api/                  # API اصلی
│   └── [سایر سرویس‌ها...]
├── TelegramBot/
│   ├── TallaEgg.TelegramBot.Core/     # مدل‌ها و interface های ربات
│   ├── TallaEgg.TelegramBot.Infrastructure/ # پیاده‌سازی ربات (پروژهٔ قابل اجرا)
│   └── TallaEgg.TelegramBot.Simulator/ # اجرای همان handlerها بدون تلگرام
└── tests/
```

## 🚀 نحوه اجرا

### 1. راه‌اندازی دیتابیس‌ها
```bash
# Orders Database
dotnet ef database update --project src/TallaEgg.Api
```

### 2. اجرای API ها
```bash
# Orders API (Port 5135)
cd src/TallaEgg.Api
dotnet run
```

### 3. اجرای ربات تلگرام
```bash
cd TelegramBot/TallaEgg.TelegramBot.Infrastructure
dotnet run
```

## ⚙️ تنظیمات

### فایل appsettings.json ربات
```json
{
  "TelegramBotToken": "YOUR_BOT_TOKEN",
  "UsersApiUrl": "http://localhost:5136/api",
  "PricesApiUrl": "http://localhost:5135/api",
  "OrderApiUrl": "http://localhost:5135/api"
}
```

## 🔧 مزایای Clean Architecture

### 1. **جداسازی مسئولیت‌ها**
- هر لایه مسئولیت خاص خود را دارد
- وابستگی‌ها فقط به سمت داخل هستند

### 2. **قابلیت تست**
- سرویس‌ها به راحتی قابل mock کردن هستند
- تست‌های unit و integration جداگانه

### 3. **انعطاف‌پذیری**
- تغییر تکنولوژی بدون تأثیر بر منطق کسب‌وکار
- جایگزینی آسان سرویس‌ها

### 4. **نگهداری آسان**
- کد تمیز و قابل فهم
- ساختار منظم و استاندارد

## 📋 جریان کار کاربر

1. **شروع**: `/start [کد_دعوت]`
2. **تایید کد**: سیستم کد دعوت را بررسی می‌کند
3. **ثبت‌نام**: کاربر در سیستم ثبت‌نام می‌شود
4. **شماره تلفن**: کاربر شماره تلفن خود را به اشتراک می‌گذارد
5. **منوی اصلی**: کاربر به منوی اصلی دسترسی پیدا می‌کند
6. **معاملات**: کاربر می‌تواند قیمت‌ها را ببیند و معامله کند

## 🛠️ توسعه

### اضافه کردن سرویس جدید
1. مدل‌ها را در Core layer تعریف کنید
2. Interface ها را در Core layer ایجاد کنید
3. سرویس‌ها را در Application layer پیاده‌سازی کنید
4. Repository ها را در Infrastructure layer پیاده‌سازی کنید

### تست‌نویسی
```bash
# اجرای تست‌ها
dotnet test

# تست‌های خاص
dotnet test --filter "Category=Unit"
```

## 📚 منابع

- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Dependency Injection in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)

## 🤝 مشارکت

1. Fork کنید
2. Feature branch ایجاد کنید (`git checkout -b feature/AmazingFeature`)
3. Commit کنید (`git commit -m 'Add some AmazingFeature'`)
4. Push کنید (`git push origin feature/AmazingFeature`)
5. Pull Request ایجاد کنید

## 📄 لایسنس

این پروژه تحت لایسنس MIT منتشر شده است. 