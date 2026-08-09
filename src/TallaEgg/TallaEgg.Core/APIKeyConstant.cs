using System;

namespace TallaEgg.Core
{
    /// <summary>
    /// The shared API key every inter-service HTTP client sends via the X-API-Key header, and
    /// that <see cref="ApiKeyAuthenticationHandler"/> checks incoming requests against in
    /// Production. Read from the <c>TALLAEGG_API_KEY</c> environment variable instead of a
    /// compiled-in literal — the previous <c>const string</c> was committed to this public
    /// repo's git history (issue #33 / audit finding C-1).
    /// </summary>
    public static class APIKeyConstant
    {
        private const string EnvironmentVariableName = "TALLAEGG_API_KEY";

        // Not a secret — an obviously-fake placeholder so a clone without the environment
        // variable set still runs locally, the same way every service already skips API-key
        // authentication entirely outside Production (see each Program.cs's IsProduction check).
        private const string LocalDevPlaceholder = "local-dev-key-set-TALLAEGG_API_KEY-for-production";

        public static string TallaEggApiKey =>
            Environment.GetEnvironmentVariable(EnvironmentVariableName) is { Length: > 0 } value
                ? value
                : LocalDevPlaceholder;

        /// <summary>
        /// Same value, but throws if the environment variable is unset. Used only where a silent
        /// fallback would be a real hole: wiring up Production's API-key authentication.
        /// </summary>
        public static string RequireTallaEggApiKey()
        {
            var value = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{EnvironmentVariableName}' is not set. Production requires " +
                    "the shared API key to be supplied out-of-band; it is no longer read from source.");
            }

            return value;
        }
    }
}
