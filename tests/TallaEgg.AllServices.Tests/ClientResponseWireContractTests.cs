using System.Text.Json;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.Json;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That every response this platform reads with <c>System.Text.Json</c> says how to read it
/// (issue #233).
/// </summary>
/// <remarks>
/// The mirror of <see cref="ClientRequestWireContractTests"/>, which did this for the write side in
/// #241. The two libraries disagree on the read side in a way they do not on the write side:
/// <c>Newtonsoft.Json</c> matches property names case-insensitively with nothing configured, while
/// <c>System.Text.Json</c> handed no options matches them exactly. Every service answers in
/// camelCase and every DTO is PascalCase, so a strict read does not throw — it returns an object
/// with every property left at its default.
///
/// That is what makes the eventual consolidation dangerous rather than tedious. Twenty-two reads
/// still run through <c>JsonConvert.DeserializeObject</c>, tolerant because of a default nobody
/// chose; swapping one for <c>JsonSerializer.Deserialize</c> is a one-word edit that looks like a
/// rename and silently empties the result. In the bot's order and trade history paths that is an
/// empty list where the customer's trades should be.
///
/// This guard is what stands in the way of that edit. It does not migrate anything — the Newtonsoft
/// call sites are out of its scope by construction, because it only matches
/// <c>System.Text.Json</c>'s spelling. It means that the moment one of them is migrated, the new
/// call has to name <see cref="ApiJson.ResponseOptions"/> or this test fails.
/// </remarks>
public class ClientResponseWireContractTests
{
    /// <summary>
    /// Every line of C# the product ships, for the reason
    /// <see cref="ClientRequestWireContractTests.ScanRoots"/> gives: a list of the files that hold
    /// one today goes quiet the first time somebody reads a response somewhere new.
    /// </summary>
    public static TheoryData<string> ScanRoots =>
    [
        "src",
        "TelegramBot",
    ];

    /// <summary>
    /// Files that deserialize with <c>System.Text.Json</c> for something other than a response one
    /// of these APIs served. Adding one is a statement about what the file does.
    /// </summary>
    /// <remarks>
    /// There is one, and it is the same file that anchors the write-side exemption list from the
    /// other end. <c>OutboxProcessorService</c> reads <c>OutboxMessages.Payload</c>, which
    /// <c>OrderMatchingRepository</c> wrote with a bare serializer — PascalCase, because that is
    /// what a bare serializer writes. Giving this read the shared options would not break it today,
    /// since case-insensitive matching reads PascalCase and camelCase alike. It would remove the
    /// only thing keeping the pair honest: the read is strict precisely so that changing the write
    /// side fails loudly instead of settling trades for an empty symbol and zero quantity. That
    /// asymmetry is deliberate and <c>STANDARDS.md</c> §2 states it.
    /// </remarks>
    private static readonly (string Path, string Why)[] NotApiResponses =
    [
        ("src/Order/Orders.Application/Services/OutboxProcessorService.cs",
            "reads OutboxMessages.Payload, which is written PascalCase by a bare serializer and "
                + "must stay case-sensitive so a change to the write side fails loudly"),
    ];

    /// <summary>
    /// How a <c>System.Text.Json</c> deserialize call can be spelled, and only that library's
    /// spellings.
    /// </summary>
    /// <remarks>
    /// The trailing character is what does the work. Newtonsoft's call is
    /// <c>DeserializeObject(</c>/<c>DeserializeObject&lt;</c>, so requiring the bracket immediately
    /// after <c>Deserialize</c> excludes it without naming it — which is the behaviour this guard
    /// wants. The Newtonsoft reads are not offenders; they are the migration this issue did not
    /// do, and the point is to catch the migration, not to fail until it happens.
    /// </remarks>
    private static readonly string[] DeserializeCalls =
    [
        ".Deserialize(",
        ".Deserialize<",
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
    /// A response read with options nobody named is the defect this guards (issue #233). Handed no
    /// options, <c>System.Text.Json</c> matches names exactly, and every API answers camelCase
    /// against PascalCase DTOs — so the read succeeds and returns nothing.
    /// </summary>
    /// <remarks>
    /// The options have to be named on the same line as the call, the same constraint the
    /// write-side guard carries and for the same reason: reading an argument across lines means
    /// parsing C#, and searching the enclosing method instead goes quiet the moment two calls sit
    /// in one. A call split across lines fails here and is fixed by joining it.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ScanRoots))]
    public void ResponseDeserialization_AnywhereInTheProduct_NamesTheSharedOptions(string relativeRoot)
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
            if (NotApiResponses.Any(exempt => exempt.Path.Equals(relative, StringComparison.Ordinal)))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!IsCode(line) || !DeserializeCalls.Any(call => line.Contains(call, StringComparison.Ordinal)))
                {
                    continue;
                }

                calls++;
                if (line.Contains("ApiJson.ResponseOptions", StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{relative}:{index + 1}: {line.Trim()}");
            }
        }

        // Guards the guard: a deserialize call spelled some new way, or an exemption widened until
        // it covers everything, would otherwise leave this sweeping nothing and reporting success.
        Assert.True(
            calls > 0,
            $"No System.Text.Json deserialize call found under {relativeRoot}; this guard has stopped looking at anything.");

        Assert.True(
            offenders.Count == 0,
            "JSON is deserialized without saying what casing to read it in, which is #233. If this "
                + "is a response one of our APIs served, pass ApiJson.ResponseOptions on the same "
                + "line as the call. If it is not — a stored payload, an external contract — add "
                + "the file to NotApiResponses with the reason:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// An exemption that no longer names a real file is an exemption nobody is reading, and it
    /// would silently widen the moment that path came back for some other reason.
    /// </summary>
    [Fact]
    public void NotApiResponses_EveryExemptFile_StillExists()
    {
        var missing = NotApiResponses
            .Where(exempt => !File.Exists(Path.Combine(RepoRoot(), exempt.Path)))
            .Select(exempt => $"  {exempt.Path} — exempt because it {exempt.Why}")
            .ToList();

        Assert.True(
            missing.Count == 0,
            "NotApiResponses exempts a file that is not there any more; drop the entry:"
                + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// What a strict read actually does to a real response body, so the cost of the migration this
    /// issue did not do is written down as behaviour rather than as a warning.
    /// </summary>
    /// <remarks>
    /// Not a regression guard — it asserts what <c>System.Text.Json</c> does, not what our code
    /// does. It is here because "silently returns a default-valued object" is the whole reason the
    /// guard above exists, and a sentence saying so is easy to disbelieve where a failing
    /// assertion is not. The body is the shape every one of the three APIs answers in.
    /// </remarks>
    [Fact]
    public void AStrictRead_OfACamelCaseResponse_SucceedsAndReturnsNothing()
    {
        const string body = """{"success":true,"message":"ok","data":{"symbol":"MAUA/IRT","buyPrice":18237000}}""";

        var strict = JsonSerializer.Deserialize<ApiResponse<Dictionary<string, object>>>(body);

        // No exception, no null result — and nothing in it.
        Assert.NotNull(strict);
        Assert.False(strict!.Success);
        Assert.Null(strict.Message);
        Assert.Null(strict.Data);
    }

    /// <summary>The same body through the shared options, which is what every call site now names.</summary>
    [Fact]
    public void TheSharedOptions_OfTheSameResponse_ReadItAll()
    {
        const string body = """{"success":true,"message":"ok","data":{"symbol":"MAUA/IRT","buyPrice":18237000}}""";

        var parsed = JsonSerializer.Deserialize<ApiResponse<Dictionary<string, object>>>(body, ApiJson.ResponseOptions);

        Assert.NotNull(parsed);
        Assert.True(parsed!.Success);
        Assert.Equal("ok", parsed.Message);
        Assert.NotNull(parsed.Data);
        Assert.Equal("MAUA/IRT", parsed.Data!["symbol"].ToString());
    }
}
