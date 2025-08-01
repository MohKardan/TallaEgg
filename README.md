# TallaEgg

پروژه **TallaEgg** یک سیستم مدیریت سفارشات با API و ربات تلگرام است که امکان ثبت و مشاهده سفارش دارایی‌ها (مانند طلا) را فراهم می‌کند.

## ساختار پروژه

```
TallaEgg/
├── src/
│   ├── TallaEgg.Api/                # وب‌سرویس API برای مدیریت سفارشات
│   ├── Orders.Core/                 # مدل دامنه سفارشات
│   ├── Orders.Application/          # منطق کاربردی سفارشات
│   ├── Orders.Infrastructure/       # زیرساخت و ارتباط با دیتابیس سفارشات
│   ├── Rates.Core/                  # (رزرو برای نرخ‌ها)
│   ├── Rates.Application/           # (رزرو برای منطق نرخ‌ها)
│   ├── Rates.Infrastructure/        # (رزرو برای زیرساخت نرخ‌ها)
│   └── BuildingBlocks.Core/         # اجزای مشترک و زیرساختی
├── TelegramBot/
│   └── TallaEgg.TelegramBot/        # ربات تلگرام برای ثبت سفارش
├── Orders.Tests/                    # تست‌های واحد سفارشات
├── tests/                           # (رزرو برای تست‌های دیگر)
└── TallaEgg.sln                     # فایل Solution
```

## اجزای اصلی

### 1. API (src/TallaEgg.Api)
- ارائه دو endpoint:
  - `POST /api/order` ثبت سفارش جدید
  - `GET /api/orders/{asset}` دریافت لیست سفارشات یک دارایی
- استفاده از Entity Framework و SQL Server برای ذخیره داده‌ها

### 2. لایه سفارشات (Orders)
- **Orders.Core**: مدل سفارش شامل فیلدهایی مانند Asset، Amount، Price، UserId و ...
- **Orders.Application**: هندلرها و منطق ثبت سفارش
- **Orders.Infrastructure**: ریپازیتوری و ارتباط با دیتابیس

### 3. ربات تلگرام (TelegramBot/TallaEgg.TelegramBot)
- دریافت دستور `/buy [Asset] [Amount] [Price]` از کاربر
- ارسال سفارش به API و نمایش نتیجه به کاربر
- ارتباط با API از طریق کلاس `OrderApiClient`

### 4. تست‌ها
- تست‌های واحد برای منطق سفارشات در پوشه `Orders.Tests`

---

## راهنمای توسعه

### پیش‌نیازها

- .NET 6 یا بالاتر
- SQL Server (یا LocalDB)
- توکن ربات تلگرام

### راه‌اندازی دیتابیس

۱. یک دیتابیس جدید با نام `TallaEggOrders` در SQL Server ایجاد کنید.
۲. Connection string را در فایل `appsettings.json` پروژه `TallaEgg.Api` تنظیم کنید.
۳. مهاجرت‌های EF را اجرا کنید (در صورت وجود):

```bash
cd src/TallaEgg.Api
dotnet ef database update
```

### اجرای پروژه

#### اجرای API

```bash
cd src/TallaEgg.Api
dotnet run
```

#### اجرای ربات تلگرام

```bash
cd TelegramBot/TallaEgg.TelegramBot
# مقداردهی TelegramBotToken و OrderApiUrl در appsettings.json
dotnet run
```

---

## تست

برای اجرای تست‌های واحد:

```bash
cd Orders.Tests
dotnet test
```

در صورت نیاز به افزودن تست‌های جدید، فایل‌های تست را در همین پوشه اضافه کنید.

---

## معماری پروژه

- **معماری لایه‌ای**: پروژه به صورت لایه‌ای (Core, Application, Infrastructure) پیاده‌سازی شده تا توسعه، تست و نگهداری آسان‌تر باشد.
- **ارتباط ربات و API**: ربات تلگرام از طریق HTTP به API متصل می‌شود و سفارشات را ثبت می‌کند.
- **قابلیت توسعه**: بخش‌های Rates و BuildingBlocks برای توسعه‌های آتی رزرو شده‌اند.

---

## مشارکت

در صورت تمایل به مشارکت:
- یک Fork از پروژه ایجاد کنید.
- تغییرات خود را در یک Branch جدید اعمال کنید.
- Pull Request ارسال کنید.

---

## لایسنس

[نوع لایسنس پروژه را اینجا بنویسید، مثلا MIT]

---

## توسعه‌دهندگان

- [نام خود را اینجا وارد کنید] 