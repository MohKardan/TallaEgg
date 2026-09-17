using TallaEgg.TelegramBot.Infrastructure.Services;

namespace TallaEgg.TelegramBot.Simulator;

/// <summary>Version announcements are irrelevant to a simulation run.</summary>
public sealed class NullVersionService : IVersionService
{
    public string GetCurrentVersion() => "simulator";

    public string? GetLastAnnouncedVersion() => null;

    public void SaveAnnouncedVersion(string version) { }
}
