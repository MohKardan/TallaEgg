using Microsoft.Extensions.Logging;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.Enums.Order;
using Xunit;

namespace Orders.Tests
{
    /// <summary>
    /// Unit tests for Order Matching Logic
    /// تست‌های واحد برای منطق تطبیق سفارشات
    /// </summary>
    public class OrderMatchingTests
    {
        [Fact]
        public void Order_Creation_Should_Work()
        {
            // Arrange
            var asset = "BTC";
            var amount = 10m;
            var price = 45000m;
            var userId = Guid.NewGuid();

            // Act
            var buyOrder = Order.CreateMakerOrder(
                asset, amount, price, userId, 
                OrderType.Buy, TradingType.Spot);

            // Assert
            Assert.NotNull(buyOrder);
            Assert.Equal(asset, buyOrder.Asset);
            Assert.Equal(amount, buyOrder.Amount);
            Assert.Equal(amount, buyOrder.RemainingAmount);
            Assert.Equal(price, buyOrder.Price);
            Assert.Equal(OrderStatus.Pending, buyOrder.Status);
            Assert.Equal(OrderRole.Maker, buyOrder.Role);
        }

        [Fact]
        public void Order_RemainingAmount_Update_Should_Work()
        {
            // Arrange
            var order = Order.CreateMakerOrder(
                "BTC", 100m, 45000m, Guid.NewGuid(),
                OrderType.Buy, TradingType.Spot);

            // Act
            order.UpdateRemainingAmount(75m);

            // Assert
            Assert.Equal(75m, order.RemainingAmount);
            Assert.Equal(100m, order.Amount); // Original amount unchanged
        }

        [Fact]
        public void Order_Status_Updates_Should_Work()
        {
            // Arrange
            var order = Order.CreateMakerOrder(
                "BTC", 100m, 45000m, Guid.NewGuid(),
                OrderType.Buy, TradingType.Spot);

            // Act & Assert - Confirm
            order.Confirm();
            Assert.Equal(OrderStatus.Confirmed, order.Status);

            // Act & Assert - Complete
            order.Complete();
            Assert.Equal(OrderStatus.Completed, order.Status);
        }

        [Fact]
        public void Trade_Creation_Should_Work()
        {
            // Arrange
            var buyOrderId = Guid.NewGuid();
            var sellOrderId = Guid.NewGuid();
            var buyerUserId = Guid.NewGuid();
            var sellerUserId = Guid.NewGuid();

            // Act
            var trade = Trade.Create(
                buyOrderId, sellOrderId, "BTC",
                45000m, 1m, 45000m,
                buyerUserId, sellerUserId,
                45m, 45m);

            // Assert
            Assert.NotNull(trade);
            Assert.Equal(buyOrderId, trade.BuyOrderId);
            Assert.Equal(sellOrderId, trade.SellOrderId);
            Assert.Equal("BTC", trade.Symbol);
            Assert.Equal(45000m, trade.Price);
            Assert.Equal(1m, trade.Quantity);
            Assert.Equal(45000m, trade.QuoteQuantity);
        }

        [Theory]
        [InlineData(50000m, 45000m, true)]  // Buy >= Sell - Should match
        [InlineData(45000m, 45000m, true)]  // Buy = Sell - Should match
        [InlineData(40000m, 45000m, false)] // Buy < Sell - Should NOT match
        public void Price_Compatibility_Logic_Should_Work(decimal buyPrice, decimal sellPrice, bool shouldMatch)
        {
            // Arrange
            var buyOrder = Order.CreateMakerOrder(
                "BTC", 100m, buyPrice, Guid.NewGuid(),
                OrderType.Buy, TradingType.Spot);

            var sellOrder = Order.CreateMakerOrder(
                "BTC", 100m, sellPrice, Guid.NewGuid(),
                OrderType.Sell, TradingType.Spot);

            // Act
            bool isCompatible = buyOrder.Price >= sellOrder.Price;

            // Assert
            Assert.Equal(shouldMatch, isCompatible);
        }

        [Fact]
        public void Order_Validation_Should_Prevent_Invalid_Data()
        {
            // Assert - Empty asset should throw
            Assert.Throws<ArgumentException>(() => 
                Order.CreateMakerOrder("", 100m, 45000m, Guid.NewGuid(), 
                    OrderType.Buy, TradingType.Spot));

            // Assert - Zero amount should throw
            Assert.Throws<ArgumentException>(() => 
                Order.CreateMakerOrder("BTC", 0m, 45000m, Guid.NewGuid(), 
                    OrderType.Buy, TradingType.Spot));

            // Assert - Zero price should throw
            Assert.Throws<ArgumentException>(() => 
                Order.CreateMakerOrder("BTC", 100m, 0m, Guid.NewGuid(), 
                    OrderType.Buy, TradingType.Spot));

            // Assert - Empty user ID should throw
            Assert.Throws<ArgumentException>(() => 
                Order.CreateMakerOrder("BTC", 100m, 45000m, Guid.Empty, 
                    OrderType.Buy, TradingType.Spot));
        }

        [Fact]
        public void Negative_RemainingAmount_Should_Throw()
        {
            // Arrange
            var order = Order.CreateMakerOrder(
                "BTC", 100m, 45000m, Guid.NewGuid(),
                OrderType.Buy, TradingType.Spot);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => order.UpdateRemainingAmount(-10m));
        }

        [Fact] 
        public void RemainingAmount_Greater_Than_Amount_Should_Throw()
        {
            // Arrange
            var order = Order.CreateMakerOrder(
                "BTC", 100m, 45000m, Guid.NewGuid(),
                OrderType.Buy, TradingType.Spot);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => order.UpdateRemainingAmount(150m));
        }
    }
}
