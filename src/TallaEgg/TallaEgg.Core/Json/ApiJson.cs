using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TallaEgg.Core.Json;

/// <summary>
/// The JSON shape this platform puts on the wire and reads back off it: camelCase, chosen rather
/// than inherited — outbound bodies since #241, inbound responses since #233.
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
/// <b>System.Text.Json is the house serializer</b> (issue #233), which is why the read side has one
/// entry and the write side has two. <see cref="NewtonsoftRequestSettings"/> stays because twelve
/// outbound bodies are still written with Newtonsoft and each has to name its casing until it is
/// migrated; there is deliberately no Newtonsoft read counterpart, because the twenty-two reads
/// still on that library are the migration #233 did not do, and giving them a shared settings
/// object would make leaving them there look like a decision.
///
/// <b>The read side is the one with a trapdoor in it.</b> Newtonsoft matches property names
/// case-insensitively with nothing configured; <c>System.Text.Json</c> handed no options matches
/// them exactly and returns an object with every property at its default rather than throwing. So
/// migrating a <c>DeserializeObject</c> call to <c>Deserialize</c> is a one-word edit that can
/// empty a customer's trade history in silence. <see cref="ResponseOptions"/> is what a migrated
/// call names, and <c>ClientResponseWireContractTests</c> is what fails if it does not.
///
/// <b>No member can be reshaped by one of its callers.</b> That matters more here than it looks:
/// these objects are reached from every request path in the platform, so a single line setting some
/// unrelated option on one of them would change what every other caller puts on the wire — or now
/// accepts back. <see cref="RequestOptions"/> and <see cref="ResponseOptions"/> are both frozen, and
/// <see cref="JsonSerializerOptions"/> throws on a write once it is.
/// <see cref="JsonSerializerSettings"/> has no equivalent, so
/// <see cref="NewtonsoftRequestSettings"/> hands out a fresh instance instead — see the note there
/// for why that costs nothing.
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
    public static JsonSerializerOptions RequestOptions { get; } = BuildWebOptions();

    /// <summary>
    /// What a <c>System.Text.Json</c> response body is read with (issue #233).
    /// </summary>
    /// <remarks>
    /// The same <see cref="JsonSerializerDefaults.Web"/> profile as <see cref="RequestOptions"/>,
    /// which on the read side means case-insensitive name matching — the thing that lets a
    /// PascalCase DTO read the camelCase every service answers in. Handed no options at all,
    /// <c>System.Text.Json</c> matches exactly: it does not throw on a camelCase body, it returns
    /// an object with every property at its default. Eighteen call sites each spelled
    /// <c>PropertyNameCaseInsensitive = true</c> by hand before this existed, which worked and left
    /// nothing saying it had to.
    ///
    /// <b>A separate object from <see cref="RequestOptions"/>, though the two are built the same
    /// way today.</b> They are not the same decision: writing needs the naming policy, reading
    /// needs the case-insensitive matching, and the profile happens to carry both. Aliasing one to
    /// the other would mean a later change to what this platform writes silently changing what it
    /// accepts, in a direction nobody looked at.
    ///
    /// <b>Not for anything but an API response.</b> The outbox payload is read strictly on purpose
    /// — see <c>OutboxProcessorService</c> and <c>STANDARDS.md</c> §2. Reading it with these
    /// options would work and would remove the only thing that makes a change to the write side
    /// fail loudly.
    /// </remarks>
    public static JsonSerializerOptions ResponseOptions { get; } = BuildWebOptions();

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
    /// A new <see cref="JsonSerializerSettings"/> every time, over one shared resolver. The
    /// settings object is what a caller could mutate and there is no way to freeze it, so nobody
    /// gets a reference anyone else is holding; the resolver is what caches the contracts it
    /// builds, and keeping that one static is what makes the fresh settings object free. Newtonsoft
    /// builds a serializer from the settings on every <c>SerializeObject</c> call regardless, so
    /// this adds one small allocation to an operation that is about to make an HTTP request.
    /// </remarks>
    public static JsonSerializerSettings NewtonsoftRequestSettings => new()
    {
        ContractResolver = CamelCaseResolver,
    };

    /// <summary>
    /// Shared because a contract resolver caches the contracts it builds, and safe to share because
    /// it is never handed to a caller — only read through <see cref="NewtonsoftRequestSettings"/>.
    /// </summary>
    private static readonly IContractResolver CamelCaseResolver = new DefaultContractResolver
    {
        NamingStrategy = new CamelCaseNamingStrategy(),
    };

    /// <summary>
    /// One profile, two callers. Named for the profile rather than for either direction, because
    /// both <see cref="RequestOptions"/> and <see cref="ResponseOptions"/> are built from it and a
    /// name that claimed one of them would be wrong for the other.
    /// </summary>
    private static JsonSerializerOptions BuildWebOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // Frozen up front rather than on first use, so a later mutation fails here instead of
        // depending on whether anything has serialized yet.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
