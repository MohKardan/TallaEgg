# اسکریپت تست API اطلاع‌رسانی تطبیق معامله
# Test Script for Trade Match Notification API

Write-Host "🧪 شروع تست API اطلاع‌رسانی تطبیق معامله..." -ForegroundColor Green

# تست 1: بررسی سلامت سرویس
Write-Host "`n1️⃣ تست سلامت سرویس..." -ForegroundColor Cyan

try {
    $healthResponse = Invoke-RestMethod -Uri "http://localhost:57546/health" -Method GET
    Write-Host "✅ Health Check موفق: $($healthResponse.Status)" -ForegroundColor Green
    Write-Host "🕐 زمان: $($healthResponse.Timestamp)" -ForegroundColor Yellow
}
catch {
    Write-Host "❌ Health Check ناموفق: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# تست 2: ارسال اطلاعیه تطبیق معامله معتبر
Write-Host "`n2️⃣ تست ارسال اطلاعیه معتبر..." -ForegroundColor Cyan

$validNotification = @{
    tradeId = "123e4567-e89b-12d3-a456-426614174000"
    buyOrderId = "223e4567-e89b-12d3-a456-426614174000"
    buyerUserId = "080A42C8-603F-422D-A02F-142407F3E94C"
    sellOrderId = "323e4567-e89b-12d3-a456-426614174000"
    sellerUserId = "D286F932-D4E9-49EC-8D72-D85EEC4907A7"
    asset = "USDT"
    price = 50000.0
    matchedVolume = 100.5
    tradeDateTime = "2024-01-15T10:30:00"
    buyOrderCompletionPercentage = 75.0
    buyOrderRemainingPercentage = 25.0
    sellOrderCompletionPercentage = 100.0
    sellOrderRemainingPercentage = 0.0
    buyOrderTotalVolume = 134.0
    buyOrderRemainingVolume = 33.5
    sellOrderTotalVolume = 100.5
    sellOrderRemainingVolume = 0.0
} | ConvertTo-Json

try {
    $validResponse = Invoke-RestMethod -Uri "http://localhost:57546/api/telegram/notifications/trade-match" -Method POST -Body $validNotification -ContentType "application/json"
    Write-Host "✅ اطلاعیه معتبر ارسال شد" -ForegroundColor Green
    Write-Host "📋 پاسخ: $($validResponse.message)" -ForegroundColor Yellow
}
catch {
    Write-Host "❌ خطا در ارسال اطلاعیه معتبر: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "📝 جزئیات: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}

# تست 3: ارسال اطلاعیه نامعتبر (حجم صفر)
Write-Host "`n3️⃣ تست اطلاعیه نامعتبر (حجم صفر)..." -ForegroundColor Cyan

$invalidNotification = @{
    tradeId = "123e4567-e89b-12d3-a456-426614174000"
    buyOrderId = "223e4567-e89b-12d3-a456-426614174000"
    buyerUserId = "080A42C8-603F-422D-A02F-142407F3E94C"
    sellOrderId = "323e4567-e89b-12d3-a456-426614174000"
    sellerUserId = "D286F932-D4E9-49EC-8D72-D85EEC4907A7"
    asset = "USDT"
    price = 50000.0
    matchedVolume = 0
    tradeDateTime = "2024-01-15T10:30:00"
    buyOrderCompletionPercentage = 75.0
    buyOrderRemainingPercentage = 25.0
    sellOrderCompletionPercentage = 100.0
    sellOrderRemainingPercentage = 0.0
    buyOrderTotalVolume = 134.0
    buyOrderRemainingVolume = 33.5
    sellOrderTotalVolume = 100.5
    sellOrderRemainingVolume = 0.0
} | ConvertTo-Json

try {
    $invalidResponse = Invoke-RestMethod -Uri "http://localhost:57546/api/telegram/notifications/trade-match" -Method POST -Body $invalidNotification -ContentType "application/json"
    Write-Host "❌ اطلاعیه نامعتبر قبول شد (نباید اتفاق بیفتد)" -ForegroundColor Red
}
catch {
    if ($_.Exception.Response.StatusCode -eq 400) {
        Write-Host "✅ اطلاعیه نامعتبر به درستی رد شد (400 Bad Request)" -ForegroundColor Green
        try {
            $errorContent = $_.ErrorDetails.Message | ConvertFrom-Json
            Write-Host "📝 پیام خطا: $($errorContent.message)" -ForegroundColor Yellow
        } catch {
            Write-Host "📝 پیام خطا: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "❌ خطای غیرمنتظره: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# تست 4: ارسال داده خالی
Write-Host "`n4️⃣ تست داده خالی..." -ForegroundColor Cyan

try {
    $emptyResponse = Invoke-RestMethod -Uri "http://localhost:57546/api/telegram/notifications/trade-match" -Method POST -Body "{}" -ContentType "application/json"
    Write-Host "❌ داده خالی قبول شد (نباید اتفاق بیفتد)" -ForegroundColor Red
}
catch {
    if ($_.Exception.Response.StatusCode -eq 400) {
        Write-Host "✅ داده خالی به درستی رد شد (400 Bad Request)" -ForegroundColor Green
    } else {
        Write-Host "❌ خطای غیرمنتظره: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# تست 5: تست با شناسه کاربران نامعتبر
Write-Host "`n5️⃣ تست شناسه کاربران نامعتبر..." -ForegroundColor Cyan

$invalidUsersNotification = @{
    tradeId = "123e4567-e89b-12d3-a456-426614174000"
    buyerUserId = "00000000-0000-0000-0000-000000000000"
    sellerUserId = "00000000-0000-0000-0000-000000000000"
    matchedVolume = 100.5
    price = 50000.0
    asset = "USDT"
    completionPercentage = 75.0
    remainingPercentage = 25.0
    tradeDate = "2024-01-15T10:30:00"
} | ConvertTo-Json

try {
    $invalidUsersResponse = Invoke-RestMethod -Uri "http://localhost:57546/api/telegram/notifications/trade-match" -Method POST -Body $invalidUsersNotification -ContentType "application/json"
    Write-Host "❌ شناسه‌های نامعتبر قبول شد (نباید اتفاق بیفتد)" -ForegroundColor Red
}
catch {
    if ($_.Exception.Response.StatusCode -eq 400) {
        Write-Host "✅ شناسه‌های نامعتبر به درستی رد شد (400 Bad Request)" -ForegroundColor Green
        try {
            $errorContent = $_.ErrorDetails.Message | ConvertFrom-Json
            Write-Host "📝 پیام خطا: $($errorContent.message)" -ForegroundColor Yellow
        } catch {
            Write-Host "📝 پیام خطا: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "❌ خطای غیرمنتظره: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n🎉 تست‌ها تکمیل شد!" -ForegroundColor Green
Write-Host "`n💡 برای تست کامل، مطمئن شوید که:" -ForegroundColor Cyan
Write-Host "   • API کاربران در دسترس باشد" -ForegroundColor White
Write-Host "   • توکن تلگرام معتبر باشد" -ForegroundColor White
Write-Host "   • کاربران تست در سیستم موجود باشند" -ForegroundColor White
