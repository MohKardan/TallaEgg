# 💳 TallaEgg Wallet API Documentation

## 📋 **Overview**
سرویس مدیریت کیف پول TallaEgg شامل عملیات واریز، برداشت، قفل/آزادسازی موجودی و مدیریت تراکنش‌ها

**Base URL:** `http://localhost:60933`  
**API Documentation:** `http://localhost:60933/api-docs`

---

## 🔗 **Endpoints**

### 1️⃣ **دریافت موجودی یک ارز**
```yaml
GET /api/wallet/balance/{userId}/{asset}
```

**Parameters:**
- `userId` (path, required): شناسه کاربر (UUID)
- `asset` (path, required): نماد ارز (مثل BTC, ETH, USDT)

**Response 200:**
```json
{
  "success": true,
  "message": "",
  "data": {
    "asset": "BTC",
    "balance": 1.5,
    "lockedBalance": 0.2,
    "updatedAt": "2025-08-28T12:30:00Z"
  }
}
```

**Response 400:**
```json
{
  "success": false,
  "message": "کیف پول پیدا نشد",
  "data": null
}
```

---

### 2️⃣ **دریافت تمام موجودی‌های کاربر**
```yaml
GET /api/wallet/balances/{userId}
```

**Parameters:**
- `userId` (path, required): شناسه کاربر (UUID)

**Response 200:**
```json
{
  "success": true,
  "message": "لیست کیف پول های کاربر",
  "data": [
    {
      "asset": "BTC",
      "balance": 1.5,
      "lockedBalance": 0.2,
      "updatedAt": "2025-08-28T12:30:00Z"
    },
    {
      "asset": "ETH",
      "balance": 10.0,
      "lockedBalance": 1.0,
      "updatedAt": "2025-08-28T12:25:00Z"
    }
  ]
}
```

---

### 3️⃣ **واریز به کیف پول**
```yaml
POST /api/wallet/deposit
```

**Request Body:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "asset": "BTC",
  "amount": 0.5,
  "referenceId": "deposit_12345"
}
```

**Response 200:**
```json
{
  "success": true,
  "message": "عملیات با موفقیت انجام شد",
  "data": {
    "asset": "BTC",
    "balanceBefore": 1.0,
    "balanceAfter": 1.5,
    "lockedBalance": 0.2,
    "updatedAt": "2025-08-28T12:30:00Z",
    "trackingCode": "ABCD1234EFGH"
  }
}
```

**Response 400:**
```json
{
  "success": false,
  "message": "مقدار باید بزرگتر از صفر باشد",
  "data": null
}
```

---

### 4️⃣ **قفل کردن موجودی**
```yaml
POST /api/wallet/lockBalance
```

**Request Body:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "asset": "BTC",
  "amount": 0.1
}
```

**Response 200:**
```json
{
  "success": true,
  "message": "عملیات با موفقیت انجام شد",
  "data": {
    "asset": "BTC",
    "balance": 1.4,
    "lockedBalance": 0.3,
    "updatedAt": "2025-08-28T12:30:00Z"
  }
}
```

---

### 5️⃣ **آزادسازی موجودی قفل شده**
```yaml
POST /api/wallet/unlockBalance
```

**Request Body:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "asset": "BTC",
  "amount": 0.1
}
```

**Response 200:**
```json
{
  "success": true,
  "message": "عملیات با موفقیت انجام شد",
  "data": true
}
```

**Response 400:**
```json
{
  "success": false,
  "message": "موجودی قفل شده کافی نیست",
  "data": false
}
```

---

### 6️⃣ **دریافت تاریخچه تراکنش‌ها**
```yaml
GET /api/wallet/transactions/{userId}?asset={asset}
```

**Parameters:**
- `userId` (path, required): شناسه کاربر (UUID)
- `asset` (query, optional): فیلتر بر اساس ارز

**Response 200:**
```json
[
  {
    "id": "trans_123",
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "asset": "BTC",
    "amount": 0.5,
    "type": 0,
    "status": 1,
    "referenceId": "deposit_12345",
    "description": "واریز BTC",
    "createdAt": "2025-08-28T12:30:00Z",
    "completedAt": "2025-08-28T12:30:05Z"
  }
]
```

---

### 7️⃣ **اعتبارسنجی موجودی برای سفارش بازار**
```yaml
POST /api/wallet/market/validate-balance
```

**Request Body:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "asset": "BTC",
  "amount": 0.1,
  "orderType": 1
}
```

**Parameters:**
- `orderType`: 0 = Buy, 1 = Sell

**Response 200:**
```json
{
  "success": true,
  "message": "موجودی کافی است",
  "hasSufficientBalance": true
}
```

**Response 200 (موجودی ناکافی):**
```json
{
  "success": true,
  "message": "موجودی ناکافی. موجودی فعلی: 0.05، مقدار مورد نیاز: 0.1",
  "hasSufficientBalance": false
}
```

---

### 8️⃣ **به‌روزرسانی موجودی پس از سفارش بازار**
```yaml
POST /api/wallet/market/update-balance
```

**Request Body:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "asset": "BTC",
  "amount": 0.1,
  "orderType": 1,
  "orderId": "order_789"
}
```

**Response 200:**
```json
{
  "success": true,
  "message": "موجودی با موفقیت به‌روزرسانی شد"
}
```

---

## 📊 **Data Models**

### **WalletDTO**
```json
{
  "asset": "string",          // نماد ارز
  "balance": 0.0,            // موجودی آزاد
  "lockedBalance": 0.0,      // موجودی قفل شده
  "updatedAt": "datetime"    // آخرین به‌روزرسانی
}
```

### **WalletDepositDTO**
```json
{
  "asset": "string",
  "balanceBefore": 0.0,      // موجودی قبل از واریز
  "balanceAfter": 0.0,       // موجودی بعد از واریز
  "lockedBalance": 0.0,
  "updatedAt": "datetime",
  "trackingCode": "string"   // کد پیگیری
}
```

### **BaseWalletRequest**
```json
{
  "userId": "uuid",          // شناسه کاربر
  "asset": "string",         // نماد ارز
  "amount": 0.0             // مقدار
}
```

### **DepositRequest**
```json
{
  "userId": "uuid",
  "asset": "string",
  "amount": 0.0,
  "referenceId": "string"    // کد مرجع (اختیاری)
}
```

---

## 🔢 **Enums**

### **TransactionType**
- `0` - Deposit (واریز)
- `1` - Withdraw (برداشت)
- `2` - Trade (معامله)
- `3` - Freeze (فریز)
- `4` - Unfreeze (آزادسازی)
- `5` - Fee (کارمزد)
- `6` - Transfer (انتقال)
- `7` - Adjustment (تعدیل)

### **TransactionStatus**
- `0` - Pending (در انتظار)
- `1` - Completed (تکمیل شده)
- `2` - Failed (ناموفق)
- `3` - Canceled (لغو شده)

---

## 🚨 **Error Codes**

| کد | پیام | توضیح |
|---|---|---|
| 400 | مقدار باید بزرگتر از صفر باشد | مقدار نامعتبر |
| 400 | کیف پول پیدا نشد | کیف پول وجود ندارد |
| 400 | موجودی کافی نیست | موجودی ناکافی برای عملیات |
| 400 | نوع سفارش نامعتبر است | orderType نامعتبر |
| 500 | خطای داخلی سرور | خطای غیرمنتظره |

---

## 🧪 **Test Examples**

### **Postman Collection:**
```json
{
  "info": {
    "name": "TallaEgg Wallet API",
    "description": "مجموعه تست‌های API کیف پول"
  },
  "item": [
    {
      "name": "Get Balance",
      "request": {
        "method": "GET",
        "url": "{{base_url}}/api/wallet/balance/{{userId}}/BTC"
      }
    },
    {
      "name": "Deposit",
      "request": {
        "method": "POST",
        "url": "{{base_url}}/api/wallet/deposit",
        "body": {
          "userId": "{{userId}}",
          "asset": "BTC",
          "amount": 0.5,
          "referenceId": "test_deposit"
        }
      }
    }
  ]
}
```

### **cURL Examples:**
```bash
# دریافت موجودی
curl -X GET "http://localhost:60933/api/wallet/balance/{userId}/BTC"

# واریز
curl -X POST "http://localhost:60933/api/wallet/deposit" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "asset": "BTC",
    "amount": 0.5
  }'
```

---

## 📝 **Notes**

1. **CORS:** تمام origins مجاز هستند
2. **Authentication:** در حال حاضر پیاده‌سازی نشده
3. **Rate Limiting:** محدودیت ندارد
4. **Database:** SQL Server با Entity Framework
5. **Tracking Codes:** برای هر تراکنش کد یکتا تولید می‌شود

---

## 🔄 **Future Features (Commented Out)**

- برداشت از کیف پول
- انتقال بین کاربران  
- شارژ کیف پول
- عملیات Credit/Debit داخلی

---

**📅 Last Updated:** August 28, 2025  
**👨‍💻 Version:** 1.0.0
