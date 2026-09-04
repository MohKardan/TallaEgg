namespace TallaEgg.Core.DTOs;

/// <summary>
/// Which build a running process is: the product version and the commit it was built from
/// (issue #218).
/// </summary>
/// <param name="Version">
/// The <c>VersionPrefix</c> the assembly was built with — <c>1.1.0</c> — from the single
/// declaration in <c>Directory.Build.props</c> (issue #217).
/// </param>
/// <param name="CommitHash">
/// The full commit hash the .NET SDK appended to <c>InformationalVersion</c>, or <c>null</c> when
/// the assembly carries no hash. It is absent whenever the build could not see a
/// <c>.git</c> directory — a published tree copied to a server builds nothing, so this is about
/// the machine that produced the binary, not the one running it.
/// </param>
public record BuildVersionDto(string Version, string? CommitHash);
