using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TallaEgg.Core.Json;

/// <summary>
/// The JSON shape this platform writes its outbound request bodies in: camelCase, chosen rather
/// than inherited (issue #241).
/// </summary>
/// <remarks>
/// Every service publishes a camelCase schema — the rule in <c>STANDARDS.md</c> §2, guarded by
/// <c>JsonNamingPolicyConsistencyTests</c>. The four typed clients that call those services used
/// to pass no serializer options at all, and neither library defaults to the published shape:
/// <c>System.Text.Json</c> writes the member name as declared when it is given no
/// <see cref="JsonSerializerOptions"/>, and <c>Newtonsoft.Json</c> always does. Fifteen of the
/// twenty-two outbound bodies therefore went out in PascalCase, and every one of them was accepted
/// only because the receiving side binds case-insensitively.
///
/// The seven that already matched were the accident, not the rule: they serialize anonymous objects
/// built from local variables, and a C# local is camelCase, so <c>new { symbol, buyPrice }</c>
/// happened to produce the published names. Promoting one of those bodies to a named DTO — a change
/// with no visible risk in it — would have flipped it to PascalCase silently. Naming the options at
/// every call site is what removes that trapdoor.
///
/// <b>Two objects, because there are two libraries.</b> Which serializer the platform standardises
/// on is issue #233's question and is deliberately left open here; this type settles only the
/// casing, so each library keeps an entry written in its own vocabulary. Both describe the same
/// wire shape.
///
/// <b>Nothing here is a deserialization setting.</b> The read side is #233's, and the clients'
/// existing <c>PropertyNameCaseInsensitive</c> reads are untouched.
///
/// Both members are shared, process-wide instances and must be treated as read-only.
/// <see cref="RequestOptions"/> enforces that for itself — <see cref="JsonSerializerOptions"/>
/// throws once it is frozen — while <see cref="JsonSerializerSettings"/> has no equivalent, so
/// mutating <see cref="NewtonsoftRequestSettings"/> would reach every caller in the process.
/// </remarks>
public static class ApiJson
{
    /// <summary>
    /// What a <c>System.Text.Json</c> request body is written with.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonSerializerDefaults.Web"/> is the same profile
    /// <c>Microsoft.AspNetCore.Http.Json.JsonOptions</c> gives the three hosts, so a body written
    /// with it carries exactly the names the endpoint's published schema declares. Naming the
    /// framework's own profile rather than setting <c>PropertyNamingPolicy</c> by hand keeps the
    /// two sides tied to one definition instead of two that happen to agree.
    /// </remarks>
    public static JsonSerializerOptions RequestOptions { get; } = BuildRequestOptions();

    /// <summary>
    /// What a <c>Newtonsoft.Json</c> request body is written with.
    /// </summary>
    /// <remarks>
    /// A <see cref="DefaultContractResolver"/> carrying a <see cref="CamelCaseNamingStrategy"/>,
    /// not the <c>CamelCasePropertyNamesContractResolver</c> the issue suggests. That subclass
    /// turns on <c>ProcessDictionaryKeys</c> and <c>OverrideSpecifiedNames</c> as well, and
    /// <see cref="JsonSerializerDefaults.Web"/> does neither: it leaves <c>DictionaryKeyPolicy</c>
    /// unset and lets an explicit wire name win over the policy. Matching the web profile is the
    /// whole purpose of this type, so the resolver that matches it is the one to use — no request
    /// body carries a dictionary today, and that is a reason the difference is cheap to get right
    /// now, not a reason to leave it wrong.
    ///
    /// The resolver is held on this single instance because a contract resolver caches the
    /// contracts it builds; a fresh one per call would throw that cache away.
    /// </remarks>
    public static JsonSerializerSettings NewtonsoftRequestSettings { get; } = BuildNewtonsoftRequestSettings();

    private static JsonSerializerOptions BuildRequestOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // Frozen up front rather than on first use, so a later mutation fails here instead of
        // depending on whether anything has serialized yet.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private static JsonSerializerSettings BuildNewtonsoftRequestSettings() => new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy(),
        },
    };
}
