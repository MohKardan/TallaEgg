using Orders.Core;

namespace Orders.Application;

public interface IMatchingEngine
{
    Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default);
    Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
