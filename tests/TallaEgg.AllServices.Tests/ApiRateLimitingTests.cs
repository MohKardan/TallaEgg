using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TallaEgg.Core.Cors;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.Json;
using TallaEgg.Core.RateLimiting;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Covers <see cref="RateLimitingExtensions"/> behaviour and verifies that Users.Api, Wallet.Api
/// and Orders.Api all wire it up in the expected middleware order (issue #279).
/// </summary>
/// <remarks>
/// Behavioural tests run against <see cref="TestServer"/> rather than booting the three API
/// entrypoints directly, because those entrypoints are top-level statements that require
/// <c>appsettings.global.json</c> and SQL Server (see <see cref="CorsExtensionsTests"/> and
/// <see cref="DatabaseMigrationStartupTests"/>). Wiring and pipeline ordering across the three
/// hosts are pinned by the source-scanning guards below.
/// </remarks>
public class ApiRateLimitingTests
{
    private const string ExpectedTooManyRequestsMessage =
        "تعداد درخواست‌ها بیش از حد مجاز است. لطفاً کمی بعد دوباره تلاش کنید.";

    public static TheoryData<string> DeployedApiEntryPoints =>
    [
        "src/User/Users.Api/Program.cs",
        "src/Wallet/Wallet.Api/Program.cs",
        "src/Order/Orders.Api/Program.cs",
    ];

    public static TheoryData<string> RepresentativeServiceRoutes =>
    [
        "/api/user/12345",
        "/api/wallet/balances/00000000-0000-0000-0000-000000000001",
        "/api/quotes/MAUA/IRT",
        "/version",
    ];

    [Theory]
    [MemberData(nameof(RepresentativeServiceRoutes))]
    public async Task Requests_WithinPermitLimit_SucceedNormally(string route)
    {
        using var host = await StartTestHostAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "3",
            ["RateLimiting:WindowSeconds"] = "60",
            ["RateLimiting:QueueLimit"] = "0",
        });

        var client = host.GetTestClient();

        for (var i = 0; i < 3; i++)
        {
            var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Requests_WithDefaultConfiguration_AllowNormalTrafficBurst()
    {
        using var host = await StartTestHostAsync(new Dictionary<string, string?>());
        var client = host.GetTestClient();

        for (var i = 0; i < 20; i++)
        {
            var response = await client.GetAsync("/api/quotes/MAUA/IRT");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Theory]
    [MemberData(nameof(RepresentativeServiceRoutes))]
    public async Task BurstRequests_ExceedingPermitLimit_Return429WithRetryAfterAndApiResponseJson(string route)
    {
        using var host = await StartTestHostAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "2",
            ["RateLimiting:WindowSeconds"] = "60",
            ["RateLimiting:QueueLimit"] = "0",
        });

        var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(route)).StatusCode);

        var throttled = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.True(
            throttled.Headers.TryGetValues("Retry-After", out var retryAfterValues),
            "429 response must include a Retry-After header.");
        var retryAfterRaw = Assert.Single(retryAfterValues);
        Assert.True(int.TryParse(retryAfterRaw, out var retryAfterSeconds));
        Assert.InRange(retryAfterSeconds, 1, 60);

        var rawBody = await throttled.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(rawBody);
        var root = jsonDoc.RootElement;

        // Wire format must stay camelCase ApiResponse<T> — never ProblemDetails.
        Assert.True(root.TryGetProperty("success", out var successProp));
        Assert.False(successProp.GetBoolean());
        Assert.True(root.TryGetProperty("message", out var messageProp));
        Assert.Equal(ExpectedTooManyRequestsMessage, messageProp.GetString());
        Assert.False(root.TryGetProperty("title", out _));
        Assert.False(root.TryGetProperty("status", out _));

        var envelope = JsonSerializer.Deserialize<ApiResponse<object>>(rawBody, ApiJson.ResponseOptions);
        Assert.NotNull(envelope);
        Assert.False(envelope.Success);
        Assert.Equal(ExpectedTooManyRequestsMessage, envelope.Message);
    }

    [Fact]
    public async Task RateLimiting_DisabledInConfig_AllowsUnlimitedBurst()
    {
        using var host = await StartTestHostAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = "false",
            ["RateLimiting:PermitLimit"] = "1",
            ["RateLimiting:WindowSeconds"] = "60",
        });

        var client = host.GetTestClient();

        for (var i = 0; i < 10; i++)
        {
            var response = await client.GetAsync("/api/orders");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task PartitionByClientIp_DifferentRemoteIpAddresses_HaveIndependentLimits()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = "2",
                ["RateLimiting:WindowSeconds"] = "60",
            })
            .Build();

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => services.AddTallaEggRateLimiting(configuration))
                    .Configure(app =>
                    {
                        app.Use((context, next) =>
                        {
                            if (context.Request.Headers.TryGetValue("X-Test-Remote-Ip", out var ipHeader)
                                && IPAddress.TryParse(ipHeader.ToString(), out var ip))
                            {
                                context.Connection.RemoteIpAddress = ip;
                            }

                            return next(context);
                        });
                        app.UseTallaEggRateLimiting();
                        app.Run(context => context.Response.WriteAsync("ok"));
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Client A exhausts its window.
        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/orders", "X-Test-Remote-Ip", "203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/orders", "X-Test-Remote-Ip", "203.0.113.10")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendWithHeaderAsync(client, "/api/orders", "X-Test-Remote-Ip", "203.0.113.10")).StatusCode);

        // Client B on a different RemoteIpAddress is unaffected by Client A's burst.
        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/orders", "X-Test-Remote-Ip", "203.0.113.20")).StatusCode);
    }

    [Fact]
    public async Task PartitionByClientIp_SpoofingXForwardedForHeader_CannotBypassLimit()
    {
        using var host = await StartTestHostAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "2",
            ["RateLimiting:WindowSeconds"] = "60",
        });

        var client = host.GetTestClient();

        // Rotating raw X-Forwarded-For values from the same connection must not rotate partitions.
        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/orders", "X-Forwarded-For", "203.0.113.1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/orders", "X-Forwarded-For", "203.0.113.2")).StatusCode);

        var third = await SendWithHeaderAsync(client, "/api/orders", "X-Forwarded-For", "203.0.113.3");
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task PartitionByIp_UnauthenticatedCallerRotatingUserIdHeader_CannotBypassLimit()
    {
        using var host = await StartTestHostAsync(new Dictionary<string, string?>
        {
            ["RateLimiting:PermitLimit"] = "2",
            ["RateLimiting:WindowSeconds"] = "60",
        });

        var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/user/1", "X-User-Id", "attacker-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/user/2", "X-User-Id", "attacker-2")).StatusCode);

        var third = await SendWithHeaderAsync(client, "/api/user/3", "X-User-Id", "attacker-3");
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task PartitionByAuthenticatedUser_DifferentUsersOnSameIp_HaveIndependentLimits()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = "2",
                ["RateLimiting:WindowSeconds"] = "60",
            })
            .Build();

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => services.AddTallaEggRateLimiting(configuration))
                    .Configure(app =>
                    {
                        // Simulate authenticated callers (matching UseAuthentication before UseTallaEggRateLimiting).
                        app.Use((context, next) =>
                        {
                            if (context.Request.Headers.TryGetValue("X-Test-Auth-User", out var userId))
                            {
                                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                                    authenticationType: "TestAuth"));
                            }

                            return next(context);
                        });
                        app.UseTallaEggRateLimiting();
                        app.Run(context => context.Response.WriteAsync("ok"));
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // User A exhausts its bucket on the shared loopback IP.
        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/orders", "X-Test-Auth-User", "user-a")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/orders", "X-Test-Auth-User", "user-a")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendWithHeaderAsync(client, "/api/orders", "X-Test-Auth-User", "user-a")).StatusCode);

        // User B on the same IP still has its own full bucket.
        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/orders", "X-Test-Auth-User", "user-b")).StatusCode);
    }

    [Fact]
    public async Task PartitionByUser_AuthenticatedNonApiKeyPrincipalRotatingUserIdHeader_CannotBypassLimit()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = "2",
                ["RateLimiting:WindowSeconds"] = "60",
            })
            .Build();

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => services.AddTallaEggRateLimiting(configuration))
                    .Configure(app =>
                    {
                        app.Use((context, next) =>
                        {
                            // Authenticated under Bearer with no NameIdentifier claim — must not honor X-User-Id.
                            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                                [new Claim(ClaimTypes.Name, "web-user")],
                                authenticationType: "Bearer"));
                            return next(context);
                        });
                        app.UseTallaEggRateLimiting();
                        app.Run(context => context.Response.WriteAsync("ok"));
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/orders", "X-User-Id", "spoof-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/orders", "X-User-Id", "spoof-2")).StatusCode);

        var third = await SendWithHeaderAsync(client, "/api/orders", "X-User-Id", "spoof-3");
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public void ResolvePartitionKey_UsesVerifiedIdentityAndIgnoresSpoofedHeaders()
    {
        var loopbackContext = new DefaultHttpContext();
        Assert.Equal("ip:loopback", RateLimitingExtensions.ResolvePartitionKey(loopbackContext));

        var remoteIpContext = new DefaultHttpContext();
        remoteIpContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.7");
        Assert.Equal("ip:198.51.100.7", RateLimitingExtensions.ResolvePartitionKey(remoteIpContext));

        // Raw X-Forwarded-For is ignored; RemoteIpAddress (or loopback) remains the partition key.
        var spoofedForwardedContext = new DefaultHttpContext();
        spoofedForwardedContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.7");
        spoofedForwardedContext.Request.Headers["X-Forwarded-For"] = "203.0.113.42, 10.0.0.5";
        Assert.Equal("ip:198.51.100.7", RateLimitingExtensions.ResolvePartitionKey(spoofedForwardedContext));

        // Authenticated Bearer user is locked to its verified claim even when sending X-User-Id.
        var authenticatedBearerUser = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "user-123")],
                authenticationType: "Bearer"))
        };
        authenticatedBearerUser.Request.Headers["X-User-Id"] = "other-user";
        Assert.Equal("user:user-123", RateLimitingExtensions.ResolvePartitionKey(authenticatedBearerUser));

        // Authenticated non-ApiKey principal without a NameIdentifier/sub cannot rotate partitions via X-User-Id.
        var authenticatedBearerNoClaim = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "anonymous-bearer")],
                authenticationType: "Bearer"))
        };
        authenticatedBearerNoClaim.Request.Headers["X-User-Id"] = "spoofed-user";
        Assert.Equal("ip:loopback", RateLimitingExtensions.ResolvePartitionKey(authenticatedBearerNoClaim));

        // Authenticated non-ApiKey principal claiming NameIdentifier "api-client" cannot use X-User-Id override.
        var nonApiKeyClaimingServiceId = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "api-client")],
                authenticationType: "Bearer"))
        };
        nonApiKeyClaimingServiceId.Request.Headers["X-User-Id"] = "spoofed-user";
        Assert.Equal("user:api-client", RateLimitingExtensions.ResolvePartitionKey(nonApiKeyClaimingServiceId));

        // Verified ApiKey service client ("api-client") may supply X-User-Id to partition per customer.
        var verifiedApiKeyWithUserHeader = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "api-client")],
                authenticationType: "ApiKey"))
        };
        verifiedApiKeyWithUserHeader.Request.Headers["X-User-Id"] = "customer-99";
        Assert.Equal("user:customer-99", RateLimitingExtensions.ResolvePartitionKey(verifiedApiKeyWithUserHeader));

        var verifiedApiKeyWithoutUserHeader = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "api-client")],
                authenticationType: "ApiKey"))
        };
        Assert.Equal("ip:loopback", RateLimitingExtensions.ResolvePartitionKey(verifiedApiKeyWithoutUserHeader));
    }

    [Fact]
    public void RateLimitingOptions_NonPositiveValues_NormalizeToSafeDefaults()
    {
        var normalized = new RateLimitingOptions
        {
            Enabled = true,
            PermitLimit = 0,
            WindowSeconds = -10,
            QueueLimit = -1
        }.Normalize();

        Assert.True(normalized.Enabled);
        Assert.Equal(RateLimitingOptions.DefaultPermitLimit, normalized.PermitLimit);
        Assert.Equal(RateLimitingOptions.DefaultWindowSeconds, normalized.WindowSeconds);
        Assert.Equal(RateLimitingOptions.DefaultQueueLimit, normalized.QueueLimit);
    }

    [Fact]
    public async Task ThrottledResponse_WithCorsRegisteredBeforeRateLimiting_PreservesCorsHeader()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://app.zarorbit.ir",
                ["RateLimiting:PermitLimit"] = "1",
                ["RateLimiting:WindowSeconds"] = "60",
            })
            .Build();

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddTallaEggCors(config);
                        services.AddTallaEggRateLimiting(config);
                    })
                    .Configure(app =>
                    {
                        app.UseTallaEggCors();
                        app.UseTallaEggRateLimiting();
                        app.Run(context => context.Response.WriteAsync("ok"));
                    });
            })
            .StartAsync();

        var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, (await SendWithHeaderAsync(client, "/api/quotes/MAUA/IRT", "Origin", "https://app.zarorbit.ir")).StatusCode);

        var throttled = await SendWithHeaderAsync(client, "/api/quotes/MAUA/IRT", "Origin", "https://app.zarorbit.ir");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.True(throttled.Headers.TryGetValues("Access-Control-Allow-Origin", out var corsValues));
        Assert.Contains("https://app.zarorbit.ir", corsValues);
    }

    [Fact]
    public void UseTallaEggRateLimiting_WithoutServiceRegistration_ThrowsAtStartup()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var app = new ApplicationBuilder(services);

        Assert.Throws<InvalidOperationException>(() => app.UseTallaEggRateLimiting());
    }

    [Theory]
    [MemberData(nameof(DeployedApiEntryPoints))]
    public void EveryDeployedApi_RegistersAndAppliesRateLimitingInCorrectPipelineOrder(string relativePath)
    {
        var lines = ReadLines(relativePath);

        var addIndex = FindCodeLine(lines, "builder.Services.AddTallaEggRateLimiting(builder.Configuration);");
        var readinessIndex = FindCodeLine(lines, "app.UseDatabaseReadinessGate();");
        var corsIndex = FindCodeLine(lines, "app.UseTallaEggCors();");
        var useIndex = FindCodeLine(lines, "app.UseTallaEggRateLimiting();");
        var firstEndpointIndex = FindCodeLine(lines, "app.MapGet(\"/version\"");

        Assert.True(addIndex >= 0, $"{relativePath} does not register AddTallaEggRateLimiting.");
        Assert.True(useIndex >= 0, $"{relativePath} does not apply UseTallaEggRateLimiting.");
        Assert.True(
            useIndex > readinessIndex && useIndex > corsIndex,
            $"{relativePath} must call UseTallaEggRateLimiting() after UseDatabaseReadinessGate() and UseTallaEggCors().");
        Assert.True(
            useIndex < firstEndpointIndex,
            $"{relativePath} must call UseTallaEggRateLimiting() before mapping endpoints.");
    }

    private static async Task<IHost> StartTestHostAsync(IDictionary<string, string?> configValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => services.AddTallaEggRateLimiting(configuration))
                    .Configure(app =>
                    {
                        app.UseTallaEggRateLimiting();
                        app.Run(context => context.Response.WriteAsync("ok"));
                    });
            })
            .StartAsync();
    }

    private static Task<HttpResponseMessage> SendWithHeaderAsync(
        HttpClient client,
        string path,
        string headerName,
        string headerValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(headerName, headerValue);
        return client.SendAsync(request);
    }

    private static string[] ReadLines(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TallaEgg.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllLines(Path.Combine(directory.FullName, relativePath));
    }

    private static int FindCodeLine(string[] lines, string token) =>
        Array.FindIndex(
            lines,
            line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                && line.Contains(token, StringComparison.Ordinal));
}
