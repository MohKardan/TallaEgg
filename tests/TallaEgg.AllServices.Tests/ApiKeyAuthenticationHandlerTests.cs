using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Verifies <see cref="ApiKeyAuthenticationHandler"/> constant-time API key authentication
/// (issue #368): valid keys succeed, invalid or mismatched-length keys fail, missing headers fail,
/// and an empty configured key fails closed even when the caller also supplies an empty header.
/// </summary>
public class ApiKeyAuthenticationHandlerTests
{
    private const string ConfiguredKey = "super-secret-tallaegg-api-key-1234";

    private static async Task<AuthenticateResult> AuthenticateAsync(string configuredKey, string? providedHeader)
    {
        var optionsMonitor = new StaticOptionsMonitor(new ApiKeyAuthenticationSchemeOptions
        {
            ApiKey = configuredKey
        });

        var handler = new ApiKeyAuthenticationHandler(
            optionsMonitor,
            NullLoggerFactory.Instance,
            UrlEncoder.Default);

        var httpContext = new DefaultHttpContext();
        if (providedHeader is not null)
        {
            httpContext.Request.Headers["X-API-Key"] = providedHeader;
        }

        var scheme = new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler));
        await handler.InitializeAsync(scheme, httpContext);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task ValidKey_SucceedsAndPopulatesExpectedClaims()
    {
        var result = await AuthenticateAsync(ConfiguredKey, ConfiguredKey);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.Equal("ApiClient", result.Principal!.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("api-client", result.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task InvalidKeyWithSameLength_Fails()
    {
        // Same byte length as ConfiguredKey, differing only in the last character.
        var wrongSameLength = ConfiguredKey[..^1] + "9";
        Assert.Equal(ConfiguredKey.Length, wrongSameLength.Length);

        var result = await AuthenticateAsync(ConfiguredKey, wrongSameLength);

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid API Key", result.Failure?.Message);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("super-secret-tallaegg-api-key-1234-with-extra-suffix")]
    [InlineData("")]
    public async Task MismatchedLengthKey_Fails(string providedKey)
    {
        var result = await AuthenticateAsync(ConfiguredKey, providedKey);

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid API Key", result.Failure?.Message);
    }

    [Fact]
    public async Task MissingHeader_FailsWithMissingApiKeyMessage()
    {
        var result = await AuthenticateAsync(ConfiguredKey, providedHeader: null);

        Assert.False(result.Succeeded);
        Assert.Equal("Missing API Key", result.Failure?.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("any-key")]
    public async Task EmptyConfiguredKey_FailsClosed(string providedHeader)
    {
        var result = await AuthenticateAsync(string.Empty, providedHeader);

        Assert.False(result.Succeeded);
        Assert.Equal("Invalid API Key", result.Failure?.Message);
    }

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "ABC", false)]
    [InlineData("abc", "abcd", false)]
    [InlineData("", "", false)]
    [InlineData(null, "", false)]
    [InlineData("", null, false)]
    [InlineData(null, null, false)]
    public void AreKeysEqual_BehavesAsExpected(string? provided, string? expected, bool shouldMatch)
    {
        Assert.Equal(shouldMatch, ApiKeyAuthenticationHandler.AreKeysEqual(provided, expected));
    }

    private sealed class StaticOptionsMonitor(ApiKeyAuthenticationSchemeOptions value)
        : IOptionsMonitor<ApiKeyAuthenticationSchemeOptions>
    {
        public ApiKeyAuthenticationSchemeOptions CurrentValue => value;
        public ApiKeyAuthenticationSchemeOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ApiKeyAuthenticationSchemeOptions, string?> listener) => null;
    }
}
