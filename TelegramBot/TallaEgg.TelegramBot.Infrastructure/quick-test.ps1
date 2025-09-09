# تست‌های ساده API اطلاع‌رسانی تطبیق معامله
Write-Host "🧪 شروع تست‌های API..." -ForegroundColor Green
Write-Host ""

# تست 1: Health Check
Write-Host "1️⃣ تست Health Check..." -ForegroundColor Cyan
try {
    $healthResponse = Invoke-RestMethod -Uri "http://localhost:57546/health" -Method GET
    Write-Host "   ✅ Health Check موفق!" -ForegroundColor Green
    Write-Host "   📊 Status: $($healthResponse.Status)" -ForegroundColor Yellow
    Write-Host "   🕐 Time: $($healthResponse.Timestamp)" -ForegroundColor Yellow
} catch {
    Write-Host "   ❌ Health Check ناموفق: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   💡 مطمئن شوید API اجرا شده باشد (dotnet run -- --api-only)" -ForegroundColor Magenta
}

Write-Host ""

# تست 2: اطلاعیه معتبر
Write-Host "2️⃣ تست اطلاعیه معتبر..." -ForegroundColor Cyan
$validNotification = @{
    tradeId = [System.Guid]::NewGuid().ToString()
    buyOrderId = [System.Guid]::NewGuid().ToString()
    buyerUserId = [System.Guid]::NewGuid().ToString()
    sellOrderId = [System.Guid]::NewGuid().ToString()
    sellerUserId = [System.Guid]::NewGuid().ToString()
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
} | ConvertTo-Json -Compress

try {
    $validResponse = Invoke-RestMethod -Uri "http://localhost:57546/api/telegram/notifications/trade-match" -Method POST -Body $validNotification -ContentType "application/json"
    Write-Host "   ✅ اطلاعیه معتبر ارسال شد!" -ForegroundColor Green
    Write-Host "   📋 پاسخ: $($validResponse.message)" -ForegroundColor Yellow
} catch {
    Write-Host "   ❌ خطا در ارسال اطلاعیه: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "   📝 جزئیات: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}

Write-Host ""

# تست 3: اطلاعیه نامعتبر
Write-Host "3️⃣ تست اطلاعیه نامعتبر (حجم صفر)..." -ForegroundColor Cyan
$invalidNotification = @{
    tradeId = [System.Guid]::NewGuid().ToString()
    buyOrderId = [System.Guid]::NewGuid().ToString()
    buyerUserId = [System.Guid]::NewGuid().ToString()
    sellOrderId = [System.Guid]::NewGuid().ToString()
    sellerUserId = [System.Guid]::NewGuid().ToString()
    asset = "USDT"
    price = 50000.0
    matchedVolume = 0  # حجم نامعتبر
    tradeDateTime = "2024-01-15T10:30:00"
    buyOrderCompletionPercentage = 75.0
    buyOrderRemainingPercentage = 25.0
    sellOrderCompletionPercentage = 100.0
    sellOrderRemainingPercentage = 0.0
    buyOrderTotalVolume = 134.0
    buyOrderRemainingVolume = 33.5
    sellOrderTotalVolume = 100.5
    sellOrderRemainingVolume = 0.0
} | ConvertTo-Json -Compress

try {
    $invalidResponse = Invoke-RestMethod -Uri "http://localhost:57546/api/telegram/notifications/trade-match" -Method POST -Body $invalidNotification -ContentType "application/json"
    Write-Host "   ❌ اطلاعیه نامعتبر قبول شد! (نباید اتفاق بیفتد)" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 400) {
        Write-Host "   ✅ اطلاعیه نامعتبر به درستی رد شد (400 Bad Request)" -ForegroundColor Green
        if ($_.ErrorDetails) {
            try {
                $errorContent = $_.ErrorDetails.Message | ConvertFrom-Json
                Write-Host "   📝 پیام خطا: $($errorContent.message)" -ForegroundColor Yellow
            } catch {
                Write-Host "   📝 پیام خطا: $($_.ErrorDetails.Message)" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "   ❌ خطای غیرمنتظره: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "🎉 تست‌ها تکمیل شد!" -ForegroundColor Green
Write-Host ""
Write-Host "📝 نتیجه‌گیری:" -ForegroundColor Cyan
Write-Host "   • برای تست کامل، API کاربران باید فعال باشد" -ForegroundColor White
Write-Host "   • توکن تلگرام معتبر در appsettings.json تنظیم شود" -ForegroundColor White
Write-Host "   • کاربران تست در سیستم موجود باشند" -ForegroundColor White
Write-Host ""
Write-Host "💡 برای متوقف کردن API: Ctrl+C در ترمینال API" -ForegroundColor Magenta
