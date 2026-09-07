using System.Text.Json;
using Newtonsoft.Json;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Json;
using TallaEgg.Core.Requests.User;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That the typed clients write their request bodies in the casing the endpoint's schema declares
/// (issue #241).
/// </summary>
/// <remarks>
/// This is the half of the wire contract the two existing guards cannot see.
/// <see cref="JsonNamingPolicyConsistencyTests"/> looks at what a service serves;
/// <see cref="RequestDtoWireContractTests"/> looks at whether a DTO round-trips against itself.
/// Nothing compared a client's outgoing body with the schema of the endpoint it posts to, which is
/// why fifteen of the twenty-two outbound bodies could go out in PascalCase against camelCase
/// schemas without a single test noticing.
///
/// The two tests below close that loop from opposite ends. The source scan says every call site
/// chooses its casing rather than inheriting it; the name comparison says the casing it chooses is
/// the one the framework binds and the schema is generated from. Together with the sibling guard's
/// promise that no service moves off that default, "the client writes what the schema declares"
/// holds for every typed body.
/// </remarks>
public class ClientRequestWireContractTests
{
    /// <summary>
    /// Every line of C# the product ships, not the two <c>Clients/</c> directories issue #241
    /// measured.
    /// </summary>
    /// <remarks>
    /// Sweeping the whole trees and gating the exceptions, the same way
    /// <see cref="JsonNamingPolicyConsistencyTests"/> does and for the reason it gives: a list of
    /// the places that hold one today goes quiet the first time somebody puts one somewhere new,
    /// and passing vacuously after a refactor is the failure a guard like this is most prone to.
    /// The four clients live in two directories now; nothing stops a fifth being written beside the
    /// service it calls, and that one would reintroduce #241 with this test still green.
    /// </remarks>
    public static TheoryData<string> ScanRoots =>
    [
        "src",
        "TelegramBot",
    ];

    /// <summary>
    /// Files that serialize JSON for something other than a request body one of these APIs binds.
    /// Adding one is a statement about what the file does, not a formatting decision.
    /// </summary>
    /// <remarks>
    /// The first is the one that matters. <c>OrderMatchingRepository</c> writes a
    /// <see cref="TradeDto"/> into <c>OutboxMessages.Payload</c> with a bare serializer, and
    /// <c>OutboxProcessorService</c> reads it back case-<em>sensitively</em> — that pair has no
    /// schema between it and no tolerance either, so moving one side to camelCase would turn every
    /// payload written before the deployment and not yet settled into a settlement for an empty
    /// symbol and zero quantity. <c>STANDARDS.md</c> §2 already says so; this keeps the guard from
    /// being the thing that argues otherwise.
    /// </remarks>
    private static readonly (string Path, string Why)[] NotRequestBodies =
    [
        ("src/Order/Orders.Infrastructure/OrderMatchingRepository.cs",
            "writes OutboxMessages.Payload, which OutboxProcessorService reads back case-sensitively"),
        ("src/TallaEgg/TallaEgg.Core/Services/TelegramLoggerService.cs",
            "posts to a third-party notification relay, whose field names it does not get to choose"),
        ("src/Order/Orders.Api/Program.cs",
            "formats a response DTO into a log line"),
        ("src/Order/Orders.Application/OrderService.cs",
            "formats a list of orders into a log line"),
    ];

    /// <summary>
    /// Every named type a client serializes into a request body, paired with whether Newtonsoft is
    /// the library that writes it. Both halves are needed: the wire names come from the library's
    /// naming strategy, and the two libraries reach camelCase by separate implementations.
    /// </summary>
    /// <remarks>
    /// The anonymous bodies — seven in <c>OrderApiClient</c>, one in <c>UsersApiClient</c>, four in
    /// <c>AffiliateApiClient</c> — have no type to name here, and are covered by the source scan
    /// instead. That is the more useful guarantee for them anyway: what was wrong with the
    /// anonymous bodies was never their names but that nothing decided their casing, and seven of
    /// them read as correct today only because a C# local is camelCase.
    /// </remarks>
    public static TheoryData<Type, bool> TypedRequestBodies => new()
    {
        // UsersApiClient — Newtonsoft.
        { typeof(RegisterUserRequest), true },
        { typeof(UpdatePhoneRequest), true },
        { typeof(UpdateUserStatusRequest), true },

        // WalletApiClient — System.Text.Json. TradeDto is the settlement body.
        { typeof(WalletRequest), false },
        { typeof(TradeDto), false },

        // OrderApiClient — System.Text.Json.
        { typeof(OrderDto), false },
        { typeof(NotifyMatchingEngineRequest), false },
    };

    /// <summary>
    /// How a serialize call can be spelled. <c>JsonSerializerOptions</c> and
    /// <c>JsonSerializer.Deserialize</c> both contain the word, so each token carries the leading
    /// dot and the opening bracket with it. The generic spellings are listed because a call written
    /// <c>Serialize&lt;T&gt;(…)</c> would otherwise slip past unseen, which is the one way a guard
    /// like this fails quietly instead of loudly.
    /// </summary>
    private static readonly string[] SerializeCalls =
    [
        ".Serialize(",
        ".Serialize<",
        ".SerializeObject(",
        ".SerializeObject<",
    ];

    private static readonly string[] SharedRequestOptions =
    [
        "ApiJson.RequestOptions",
        "ApiJson.NewtonsoftRequestSettings",
    ];

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TallaEgg.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private static bool IsCode(string line)
    {
        var trimmed = line.TrimStart();
        return !trimmed.StartsWith("//", StringComparison.Ordinal)
            && !trimmed.StartsWith('*');
    }

    /// <summary>
    /// A request body whose casing nothing chose is the defect (issue #241). Handed no options,
    /// both serializers write the member name as it is declared in C#, so a call that passes none
    /// puts PascalCase on the wire against a camelCase schema — and is accepted anyway, because the
    /// receiving side binds case-insensitively. Fifteen of the twenty-two call sites were in that
    /// state; the seven that were not were anonymous objects built from locals, right by
    /// coincidence and one ordinary refactor away from being wrong.
    /// </summary>
    /// <remarks>
    /// The options have to be named on the same line as the call. That is a real constraint on how
    /// these lines are written, and it is the price of a guard that cannot pass vacuously: reading
    /// the argument across lines means parsing C#, and searching the enclosing method instead would
    /// go quiet the moment two calls sat in one. A call split across lines fails loudly here and is
    /// fixed by joining it.
    ///
    /// Scoped to explicit serialize calls, which is every outbound body these clients build today.
    /// <c>PostAsJsonAsync</c> and its siblings are not covered and do not need to be — they
    /// serialize with <c>JsonSerializerDefaults.Web</c> already, which is the shape this asks for.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ScanRoots))]
    public void RequestSerialization_AnywhereInTheProduct_NamesTheSharedOptions(string relativeRoot)
    {
        var root = Path.Combine(RepoRoot(), relativeRoot);
        Assert.True(Directory.Exists(root), $"{relativeRoot} does not exist; update the root list.");

        var offenders = new List<string>();
        var calls = 0;

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            // Build output carries copies of source-generated and referenced code.
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = Path.GetRelativePath(RepoRoot(), file).Replace(Path.DirectorySeparatorChar, '/');
            if (NotRequestBodies.Any(exempt => exempt.Path.Equals(relative, StringComparison.Ordinal)))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!IsCode(line) || !SerializeCalls.Any(call => line.Contains(call, StringComparison.Ordinal)))
                {
                    continue;
                }

                calls++;
                if (SharedRequestOptions.Any(options => line.Contains(options, StringComparison.Ordinal)))
                {
                    continue;
                }

                offenders.Add($"{relative}:{index + 1}: {line.Trim()}");
            }
        }

        // Guards the guard: a serialize call spelled some new way, or an exemption widened until it
        // covers everything, would otherwise leave this sweeping nothing and reporting success.
        Assert.True(
            calls > 0,
            $"No serialize call found under {relativeRoot}; this guard has stopped looking at anything.");

        Assert.True(
            offenders.Count == 0,
            "JSON is serialized without saying what casing to write it in, which is #241. If this is "
                + "a request body one of our APIs binds, pass ApiJson.RequestOptions "
                + "(System.Text.Json) or ApiJson.NewtonsoftRequestSettings (Newtonsoft) on the same "
                + "line as the call. If it is not — a log line, a stored payload, an external "
                + "contract — add the file to NotRequestBodies with the reason:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// An exemption that no longer names a real file is an exemption nobody is reading, and it
    /// would silently widen the moment that path came back for some other reason.
    /// </summary>
    [Fact]
    public void NotRequestBodies_EveryExemptFile_StillExists()
    {
        var missing = NotRequestBodies
            .Where(exempt => !File.Exists(Path.Combine(RepoRoot(), exempt.Path)))
            .Select(exempt => $"  {exempt.Path} — exempt because it {exempt.Why}")
            .ToList();

        Assert.True(
            missing.Count == 0,
            "NotRequestBodies exempts a file that is not there any more; drop the entry:"
                + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// The other end of the loop: the names a client writes are the names its endpoint publishes.
    /// The server side is <c>Microsoft.AspNetCore.Http.Json.JsonOptions</c> exactly as the framework
    /// hands it to a minimal API — read from the framework rather than restated here, so this
    /// compares two independent answers rather than one constant against itself.
    /// </summary>
    /// <remarks>
    /// It earns its keep on the Newtonsoft rows. The two libraries implement camelCase separately,
    /// each with its own rule for a name opening on consecutive capitals, and nothing guarantees
    /// they stay in step across a package upgrade. No DTO here carries a name that would show a
    /// difference today; this is what notices the first one that does.
    ///
    /// It rests on <see cref="JsonNamingPolicyConsistencyTests"/> for the step it cannot take
    /// itself — that no deployed service moves its own serializer off that default, and that no
    /// attribute pins a name past it. With both, the published schema and the framework default are
    /// the same thing, and comparing against one compares against the other.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TypedRequestBodies))]
    public void TypedRequestBody_AsWrittenByItsClient_CarriesTheNamesTheServerBinds(Type type, bool newtonsoft)
    {
        var probe = Activator.CreateInstance(type);
        Assert.NotNull(probe);

        var asTheServerBindsIt = new Microsoft.AspNetCore.Http.Json.JsonOptions().SerializerOptions;
        var declared = TopLevelNames(
            System.Text.Json.JsonSerializer.Serialize(probe, type, asTheServerBindsIt));

        var written = TopLevelNames(newtonsoft
            ? JsonConvert.SerializeObject(probe, ApiJson.NewtonsoftRequestSettings)
            : System.Text.Json.JsonSerializer.Serialize(probe, type, ApiJson.RequestOptions));

        // Only that the schema side has something to compare against, so a type that serialized to
        // "{}" could not pass by writing nothing. Not an exact property count: a member both sides
        // agree to leave off the wire — a [JsonIgnore], an indexer — is a consistent contract, and
        // failing on it would report a wire mismatch that is not one.
        Assert.NotEmpty(declared);

        var unpublished = written.Except(declared, StringComparer.Ordinal).ToList();
        var unwritten = declared.Except(written, StringComparer.Ordinal).ToList();

        Assert.True(
            unpublished.Count == 0 && unwritten.Count == 0,
            $"{type.Name} goes on the wire under names its endpoint's schema does not declare, which "
                + "is #241. The server binds them case-insensitively today and would stop the moment "
                + "that tolerance is tightened:" + Environment.NewLine
                + $"  written, not declared: {Render(unpublished)}" + Environment.NewLine
                + $"  declared, not written: {Render(unwritten)}");
    }

    private static List<string> TopLevelNames(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().Select(property => property.Name).ToList();
    }

    private static string Render(IReadOnlyCollection<string> names) =>
        names.Count == 0 ? "(none)" : string.Join(", ", names);
}
