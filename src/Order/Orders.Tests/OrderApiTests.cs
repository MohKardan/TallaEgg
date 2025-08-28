using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Newtonsoft.Json;

public class OrderApiTests
{
    [Fact]
    public async Task Can_Register_Buy_Order()
    {
        // Arrange
        var appFactory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        var client = appFactory.CreateClient();

        var order = new
        {
            Asset = "Gold",
            Amount = 1.5M,
            Price = 5200000M,
            UserId = "11111111-2222-3333-4444-555555555555",
            Type = "BUY"
        };
        var json = JsonConvert.SerializeObject(order);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/order", content);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("Gold");
        responseBody.Should().Contain("BUY");
    }
}