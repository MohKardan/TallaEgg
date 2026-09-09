using System.Reflection;
using TallaEgg.Core.DTOs;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That no type in the typed-clients namespace shares a name with one in <c>TallaEgg.Core</c>
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
/// Reflection rather than a source scan, unlike its sibling guards: the assemblies are all
/// referenced by this project, so the types can be asked directly and a declaration spelled some
/// unusual way cannot hide from it. Note that "the clients namespace" is not one project — see
/// <see cref="ClientNamespaceAssemblies"/>, which is the correction this guard needed after its
/// first version covered only half of it.
/// </remarks>
public class ClientTypeNameCollisionTests
{
    /// <summary>
    /// The assemblies that declare types in the clients namespace. There are two, which is the
    /// whole reason this list is written out rather than derived from one anchor type.
    /// </summary>
    /// <remarks>
    /// <c>TallaEgg.TelegramBot.Infrastructure.Clients</c> is not one project's namespace. The bot
    /// declares <c>OrderApiClient</c> and <c>AffiliateApiClient</c> in it; <c>TallaEgg.Infrastructure</c>
    /// declares <c>UsersApiClient</c> and <c>IUsersApiClient</c> in the same namespace from a
    /// different assembly — and that is the half <c>Orders.Api</c> actually resolves
    /// <c>UsersApiClient</c> from. A guard anchored on one assembly covered five of the nine types
    /// and would have stayed green while the other four collided, which is the same shape of
    /// blindness this whole issue is about.
    /// </remarks>
    private static readonly Assembly[] ClientNamespaceAssemblies =
    [
        typeof(OrderApiClient).Assembly,   // TallaEgg.TelegramBot.Infrastructure
        typeof(UsersApiClient).Assembly,   // TallaEgg.Infrastructure
    ];

    private static readonly string ClientNamespace = typeof(OrderApiClient).Namespace!;

    /// <summary>Every public type declared in the clients namespace, from either assembly.</summary>
    private static Type[] ClientTypes() =>
        ClientNamespaceAssemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.Namespace == ClientNamespace)
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

        var collisions = ClientTypes()
            .Where(t => coreNames.ContainsKey(t.Name))
            .Select(t => $"  {t.Assembly.GetName().Name}: {t.FullName} collides with {coreNames[t.Name]}")
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
    /// Guards the guard: the sweep has to reach <em>both</em> assemblies that declare this
    /// namespace, not just the one the anchor type lives in.
    /// </summary>
    /// <remarks>
    /// The first version of this asserted only that the set was non-empty, which it could not fail:
    /// the filter was derived from <c>typeof(OrderApiClient).Namespace</c>, so <c>OrderApiClient</c>
    /// always matched itself however the namespace was renamed. It sat over a guard that was in
    /// fact missing four of the nine types, and said nothing. Counting distinct assemblies is what
    /// that check should have been doing — it fails on exactly the gap that was there.
    /// </remarks>
    [Fact]
    public void TheGuard_Reaches_BothAssembliesThatDeclareTheClientsNamespace()
    {
        var byAssembly = ClientTypes()
            .GroupBy(t => t.Assembly.GetName().Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key!, g => g.Count(), StringComparer.Ordinal);

        Assert.Contains("TallaEgg.TelegramBot.Infrastructure", byAssembly.Keys);
        Assert.Contains("TallaEgg.Infrastructure", byAssembly.Keys);

        // Not just reached — carrying types. An assembly present with nothing in it would mean the
        // namespace filter has drifted away from what these projects actually declare.
        Assert.All(byAssembly, entry => Assert.True(
            entry.Value > 0,
            $"{entry.Key} declares nothing in {ClientNamespace}; the namespace filter has drifted."));

        Assert.NotEmpty(CoreTypes());
    }
}
