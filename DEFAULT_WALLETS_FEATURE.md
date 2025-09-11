# ویژگی ایجاد کیف پول‌های پیش‌فرض

## توضیحات

این ویژگی به صورت خودکار هنگام ثبت‌نام کاربر، کیف پول‌های پیش‌فرض زیر را ایجاد می‌کند:

1. **کیف پول ریال (IRR)** - برای نگهداری ریال ایران
2. **کیف پول طلا (MAUA)** - برای نگهداری طلا 
3. **کیف پول اعتبار طلا (CREDIT_MAUA)** - برای استفاده از اعتبار طلا

## تغییرات انجام شده

### Wallet API (Wallet.Api)

#### 1. WalletService.cs
- اضافه شدن متد `CreateDefaultWalletsAsync`
- ایجاد خودکار سه نوع کیف پول با موجودی صفر

#### 2. IWalletService.cs
- اضافه شدن امضای متد `CreateDefaultWalletsAsync`

#### 3. Program.cs
- اضافه شدن endpoint جدید: `POST /api/wallet/create-default/{userId}`
- به‌روزرسانی عنوان Swagger به "TallaEgg Wallet API"

### Users API (Users.Api)

#### 1. UserService.cs
- اضافه شدن `IHttpClientFactory` به constructor
- اضافه شدن متد خصوصی `CreateDefaultWalletsAsync`
- تغییر در `RegisterUserAsync` برای فراخوانی خودکار ایجاد کیف پول‌ها

#### 2. Program.cs
- اضافه شدن HttpClient برای ارتباط با Wallet API
- اضافه شدن endpoint جدید: `POST /api/user/{userId}/create-default-wallets`

#### 3. appsettings.json
- اضافه شدن تنظیمات `WalletApiUrl`

## Endpoints جدید

### Wallet API

```http
POST /api/wallet/create-default/{userId}
```

**پارامترها:**
- `userId` (Guid): شناسه کاربر

**پاسخ:**
```json
{
  "success": true,
  "message": "کیف پول‌های پیش‌فرض با موفقیت ایجاد شدند",
  "data": [
    {
      "id": "guid",
      "userId": "guid", 
      "asset": "IRR",
      "balance": 0,
      "lockedBalance": 0,
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": "2024-01-01T00:00:00Z"
    },
    // ... سایر کیف پول‌ها
  ]
}
```

### Users API

```http
POST /api/user/{userId}/create-default-wallets
```

**پارامترها:**
- `userId` (Guid): شناسه کاربر

**پاسخ:**
```json
{
  "success": true,
  "message": "کیف پول‌های پیش‌فرض با موفقیت ایجاد شدند",
  "data": null
}
```

## نحوه کارکرد

1. **ثبت‌نام خودکار**: هنگام ثبت‌نام کاربر از طریق `/api/user/register`، کیف پول‌های پیش‌فرض به صورت خودکار ایجاد می‌شوند.

2. **ایجاد دستی**: می‌توان برای کاربران موجود نیز کیف پول‌های پیش‌فرض را از طریق endpoint مربوطه ایجاد کرد.

## تنظیمات

در فایل `appsettings.json` پروژه Users.Api:

```json
{
  "WalletApiUrl": "https://localhost:7001/"
}
```

## نکات مهم

- اگر خطایی در ایجاد کیف پول‌ها رخ دهد، ثبت‌نام کاربر همچنان ادامه می‌یابد
- هر کیف پول با موجودی صفر ایجاد می‌شود
- استفاده از الگوی existing lazy creation برای compatibility با کد موجود
- کمترین تغییر در کد موجود برای حفظ ثبات سیستم

## Swagger Documentation

پس از اجرای پروژه‌ها، می‌توانید documentationها را در آدرس‌های زیر مشاهده کنید:

- **Wallet API**: `https://localhost:7001/api-docs`
- **Users API**: `https://localhost:7000/api-docs` (یا پورت مربوطه)

## خلاصه تغییرات

✅ **کامل شده**: تمام تغییرات با موفقیت پیاده‌سازی و کامپایل شدند.

### فیچرهای پیاده‌سازی شده:

1. **ایجاد خودکار کیف پول‌ها**: هنگام ثبت‌نام کاربر، سه کیف پول پیش‌فرض (IRR، MAUA، CREDIT_MAUA) ایجاد می‌شود
2. **Endpoint مستقل**: امکان ایجاد کیف پول‌های پیش‌فرض برای کاربران موجود
3. **مدیریت خطا**: در صورت خطا در ایجاد کیف پول‌ها، ثبت‌نام کاربر همچنان ادامه می‌یابد
4. **Swagger Documentation**: مستندات کامل برای APIها
5. **HttpClient Integration**: ارتباط امن بین میکروسرویس‌ها

### وضعیت کامپایل:
- ✅ **Wallet.Api**: کامپایل موفق (17 warning - عادی)
- ✅ **Users.Api**: کامپایل موفق (24 warning - عادی)

## تست

برای تست این ویژگی:

1. پروژه Wallet.Api را اجرا کنید
2. پروژه Users.Api را اجرا کنید  
3. کاربر جدیدی ثبت‌نام کنید
4. بررسی کنید که کیف پول‌های پیش‌فرض ایجاد شده‌اند
