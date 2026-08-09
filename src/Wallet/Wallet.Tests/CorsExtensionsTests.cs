using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TallaEgg.Core.Cors;

namespace Wallet.Tests;

// Exercises TallaEgg.Core.Cors.CorsExtensions the same way every API service wires it up
// (issue #31), against a bare TestServer instead of any specific service's Program — none of
// them can boot in this environment without a live SQL Server (issue #68).
public class CorsExtensionsTests
{
    [Fact]
    public async Task UnlistedOrigin_IsRejected()
    {
        var client = await CreateClientAsync("https://allowed.example.com");

        var response = await SendWithOriginAsync(client, "https://not-allowed.example.com");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ListedOrigin_IsAllowed()
    {
        var client = await CreateClientAsync("https://allowed.example.com");

        var response = await SendWithOriginAsync(client, "https://allowed.example.com");

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Contains("https://allowed.example.com", values!);
    }

    [Fact]
    public async Task NoConfiguredOrigins_RejectsEveryOrigin()
    {
        var client = await CreateClientAsync();

        var response = await SendWithOriginAsync(client, "https://anything.example.com");

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static Task<HttpResponseMessage> SendWithOriginAsync(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", origin);
        return client.SendAsync(request);
    }

    private static async Task<HttpClient> CreateClientAsync(params string[] allowedOrigins)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(BuildOriginsConfig(allowedOrigins))
            .Build();

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services => services.AddTallaEggCors(configuration))
                    .Configure(app =>
                    {
                        app.UseTallaEggCors();
                        app.Run(context => context.Response.WriteAsync("ok"));
                    });
            })
            .StartAsync();

        return host.GetTestClient();
    }

    private static IEnumerable<KeyValuePair<string, string?>> BuildOriginsConfig(string[] allowedOrigins)
    {
        for (var i = 0; i < allowedOrigins.Length; i++)
        {
            yield return new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{i}", allowedOrigins[i]);
        }
    }
}
