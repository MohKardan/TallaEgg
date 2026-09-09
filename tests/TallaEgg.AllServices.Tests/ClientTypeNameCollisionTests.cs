using System.Reflection;
using TallaEgg.Core.DTOs;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That no type the bot's clients declare shares a name with one in <c>TallaEgg.Core</c>
/// (issue #267).
/// </summary>
/// <remarks>
/// This exists because a duplicated name switches the compiler off, and the compiler is what three
/// deletions in a row relied on. <c>CancelActiveOrdersResponseDto</c> was declared in both places
/// with an identical shape; deleting either copy still built, because the name simply rebound to
/// the other one and a live call site changed type in silence. That was found by accident while
/// doing #266 — the build was expected to fail and did not.
///
/// The pair is specific and so is this guard. <c>Orders.Api/Program.cs</c> imports both
/// <c>TallaEgg.Core.DTOs.Order</c> and <c>TallaEgg.TelegramBot.Infrastructure.Clients</c>, which is
/// what turned the duplicate into an ambiguity there — it needed a <c>using</c> alias to say which
/// type it meant, and that alias was load-bearing: removing it produced two <c>CS0104</c> errors.
/// A wider guard over every pair of assemblies in the solution would flag names that are supposed
/// to repeat across bounded contexts, so this one covers the two namespaces that are actually
/// imported together and have actually collided.
///
/// Reflection rather than a source scan, unlike its sibling guards: both assemblies are referenced
/// by this project, so the types can be asked directly and a declaration spelled some unusual way
/// cannot hide from it.
/// </remarks>
public class ClientTypeNameCollisionTests
{
    /// <summary>Every public type the bot declares in its clients namespace.</summary>
    private static Type[] BotClientTypes() =>
        typeof(OrderApiClient).Assembly
            .GetExportedTypes()
            .Where(t => t.Namespace == typeof(OrderApiClient).Namespace)
            .ToArray();

    /// <summary>Every public type in the shared kernel, whatever namespace it sits in.</summary>
    private static Type[] CoreTypes() =>
        typeof(ApiResponse<>).Assembly.GetExportedTypes();

    /// <summary>
    /// A name declared on both sides is a name the compiler can no longer be trusted about.
    /// </summary>
    /// <remarks>
    /// Nested and generic types are compared on <see cref="MemberInfo.Name"/>, which carries the
    /// arity suffix for generics — so <c>ApiResponse`1</c> and a hypothetical non-generic
    /// <c>ApiResponse</c> would not be reported against each other. They cannot be ambiguous to the
    /// compiler either, for the same reason, so the two agree.
    /// </remarks>
    [Fact]
    public void NoBotClientType_SharesItsNameWith_AnyTypeInTheSharedKernel()
    {
        var coreNames = CoreTypes()
            .GroupBy(t => t.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().FullName ?? g.Key, StringComparer.Ordinal);

        var collisions = BotClientTypes()
            .Where(t => coreNames.ContainsKey(t.Name))
            .Select(t => $"  {t.FullName} collides with {coreNames[t.Name]}")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            collisions.Count == 0,
            "A type name is declared both in the bot's clients namespace and in TallaEgg.Core "
                + "(issue #267). Any file importing both namespaces then needs a using alias to say "
                + "which one it means, and — worse — deleting either declaration still compiles, "
                + "because the name rebinds to the other and the call site changes type silently. "
                + "Declare it once, in TallaEgg.Core, and let the bot use that:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, collisions));
    }

    /// <summary>
    /// Guards the guard: if the namespace filter ever stops matching, the test above would sweep an
    /// empty set and report success.
    /// </summary>
    [Fact]
    public void TheGuard_IsActuallyLookingAtSomething()
    {
        Assert.NotEmpty(BotClientTypes());
        Assert.NotEmpty(CoreTypes());
    }
}
