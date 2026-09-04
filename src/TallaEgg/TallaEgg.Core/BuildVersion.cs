using System.Reflection;
using TallaEgg.Core.DTOs;

namespace TallaEgg.Core;

/// <summary>
/// Reads the version and commit hash out of a built assembly, so a running process can be asked
/// which build it is (issue #218).
/// </summary>
/// <remarks>
/// Nothing has to be generated or wired up for this to have an answer. Every project inherits one
/// <c>VersionPrefix</c> from <c>Directory.Build.props</c> (issue #217), and the .NET SDK appends
/// <c>+&lt;commit-hash&gt;</c> to <c>InformationalVersion</c> on its own whenever it can see a
/// <c>.git</c> directory — no <c>SourceLink</c> package is involved. This class only reads what is
/// already stamped into the assembly.
/// </remarks>
public static class BuildVersion
{
    /// <summary>
    /// The build the current process is running.
    /// </summary>
    /// <remarks>
    /// Read once: an assembly's attributes cannot change while it is loaded, and this is served
    /// from a request path.
    /// </remarks>
    public static BuildVersionDto Current { get; } = Read(Assembly.GetEntryAssembly());

    /// <summary>
    /// Reads <paramref name="assembly"/>'s <see cref="AssemblyInformationalVersionAttribute"/> and
    /// splits it into the version and the commit hash.
    /// </summary>
    /// <param name="assembly">
    /// The assembly to read. <c>null</c> is accepted because
    /// <see cref="Assembly.GetEntryAssembly"/> returns it when the process was not started from a
    /// managed entry point; the fall-back reads this assembly instead, which reports the same
    /// version because every project inherits it from the same place.
    /// </param>
    public static BuildVersionDto Read(Assembly? assembly)
    {
        assembly ??= typeof(BuildVersion).Assembly;

        return Parse(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version);
    }

    /// <summary>
    /// Splits an informational version into its two halves.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Read"/>, and internal rather than private, so the split can be
    /// tested against the strings a build actually produces. Fabricating an assembly per case
    /// would test reflection rather than this.
    /// </remarks>
    /// <param name="informationalVersion">
    /// The attribute's value — <c>1.1.0+ff95e00a327...</c> for a build made inside a git checkout,
    /// <c>1.1.0</c> for one made outside it, and <c>null</c> when the attribute is absent.
    /// </param>
    /// <param name="assemblyVersion">
    /// Fall-back for a missing attribute. Always present, and carries the same three numbers with
    /// a fourth appended.
    /// </param>
    internal static BuildVersionDto Parse(string? informationalVersion, Version? assemblyVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return new BuildVersionDto(assemblyVersion?.ToString() ?? "unknown", null);
        }

        // The separator is SemVer's build-metadata '+', so everything after the first one is the
        // hash. A version with no metadata has no '+' at all and keeps the whole string.
        var separator = informationalVersion.IndexOf('+');
        if (separator < 0)
        {
            return new BuildVersionDto(informationalVersion, null);
        }

        var hash = informationalVersion[(separator + 1)..];

        return new BuildVersionDto(
            informationalVersion[..separator],
            string.IsNullOrWhiteSpace(hash) ? null : hash);
    }
}
