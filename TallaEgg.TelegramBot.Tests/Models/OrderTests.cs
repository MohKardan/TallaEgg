using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Tests.Models;

public class OrderTests
{
    [Fact]
    public void Order_DefaultValues_AreCorrect()
    {
        // Act
        var order = new Order();

        // Assert
        Assert.Equal(Guid.Empty, order.Id);
        Assert.Equal("Gold", order.Asset);
        Assert.Equal(0, order.Amount);
        Assert.Equal(0, order.Price);
        Assert.Equal(Guid.Empty, order.UserId);
        Assert.Equal("BUY", order.Type);
        Assert.Equal(DateTime.MinValue, order.CreatedAt);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.CancelledAt);
        Assert.Null(order.CancelledBy);
        Assert.Null(order.CancellationReason);
    }

    [Fact]
    public void Order_WithValidData_PropertiesAreSet()
    {
        // Arrange
        var id = Guid.NewGuid();
        var asset = "Diamond";
        var amount = 5.5m;
        var price = 5000000m;
        var userId = Guid.NewGuid();
        var type = "SELL";
        var createdAt = DateTime.UtcNow;
        var status = OrderStatus.Completed;
        var cancelledAt = DateTime.UtcNow;
        var cancelledBy = "user123";
        var cancellationReason = "Cancelled by user";

        // Act
        var order = new Order
        {
            Id = id,
            Asset = asset,
            Amount = amount,
            Price = price,
            UserId = userId,
            Type = type,
            CreatedAt = createdAt,
            Status = status,
            CancelledAt = cancelledAt,
            CancelledBy = cancelledBy,
            CancellationReason = cancellationReason
        };

        // Assert
        Assert.Equal(id, order.Id);
        Assert.Equal(asset, order.Asset);
        Assert.Equal(amount, order.Amount);
        Assert.Equal(price, order.Price);
        Assert.Equal(userId, order.UserId);
        Assert.Equal(type, order.Type);
        Assert.Equal(createdAt, order.CreatedAt);
        Assert.Equal(status, order.Status);
        Assert.Equal(cancelledAt, order.CancelledAt);
        Assert.Equal(cancelledBy, order.CancelledBy);
        Assert.Equal(cancellationReason, order.CancellationReason);
    }

    [Fact]
    public void OrderStatus_EnumValues_AreCorrect()
    {
        // Assert
        Assert.Equal(0, (int)OrderStatus.Pending);
        Assert.Equal(1, (int)OrderStatus.Completed);
        Assert.Equal(2, (int)OrderStatus.Cancelled);
        Assert.Equal(3, (int)OrderStatus.Failed);
    }

    [Theory]
    [InlineData("BUY", "خرید")]
    [InlineData("SELL", "فروش")]
    public void Order_TypeValues_AreValid(string type, string expectedPersianName)
    {
        // Arrange
        var order = new Order { Type = type };

        // Act & Assert
        Assert.Equal(type, order.Type);
        // Note: This test validates that the type values are as expected
        // The Persian name mapping would be handled in the business logic
    }

    [Theory]
    [InlineData("Gold")]
    [InlineData("Diamond")]
    [InlineData("Silver")]
    public void Order_AssetValues_AreValid(string asset)
    {
        // Arrange
        var order = new Order { Asset = asset };

        // Act & Assert
        Assert.Equal(asset, order.Asset);
    }

    [Fact]
    public void Order_WithCancellationInfo_IsCancelled()
    {
        // Arrange
        var order = new Order
        {
            Status = OrderStatus.Cancelled,
            CancelledAt = DateTime.UtcNow,
            CancelledBy = "user123",
            CancellationReason = "User requested cancellation"
        };

        // Assert
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.NotNull(order.CancelledAt);
        Assert.NotNull(order.CancelledBy);
        Assert.NotNull(order.CancellationReason);
    }
} 