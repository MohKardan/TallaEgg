using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace TallaEgg.Core
{
    public class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {
        public string ApiKey { get; set; } = string.Empty;
    }

    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>
    {
        public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Check the header.
            if (!Request.Headers.TryGetValue("X-API-Key", out var headerValues))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing API Key"));
            }

            var apiKey = headerValues.ToString();

            if (!AreKeysEqual(apiKey, Options.ApiKey))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "ApiClient"),
                new Claim(ClaimTypes.NameIdentifier, "api-client")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        /// <summary>
        /// Compares a caller-supplied API key against the configured key in constant time without
        /// leaking key length (both inputs are hashed with SHA-256 before fixed-time comparison).
        /// Fails closed if either value is null or empty.
        /// </summary>
        internal static bool AreKeysEqual(string? providedKey, string? expectedKey)
        {
            if (string.IsNullOrEmpty(expectedKey) || string.IsNullOrEmpty(providedKey))
            {
                return false;
            }

            Span<byte> providedHash = stackalloc byte[SHA256.HashSizeInBytes];
            Span<byte> expectedHash = stackalloc byte[SHA256.HashSizeInBytes];

            SHA256.HashData(Encoding.UTF8.GetBytes(providedKey), providedHash);
            SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey), expectedHash);

            return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        }
    }
}
