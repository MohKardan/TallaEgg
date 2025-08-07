using Orders.Core;

namespace Orders.Application;

public class CreateOrderCommandHandler
{
    private readonly IOrderService _orderService;

    public CreateOrderCommandHandler(IOrderService orderService)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
    }

    public async Task<Order> Handle(CreateOrderCommand command)
    {
        return await _orderService.CreateOrderAsync(command);
    }
}