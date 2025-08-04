using Moq;
using TallaEgg.TelegramBot.Application.Services;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Tests.Services;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        _orderService = new OrderService(_mockOrderRepository.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_WithValidData_ReturnsOrder()
    {
        // Arrange
        var asset = "Gold";
        var amount = 10.5m;
        var price = 2500000m;
        var userId = Guid.NewGuid();
        var type = "BUY";

        var expectedOrder = new Order
        {
            Id = Guid.NewGuid(),
            Asset = asset,
            Amount = amount,
            Price = price,
            UserId = userId,
            Type = type,
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending
        };

        _mockOrderRepository.Setup(x => x.AddAsync(It.IsAny<Order>()))
            .ReturnsAsync(expectedOrder);

        // Act
        var result = await _orderService.CreateOrderAsync(asset, amount, price, userId, type);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(asset, result.Asset);
        Assert.Equal(amount, result.Amount);
        Assert.Equal(price, result.Price);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(type, result.Type);
        Assert.Equal(OrderStatus.Pending, result.Status);
    }

    [Fact]
    public async Task GetOrdersByAssetAsync_ReturnsOrders()
    {
        // Arrange
        var asset = "Gold";
        var orders = new List<Order>
        {
            new Order { Id = Guid.NewGuid(), Asset = asset, Amount = 10, Price = 2500000, Status = OrderStatus.Pending },
            new Order { Id = Guid.NewGuid(), Asset = asset, Amount = 5, Price = 2500000, Status = OrderStatus.Completed }
        };

        _mockOrderRepository.Setup(x => x.GetOrdersByAssetAsync(asset))
            .ReturnsAsync(orders);

        // Act
        var result = await _orderService.GetOrdersByAssetAsync(asset);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.All(result, order => Assert.Equal(asset, order.Asset));
    }

    [Fact]
    public async Task GetOrderByIdAsync_WithExistingOrder_ReturnsOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var expectedOrder = new Order
        {
            Id = orderId,
            Asset = "Gold",
            Amount = 10,
            Price = 2500000,
            Status = OrderStatus.Pending
        };

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
            .ReturnsAsync(expectedOrder);

        // Act
        var result = await _orderService.GetOrderByIdAsync(orderId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderId, result.Id);
    }

    [Fact]
    public async Task GetOrderByIdAsync_WithNonExistentOrder_ReturnsNull()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
            .ReturnsAsync((Order?)null);

        // Act
        var result = await _orderService.GetOrderByIdAsync(orderId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserActiveOrdersAsync_ReturnsOnlyPendingOrders()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var allOrders = new List<Order>
        {
            new Order { Id = Guid.NewGuid(), UserId = userId, Asset = "Gold", Status = OrderStatus.Pending },
            new Order { Id = Guid.NewGuid(), UserId = userId, Asset = "Gold", Status = OrderStatus.Completed },
            new Order { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Asset = "Gold", Status = OrderStatus.Pending },
            new Order { Id = Guid.NewGuid(), UserId = userId, Asset = "Gold", Status = OrderStatus.Cancelled }
        };

        _mockOrderRepository.Setup(x => x.GetOrdersByAssetAsync("Gold"))
            .ReturnsAsync(allOrders);

        // Act
        var result = await _orderService.GetUserActiveOrdersAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(userId, result.First().UserId);
        Assert.Equal(OrderStatus.Pending, result.First().Status);
    }

    [Fact]
    public async Task CancelOrderAsync_WithValidOrder_ReturnsCancelledOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reason = "Cancelled by user";

        var existingOrder = new Order
        {
            Id = orderId,
            UserId = userId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var expectedOrder = new Order
        {
            Id = orderId,
            UserId = userId,
            Status = OrderStatus.Cancelled,
            CancelledAt = DateTime.UtcNow,
            CancelledBy = userId.ToString(),
            CancellationReason = reason
        };

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
            .ReturnsAsync(existingOrder);
        _mockOrderRepository.Setup(x => x.UpdateAsync(It.IsAny<Order>()))
            .ReturnsAsync(expectedOrder);

        // Act
        var result = await _orderService.CancelOrderAsync(orderId, userId, reason);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Cancelled, result.Status);
        Assert.NotNull(result.CancelledAt);
        Assert.Equal(userId.ToString(), result.CancelledBy);
        Assert.Equal(reason, result.CancellationReason);
    }

    [Fact]
    public async Task CancelOrderAsync_WithNonExistentOrder_ThrowsException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
            .ReturnsAsync((Order?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.CancelOrderAsync(orderId, userId));
    }

    [Fact]
    public async Task CancelOrderAsync_WithWrongUser_ThrowsException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orderUserId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();

        var existingOrder = new Order
        {
            Id = orderId,
            UserId = orderUserId,
            Status = OrderStatus.Pending
        };

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
            .ReturnsAsync(existingOrder);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.CancelOrderAsync(orderId, requestingUserId));
    }

    [Fact]
    public async Task CancelOrderAsync_WithNonPendingOrder_ThrowsException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var existingOrder = new Order
        {
            Id = orderId,
            UserId = userId,
            Status = OrderStatus.Completed
        };

        _mockOrderRepository.Setup(x => x.GetByIdAsync(orderId))
            .ReturnsAsync(existingOrder);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _orderService.CancelOrderAsync(orderId, userId));
    }

    [Fact]
    public async Task CancelAllUserOrdersAsync_WithActiveOrders_CancelsAll()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeOrders = new List<Order>
        {
            new Order { Id = Guid.NewGuid(), UserId = userId, Status = OrderStatus.Pending },
            new Order { Id = Guid.NewGuid(), UserId = userId, Status = OrderStatus.Pending }
        };

        _mockOrderRepository.Setup(x => x.GetOrdersByAssetAsync("Gold"))
            .ReturnsAsync(activeOrders);
        _mockOrderRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => activeOrders.First(o => o.Id == id));
        _mockOrderRepository.Setup(x => x.UpdateAsync(It.IsAny<Order>()))
            .ReturnsAsync((Order order) => order);

        // Act
        var result = await _orderService.CancelAllUserOrdersAsync(userId);

        // Assert
        Assert.True(result);
        _mockOrderRepository.Verify(x => x.UpdateAsync(It.IsAny<Order>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HasActiveOrdersAsync_WithActiveOrders_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var activeOrders = new List<Order>
        {
            new Order { Id = Guid.NewGuid(), UserId = userId, Status = OrderStatus.Pending }
        };

        _mockOrderRepository.Setup(x => x.GetOrdersByAssetAsync("Gold"))
            .ReturnsAsync(activeOrders);

        // Act
        var result = await _orderService.HasActiveOrdersAsync(userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasActiveOrdersAsync_WithNoActiveOrders_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orders = new List<Order>
        {
            new Order { Id = Guid.NewGuid(), UserId = userId, Status = OrderStatus.Completed },
            new Order { Id = Guid.NewGuid(), UserId = userId, Status = OrderStatus.Cancelled }
        };

        _mockOrderRepository.Setup(x => x.GetOrdersByAssetAsync("Gold"))
            .ReturnsAsync(orders);

        // Act
        var result = await _orderService.HasActiveOrdersAsync(userId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanUserCreateOrderAsync_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await _orderService.CanUserCreateOrderAsync(userId);

        // Assert
        Assert.True(result);
    }
} 