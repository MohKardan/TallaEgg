using Moq;
using TallaEgg.TelegramBot.Application.Services;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Tests.Services;

public class PriceServiceTests
{
    private readonly Mock<IPriceRepository> _mockPriceRepository;
    private readonly PriceService _priceService;

    public PriceServiceTests()
    {
        _mockPriceRepository = new Mock<IPriceRepository>();
        _priceService = new PriceService(_mockPriceRepository.Object);
    }

    [Fact]
    public async Task GetLatestPriceAsync_WithExistingPrice_ReturnsPrice()
    {
        // Arrange
        var asset = "Gold";
        var expectedPrice = new Price
        {
            Id = Guid.NewGuid(),
            Asset = asset,
            BuyPrice = 2500000m,
            SellPrice = 2550000m,
            UpdatedAt = DateTime.UtcNow,
            Source = "Manual"
        };

        _mockPriceRepository.Setup(x => x.GetByAssetAsync(asset))
            .ReturnsAsync(expectedPrice);

        // Act
        var result = await _priceService.GetLatestPriceAsync(asset);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(asset, result.Asset);
        Assert.Equal(2500000m, result.BuyPrice);
        Assert.Equal(2550000m, result.SellPrice);
    }

    [Fact]
    public async Task GetLatestPriceAsync_WithNonExistentPrice_ReturnsNull()
    {
        // Arrange
        var asset = "NonExistent";

        _mockPriceRepository.Setup(x => x.GetByAssetAsync(asset))
            .ReturnsAsync((Price?)null);

        // Act
        var result = await _priceService.GetLatestPriceAsync(asset);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllPricesAsync_ReturnsAllPrices()
    {
        // Arrange
        var prices = new List<Price>
        {
            new Price { Id = Guid.NewGuid(), Asset = "Gold", BuyPrice = 2500000m, SellPrice = 2550000m },
            new Price { Id = Guid.NewGuid(), Asset = "Diamond", BuyPrice = 5000000m, SellPrice = 5100000m }
        };

        _mockPriceRepository.Setup(x => x.GetAllAsync())
            .ReturnsAsync(prices);

        // Act
        var result = await _priceService.GetAllPricesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Contains(result, p => p.Asset == "Gold");
        Assert.Contains(result, p => p.Asset == "Diamond");
    }

    [Fact]
    public async Task UpdatePriceAsync_WithValidData_ReturnsUpdatedPrice()
    {
        // Arrange
        var asset = "Gold";
        var buyPrice = 2500000m;
        var sellPrice = 2550000m;
        var source = "Manual";

        var expectedPrice = new Price
        {
            Id = Guid.NewGuid(),
            Asset = asset,
            BuyPrice = buyPrice,
            SellPrice = sellPrice,
            UpdatedAt = DateTime.UtcNow,
            Source = source
        };

        _mockPriceRepository.Setup(x => x.UpdateAsync(It.IsAny<Price>()))
            .ReturnsAsync(expectedPrice);

        // Act
        var result = await _priceService.UpdatePriceAsync(asset, buyPrice, sellPrice, source);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(asset, result.Asset);
        Assert.Equal(buyPrice, result.BuyPrice);
        Assert.Equal(sellPrice, result.SellPrice);
        Assert.Equal(source, result.Source);
    }

    [Fact]
    public async Task UpdatePriceAsync_WithDefaultSource_UsesManual()
    {
        // Arrange
        var asset = "Gold";
        var buyPrice = 2500000m;
        var sellPrice = 2550000m;

        var expectedPrice = new Price
        {
            Id = Guid.NewGuid(),
            Asset = asset,
            BuyPrice = buyPrice,
            SellPrice = sellPrice,
            UpdatedAt = DateTime.UtcNow,
            Source = "Manual"
        };

        _mockPriceRepository.Setup(x => x.UpdateAsync(It.IsAny<Price>()))
            .ReturnsAsync(expectedPrice);

        // Act
        var result = await _priceService.UpdatePriceAsync(asset, buyPrice, sellPrice);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Manual", result.Source);
    }
} 