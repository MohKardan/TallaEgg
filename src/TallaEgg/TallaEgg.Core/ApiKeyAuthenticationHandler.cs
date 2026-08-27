using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
            if (!Request.Headers.ContainsKey("X-API-Key"))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing API Key"));
            }

            var apiKey = Request.Headers["X-API-Key"].ToString();

            if (apiKey != Options.ApiKey)
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
    }
}
