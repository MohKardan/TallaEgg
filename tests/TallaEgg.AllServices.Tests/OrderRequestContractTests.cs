using TallaEgg.Core.DTOs.Order;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That <c>POST /api/orders</c> publishes the request it reads and the error shape it returns
/// (issue #240).
/// </summary>
/// <remarks>
/// Both halves are things the C# cannot show on its own. A property nothing reads compiles, binds
/// and serializes exactly like one that matters, and endpoint metadata has no effect a caller can
/// observe outside the published document — which is why #235, #237 and this issue were all found
/// by walking <c>swagger.json</c> rather than by reading the diff.
///
/// The second test source-scans, like <see cref="JsonNamingPolicyConsistencyTests"/> and for the
/// same reason: the three APIs are top-level statements no test can build a container from.
/// </remarks>
public class OrderRequestContractTests
{
    /// <summary>
    /// Every member of <see cref="OrderDto"/> that <c>POST /api/orders</c> reads. Adding to this
    /// list is a change to the request contract of the platform's order-entry endpoint, not a
    /// formatting decision.
    /// </summary>
    /// <remarks>
    /// <c>Type</c> was on this list on the weakest grounds of the eight: it was read into a log
    /// line and never branched on, and no code under <c>src/Order/</c> mentioned <c>OrderType</c>
    /// at all. It now decides what the order records (issue #250) — which is still not the same as
    /// deciding how the order executes, since this endpoint builds the same resting order whatever
    /// it is sent. See <see cref="OrderDto.Type"/>.
    /// </remarks>
    private static readonly string[] MembersTheEndpointReads =
    [
        nameof(OrderDto.Asset),
        nameof(OrderDto.Amount),
        nameof(OrderDto.Price),
        nameof(OrderDto.UserId),
        nameof(OrderDto.Side),
        nameof(OrderDto.Type),
        nameof(OrderDto.TradingType),
        nameof(OrderDto.Notes),
    ];

    /// <summary>
    /// Publishing a <c>ProblemDetails</c> response shape, in every spelling. The two
    /// <c>Produces…</c> tokens are the metadata — <c>ProducesProblem</c> is not a substring of
    /// <c>ProducesValidationProblem</c>, so both are needed. The two <c>Results.</c> tokens are how
    /// a handler could start genuinely returning one, and each matches its <c>TypedResults.</c>
    /// spelling too, that being the longer string. A handler that does return one is the case where
    /// this guard should be revisited rather than satisfied.
    /// </summary>
    private static readonly string[] ProblemDetailsResponses =
    [
        "ProducesValidationProblem",
        "ProducesProblem",
        "Results.ValidationProblem",
        "Results.Problem",
    ];

    /// <summary>
    /// The three APIs that are deployed and that the bot calls, plus the shared kernel their error
    /// handling lives in. <c>Affiliate.Api</c> and <c>TallaEgg.Api</c> are absent because neither
    /// is run.
    /// </summary>
    /// <remarks>
    /// <c>TallaEgg.Core</c> is on the list for the reason <see cref="JsonNamingPolicyConsistencyTests"/>
    /// gives for the same entry: cross-service concerns get hoisted there, and the error body this
    /// guard is about is already one of them — <c>GlobalExceptionHandler</c> and the repository's
    /// one <c>AddProblemDetails()</c> both live under <c>ErrorHandling/</c>. Scanning only the three
    /// hosts would leave the guard passing vacuously the moment a refusal moves into a shared
    /// helper.
    /// </remarks>
    public static TheoryData<string> DeployedApiRoots =>
    [
        "src/User/Users.Api",
        "src/Wallet/Wallet.Api",
        "src/Order/Orders.Api",
        "src/TallaEgg/TallaEgg.Core",
    ];

    /// <summary>
    /// A bound property nothing reads is worse on this endpoint than on most: <c>Status</c> and
    /// <c>Role</c> name real trading concepts, so a client reading the schema can reasonably
    /// believe it is choosing whether to post as maker or taker. It is not — both are decided
    /// server-side — and the request is accepted with a 200 and no warning.
    /// </summary>
    [Fact]
    public void OrderDto_DeclaredMembers_AreOnlyThoseTheEndpointReads()
    {
        var declared = typeof(OrderDto)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var expected = MembersTheEndpointReads
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, declared);
    }

    /// <summary>
    /// <c>POST /api/orders</c> carried the repository's only <c>.ProducesValidationProblem(400)</c>
    /// while returning <c>{ success, message }</c> from all three of its refusals, so a client
    /// generating its error type from the document found neither <c>title</c> nor <c>errors</c>,
    /// and the member that actually carries the reason was not in the published shape at all.
    /// </summary>
    /// <remarks>
    /// No service returns a <c>ProblemDetails</c> body. The one <c>AddProblemDetails()</c> in the
    /// repository is the fallback <c>UseExceptionHandler()</c> refuses to start without, and
    /// <c>GlobalExceptionHandler</c> handles every exception before it is reached.
    /// </remarks>
    [Theory]
    [MemberData(nameof(DeployedApiRoots))]
    public void ErrorResponseMetadata_InAnyDeployedApi_DoesNotAdvertiseProblemDetails(string relativeRoot)
    {
        var offenders = ScanForTokens(relativeRoot, ProblemDetailsResponses);

        Assert.True(
            offenders.Count == 0,
            "An endpoint publishes a ProblemDetails response shape. Every refusal these services "
                + "send is an ApiResponse<T> envelope or a bare { success, message }, so this "
                + "advertises a body no handler returns (issue #240):"
                + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

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

    private static bool IsCode(string line) =>
        !line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    private static List<string> ScanForTokens(string relativeRoot, params string[] tokens)
    {
        var root = Path.Combine(RepoRoot(), relativeRoot);
        Assert.True(Directory.Exists(root), $"{relativeRoot} does not exist; update the root list.");

        var hits = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            // Build output carries copies of source-generated and referenced code.
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!IsCode(lines[index]))
                {
                    continue;
                }

                if (tokens.Any(token => lines[index].Contains(token, StringComparison.Ordinal)))
                {
                    hits.Add($"{Path.GetRelativePath(RepoRoot(), file)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        return hits;
    }
}
