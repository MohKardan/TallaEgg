using Orders.Core;

namespace Orders.Application;

public interface IMatchingEngine
{
    Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default);
    Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default);

    // StartAsync/StopAsync are deliberately absent. The host (IHostedService) owns the matching
    // engine's lifetime. Now that only one instance exists (issue #53), exposing StopAsync would let
    // any consumer switch matching off for the entire process.
}