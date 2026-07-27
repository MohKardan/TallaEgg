namespace TallaEgg.TelegramBot.Infrastructure.Services
{
    using System.Reflection;
    using Microsoft.Extensions.Logging;

    public class VersionService : IVersionService
    {
        private readonly ILogger<VersionService> _logger;

        // Kept outside the application folder so it survives a redeploy: a redeploy that
        // does not change the version must not be announced to users as an update.
        private static readonly string StateFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TallaEgg",
            "bot-announced-version.txt");

        public VersionService(ILogger<VersionService> logger)
        {
            _logger = logger;
        }

        public string GetCurrentVersion()
        {
            var version = Assembly
                .GetExecutingAssembly()
                .GetName()
                .Version;

            if (version == null)
                return "unknown";

            // مثال خروجی: 1.2.0
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        public string? GetLastAnnouncedVersion()
        {
            try
            {
                if (!File.Exists(StateFilePath))
                    return null;

                var stored = File.ReadAllText(StateFilePath).Trim();
                return string.IsNullOrWhiteSpace(stored) ? null : stored;
            }
            catch (Exception ex)
            {
                // Unreadable state is treated as "never announced". Worst case the current
                // version is announced once more; that is better than failing startup.
                _logger.LogWarning(ex, "Could not read the last announced version from {Path}", StateFilePath);
                return null;
            }
        }

        public void SaveAnnouncedVersion(string version)
        {
            try
            {
                var directory = Path.GetDirectoryName(StateFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(StateFilePath, version);
            }
            catch (Exception ex)
            {
                // If this fails the next restart will announce the same version again.
                // Log it loudly enough to be noticed, but never break startup over it.
                _logger.LogWarning(ex, "Could not persist the announced version to {Path}", StateFilePath);
            }
        }
    }

}
