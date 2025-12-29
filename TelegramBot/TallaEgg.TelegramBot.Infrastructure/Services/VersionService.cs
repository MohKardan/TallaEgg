namespace TallaEgg.TelegramBot.Infrastructure.Services
{
    using System.Reflection;

    public class VersionService : IVersionService
    {
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
    }

}
