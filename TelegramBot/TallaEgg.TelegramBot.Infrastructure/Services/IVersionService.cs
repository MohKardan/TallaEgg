namespace TallaEgg.TelegramBot.Infrastructure.Services
{
    public interface IVersionService
    {
        public string GetCurrentVersion();

        /// <summary>
        /// The version that was last announced to users, or null if nothing has been
        /// announced yet (or the stored value could not be read). Lets a real version
        /// change be told apart from an ordinary restart.
        /// </summary>
        string? GetLastAnnouncedVersion();

        /// <summary>
        /// Records the version just announced, so later restarts on the same version
        /// are not reported to users as an update.
        /// </summary>
        void SaveAnnouncedVersion(string version);
    }
}