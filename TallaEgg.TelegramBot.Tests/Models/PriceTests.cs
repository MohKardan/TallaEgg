using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Tests.Models;

public class PriceTests
{
    [Fact]
    public void Price_DefaultValues_AreCorrect()
    {
        // Act
        var price = new Price();

        // Assert
        Assert.Equal(Guid.Empty, price.Id);
        Assert.Equal("", price.Asset);
        Assert.Equal(0, price.BuyPrice);
        Assert.Equal(0, price.SellPrice);
        Assert.Equal(DateTime.MinValue, price.UpdatedAt);
        Assert.Equal("", price.Source);
    }

    [Fact]
    public void Price_WithValidData_PropertiesAreSet()
    {
        // Arrange
        var id = Guid.NewGuid();
        var asset = "Gold";
        var buyPrice = 2500000m;
        var sellPrice = 2550000m;
        var updatedAt = DateTime.UtcNow;
        var source = "Manual";

        // Act
        var price = new Price
        {
            Id = id,
            Asset = asset,
            BuyPrice = buyPrice,
            SellPrice = sellPrice,
            UpdatedAt = updatedAt,
            Source = source
        };

        // Assert
        Assert.Equal(id, price.Id);
        Assert.Equal(asset, price.Asset);
        Assert.Equal(buyPrice, price.BuyPrice);
        Assert.Equal(sellPrice, price.SellPrice);
        Assert.Equal(updatedAt, price.UpdatedAt);
        Assert.Equal(source, price.Source);
    }

    [Theory]
    [InlineData("Gold")]
    [InlineData("Diamond")]
    [InlineData("Silver")]
    [InlineData("Platinum")]
    public void Price_AssetValues_AreValid(string asset)
    {
        // Arrange
        var price = new Price { Asset = asset };

        // Act & Assert
        Assert.Equal(asset, price.Asset);
    }

    [Theory]
    [InlineData("Manual")]
    [InlineData("API")]
    [InlineData("Market")]
    [InlineData("Exchange")]
    public void Price_SourceValues_AreValid(string source)
    {
        // Arrange
        var price = new Price { Source = source };

        // Act & Assert
        Assert.Equal(source, price.Source);
    }

    [Fact]
    public void Price_WithValidPrices_BuyPriceIsLessThanSellPrice()
    {
        // Arrange
        var buyPrice = 2500000m;
        var sellPrice = 2550000m;

        // Act
        var price = new Price
        {
            BuyPrice = buyPrice,
            SellPrice = sellPrice
        };

        // Assert
        Assert.Equal(buyPrice, price.BuyPrice);
        Assert.Equal(sellPrice, price.SellPrice);
        Assert.True(price.BuyPrice < price.SellPrice);
    }

    [Fact]
    public void Price_SpreadCalculation_IsCorrect()
    {
        // Arrange
        var buyPrice = 2500000m;
        var sellPrice = 2550000m;
        var expectedSpread = sellPrice - buyPrice;

        var price = new Price
        {
            BuyPrice = buyPrice,
            SellPrice = sellPrice
        };

        // Act
        var actualSpread = price.SellPrice - price.BuyPrice;

        // Assert
        Assert.Equal(expectedSpread, actualSpread);
        Assert.Equal(50000m, actualSpread);
    }

    [Fact]
    public void Price_UpdatedAt_IsSetCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var price = new Price
        {
            UpdatedAt = now
        };

        // Assert
        Assert.Equal(now, price.UpdatedAt);
        Assert.True(price.UpdatedAt <= DateTime.UtcNow);
    }
} 