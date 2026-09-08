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
    /// <c>Type</c> is on the list on the weakest grounds of the eight: <c>CreateOrderAsync</c>
    /// reads it into a log line and never branches on it, and no code under <c>src/Order/</c>
    /// mentions <c>OrderType</c> at all. It is here because it is read, not because it decides
    /// anything.
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
    /// Every way a deployed API can publish <c>HttpValidationProblemDetails</c> as a response
    /// shape. <c>ProducesValidationProblem</c> is the metadata; <c>Results.ValidationProblem</c>
    /// and <c>Results.Problem</c> are the two ways a handler could start genuinely returning one,
    /// at which point the metadata would stop being a lie and this guard would need revisiting
    /// rather than satisfying.
    /// </summary>
    private static readonly string[] ProblemDetailsResponses =
    [
        "ProducesValidationProblem",
        "Results.ValidationProblem",
        "Results.Problem",
    ];

    /// <summary>
    /// The three APIs that are deployed and that the bot calls. <c>Affiliate.Api</c> and
    /// <c>TallaEgg.Api</c> are absent because neither is run.
    /// </summary>
    public static TheoryData<string> DeployedApiRoots =>
    [
        "src/User/Users.Api",
        "src/Wallet/Wallet.Api",
        "src/Order/Orders.Api",
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
