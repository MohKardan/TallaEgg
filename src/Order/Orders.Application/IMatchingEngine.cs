using Orders.Core;

namespace Orders.Application;

public interface IMatchingEngine
{
    Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default);
    Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default);

    // StartAsync/StopAsync عمداً اینجا نیستند. چرخهٔ حیات موتور تطبیق را میزبان
    // (IHostedService) مدیریت می‌کند. حالا که فقط یک نمونه از موتور وجود دارد
    // (issue #53)، در معرض گذاشتن StopAsync یعنی هر مصرف‌کننده‌ای می‌توانست تطبیق
    // را برای کل پروسه خاموش کند.
}