using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Infrastructure.Clients;

namespace Orders.Application.Services;

/// <summary>
/// وقتی سفارشی کاملاً پر می‌شود، هر مقدار وثیقه‌ای که هنوز به نامش قفل مانده آزاد می‌کند.
///
/// چرا باقی‌مانده‌ای وجود دارد (issue #52): مبلغ قفل یک بار برای کل سفارش و رو به بالا
/// حساب می‌شود، ولی مصرفِ هر معامله جداگانه و رو به پایین. این جهت‌های مخالف تضمین
/// می‌کنند مجموع مصرف هرگز از قفل بیشتر نشود، اما اختلاف کوچکی باقی می‌گذارند که متعلق
/// به کاربر است و باید برگردد.
///
/// <b>زمان‌بندی مهم است.</b> نسخهٔ اول این کد بلافاصله پس از تطبیق اجرا می‌شد و در عمل
/// شکست می‌خورد: قفلِ موجودی <b>بعد از</b> تطبیق ساخته می‌شود (یافتهٔ ممیزی C-5) و
/// <b>بعدتر</b> توسط تسویهٔ outbox مصرف می‌گردد. پس آزادسازی با هر دو مسابقه می‌داد و
/// گاهی روی کیف پولی اجرا می‌شد که هنوز چیزی در آن قفل نشده بود.
///
/// حالا از پردازشگر outbox و پس از تسویهٔ موفق صدا زده می‌شود. آن نقطه ذاتاً بعد از
/// ساخته‌شدن قفل است — چون تسویه بدون قفل اصلاً موفق نمی‌شود — و بعد از مصرف آن.
/// </summary>
public class OrderCollateralReconciler
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly IWalletApiClient _walletApiClient;
    private readonly ILogger<OrderCollateralReconciler> _logger;

    public OrderCollateralReconciler(
        IOrderRepository orderRepository,
        ITradeRepository tradeRepository,
        IWalletApiClient walletApiClient,
        ILogger<OrderCollateralReconciler> logger)
    {
        _orderRepository = orderRepository;
        _tradeRepository = tradeRepository;
        _walletApiClient = walletApiClient;
        _logger = logger;
    }

    /// <summary>
    /// اگر سفارش کاملاً پر شده باشد، باقی‌ماندهٔ وثیقه‌اش را آزاد می‌کند. برای سفارشی که
    /// هنوز باز است کاری نمی‌کند، چون وثیقهٔ باقی‌مانده هنوز پشتوانهٔ بخش پرنشده است.
    /// </summary>
    public async Task ReleaseResidualLockIfCompletedAsync(Guid orderId)
    {
        try
        {
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order is null || order.Status != OrderStatus.Completed)
                return;

            var (asset, residual) = await ComputeResidualLockAsync(order);
            if (residual <= 0)
                return;

            var (success, message) = await _walletApiClient.UnlockBalanceAsync(order.UserId, asset, residual);

            if (success)
                _logger.LogInformation(
                    "Released residual lock of {Residual} {Asset} for completed order {OrderId}.",
                    residual, asset, orderId);
            else
                // بی‌خطر است: باقی‌مانده سر جایش می‌ماند و مغایرت‌گیری (#39) می‌تواند
                // بعداً برش دارد. هیچ پولی گم نمی‌شود، فقط آزاد نمی‌شود.
                _logger.LogWarning(
                    "Could not release residual lock of {Residual} {Asset} for completed order {OrderId}: {Message}",
                    residual, asset, orderId, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing residual lock for order {OrderId}", orderId);
        }
    }

    /// <summary>
    /// باقی‌مانده = «آنچه قفل شد» منهای «آنچه معاملات این سفارش مصرف کردند».
    ///
    /// «آنچه قفل شد» با همان فرمول و همان جهتِ گرد کردنِ لحظهٔ ثبت سفارش بازمحاسبه
    /// می‌شود. اگر اینجا Round و آنجا Ceiling باشد، دوباره همان اختلافی ساخته می‌شود
    /// که این کد برای برداشتنش نوشته شده.
    ///
    /// بازمحاسبه فقط به این دلیل ممکن است که قیمت هنگام ثبت سفارش به دقت ستون گرد
    /// می‌شود؛ پیش از آن، قفل با قیمتِ گرد‌نشده حساب می‌شد و از روی ردیف ذخیره‌شده
    /// قابل بازسازی نبود.
    /// </summary>
    public async Task<(string Asset, decimal Residual)> ComputeResidualLockAsync(Order order)
    {
        var parts = order.Asset.Split('/');
        var baseAsset = parts[0];
        var quoteAsset = parts.Length > 1 ? parts[1] : parts[0];

        if (order.Side == OrderSide.Buy)
        {
            var locked = CurrenciesConstant.CeilingToCurrencyPrecision(order.Amount * order.Price, quoteAsset);
            var trades = await _tradeRepository.GetTradesByBuyOrderIdAsync(order.Id);
            return (quoteAsset, locked - trades.Sum(t => t.QuoteQuantity));
        }

        // سمت فروش: وثیقه دارایی پایه است و هر معامله دقیقاً Quantity مصرف می‌کند،
        // بدون گرد کردن — پس در حالت عادی باقی‌مانده‌ای ندارد. محاسبه‌اش نگه داشته
        // می‌شود تا اگر روزی این فرض عوض شد، خودبه‌خود پوشش داده شود.
        var lockedBase = CurrenciesConstant.RoundToCurrencyPrecision(order.Amount, baseAsset);
        var sellTrades = await _tradeRepository.GetTradesBySellOrderIdAsync(order.Id);
        return (baseAsset, lockedBase - sellTrades.Sum(t => t.Quantity));
    }
}
