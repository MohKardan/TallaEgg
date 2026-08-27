using System.Reflection;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A matched trade must be created by one path, not two (issue #40).
///
/// Two places used to call <c>Trade.Create</c>:
/// <c>MatchingEngineService.CreateMakerTakerTrade</c> with fee rates of <c>0.000</c>, and
/// <c>OrderMatchingRepository.CreateTrade</c> with <c>0.001/0.002</c>. The engine built the first,
/// threw it away, and immediately called the second through <c>ExecuteAtomicMatchAsync</c>. The
/// version stored and queued for settlement was always the second.
///
/// Why this was more than duplicate code: reading the engine said the fees were zero, while the
/// stored trade carried fees — denominated in the quote currency. Every settlement was then refused
/// with "fee exceeds trade amount", and the cause was hidden in code that never ran. A rate defined
/// in one place but read from another is exactly what produced the bug.
///
/// This test is structural rather than behavioural: running a match cannot prove a second path does
/// not exist — the second path ran back then too and broke no test, because its output was
/// discarded. Counting call sites is the only thing that catches its return.
/// </summary>
public class SingleTradeCreationPathTests
{
    /// <summary>
    /// The assemblies that could create a trade. <c>Orders.Core</c> is included because
    /// <c>Trade.Create</c> is defined there, and a second factory alongside it would be just as much
    /// of a problem as one in a higher layer.
    /// </summary>
    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(MatchingEngineService).Assembly,   // Orders.Application
        typeof(OrderMatchingRepository).Assembly, // Orders.Infrastructure
        typeof(Trade).Assembly                    // Orders.Core
    ];

    private static readonly MethodInfo TradeCreate =
        typeof(Trade).GetMethod(nameof(Trade.Create), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("Trade.Create not found — did it get renamed?");

    [Fact]
    public void TradeCreate_IsCalledFromExactlyOnePlace()
    {
        var callers = FindCallersOf(TradeCreate);

        Assert.Equal(
            [$"{nameof(OrderMatchingRepository)}.CreateTrade"],
            callers);
    }

    /// <summary>
    /// Fee rates must also be defined only on that one path. A rate of its own anywhere else brings
    /// back the contradiction where the code said one number and the database showed another — even
    /// if that code creates no trade and only computes a rate for reporting.
    /// </summary>
    [Fact]
    public void MatchingEngine_DoesNotDeclareItsOwnFeeRates()
    {
        var engineFields = typeof(MatchingEngineService)
            .GetFields(BindingFlags.Instance | BindingFlags.Static |
                       BindingFlags.Public | BindingFlags.NonPublic)
            .Select(f => f.Name);

        Assert.DoesNotContain(engineFields, n => n.Contains("FeeRate", StringComparison.OrdinalIgnoreCase));

        var engineMethods = typeof(MatchingEngineService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Static |
                        BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.DeclaringType == typeof(MatchingEngineService));

        Assert.DoesNotContain(engineMethods, m => m.ReturnType == typeof(Trade));
    }

    // ── IL scanning ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The sorted Type.Method names of everywhere that calls <paramref name="target"/>.
    ///
    /// Async methods compile into a generated state machine, so the real call sits inside
    /// <c>MoveNext</c> on a hidden nested type. That type's name — for example
    /// <c>&lt;ExecuteAtomicMatchAsync&gt;d__12</c> — is mapped back to the original type and method so
    /// the failure message stays readable.
    /// </summary>
    private static string[] FindCallersOf(MethodInfo target) =>
        ProductionAssemblies
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                          BindingFlags.Public | BindingFlags.NonPublic |
                                          BindingFlags.DeclaredOnly)
                              .Cast<MethodBase>()
                              .Concat(t.GetConstructors(BindingFlags.Instance | BindingFlags.Static |
                                                        BindingFlags.Public | BindingFlags.NonPublic)))
            .Where(m => Calls(m, target))
            .Select(DisplayName)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    private const byte OpCall = 0x28;
    private const byte OpCallvirt = 0x6F;

    /// <summary>
    /// Does <paramref name="caller"/>'s body contain a <c>call</c> or <c>callvirt</c> to
    /// <paramref name="target"/>?
    ///
    /// The scan is approximate: it looks for any <c>0x28</c> or <c>0x6F</c> byte and resolves the
    /// following four bytes as a metadata token, without tracking instruction lengths exactly. It
    /// could therefore mistake an operand byte for an opcode. That is harmless here: such a
    /// coincidence would have to resolve to <c>Trade.Create</c> precisely to produce a false
    /// positive, and it never misses a real call — which is the side that matters, since this test's
    /// job is to catch a second path coming back.
    /// </summary>
    private static bool Calls(MethodBase caller, MethodInfo target)
    {
        byte[]? il;
        try { il = caller.GetMethodBody()?.GetILAsByteArray(); }
        catch { return false; } // متدهای abstract/extern بدنه ندارند

        if (il is null) return false;

        var typeArgs = caller.DeclaringType?.IsGenericType == true
            ? caller.DeclaringType.GetGenericArguments()
            : null;
        var methodArgs = caller.IsGenericMethod ? caller.GetGenericArguments() : null;

        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != OpCall && il[i] != OpCallvirt) continue;

            var token = BitConverter.ToInt32(il, i + 1);

            try
            {
                if (caller.Module.ResolveMethod(token, typeArgs, methodArgs) is MethodInfo m &&
                    m.MetadataToken == target.MetadataToken &&
                    m.Module == target.Module)
                {
                    return true;
                }
            }
            catch
            {
                // Not a valid token, so this byte was not an opcode at all. Skip it.
            }
        }

        return false;
    }

    /// <summary>
    /// Maps <c>&lt;X&gt;d__7.MoveNext</c> back to <c>DeclaringType.X</c>, so the test output names the
    /// real method rather than the compiler-generated one.
    /// </summary>
    private static string DisplayName(MethodBase method)
    {
        var type = method.DeclaringType!;
        var name = method.Name;

        if (type.Name.StartsWith('<') && type.DeclaringType is not null)
        {
            name = type.Name[1..type.Name.IndexOf('>')];
            type = type.DeclaringType;
        }

        return $"{type.Name}.{name}";
    }
}
