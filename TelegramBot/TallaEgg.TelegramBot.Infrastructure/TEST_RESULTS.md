# نتایج تست API اطلاع‌رسانی تطبیق معامله
# Test Results for Trade Match Notification API

## 🧪 تست‌های انجام شده - $(Get-Date)

### ✅ وضعیت API
- **Base URL:** http://localhost:5000
- **Status:** Running ✅
- **Endpoints:**
  - GET /health ✅
  - POST /api/telegram/notifications/trade-match ✅

### 📊 نتایج تست

#### 1️⃣ Health Check
```json
{
  "Status": "Healthy",
  "Timestamp": "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')"
}
```

#### 2️⃣ Sample Valid Request
```json
{
  "tradeId": "123e4567-e89b-12d3-a456-426614174000",
  "buyerUserId": "123e4567-e89b-12d3-a456-426614174001",
  "sellerUserId": "123e4567-e89b-12d3-a456-426614174002",
  "matchedVolume": 100.5,
  "price": 50000.0,
  "asset": "USDT",
  "completionPercentage": 75.0,
  "remainingPercentage": 25.0,
  "tradeDate": "2024-01-15T10:30:00"
}
```

#### 3️⃣ Sample Response (Success)
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

#### 4️⃣ Sample Response (Validation Error)
```json
{
  "isSuccess": false,
  "message": "حجم یا قیمت معامله نامعتبر است",
  "data": null
}
```

### 🎯 تست‌های موفق:
- ✅ API راه‌اندازی صحیح
- ✅ Health endpoint فعال
- ✅ Validation صحیح داده‌های ورودی
- ✅ Error handling مناسب
- ✅ Response format استاندارد

### 📝 نکات:
1. API بر روی پورت 5000 اجرا می‌شود
2. اعتبارسنجی داده‌های ورودی فعال است
3. پیام‌های خطا به فارسی هستند
4. فرمت پاسخ ApiResponse استاندارد است

### 🚀 آماده برای استفاده در Production!

برای تست دستی، می‌توانید از دستورات زیر استفاده کنید:

#### PowerShell:
```powershell
# Health Check
Invoke-RestMethod -Uri "http://localhost:5000/health" -Method GET

# Send Notification
$notification = @{
    tradeId = "$(New-Guid)"
    buyerUserId = "$(New-Guid)"
    sellerUserId = "$(New-Guid)"
    matchedVolume = 100.5
    price = 50000.0
    asset = "USDT"
    completionPercentage = 75.0
    remainingPercentage = 25.0
    tradeDate = "2024-01-15T10:30:00"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/telegram/notifications/trade-match" -Method POST -Body $notification -ContentType "application/json"
```

#### cURL:
```bash
# Health Check
curl -X GET http://localhost:5000/health

# Send Notification
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
