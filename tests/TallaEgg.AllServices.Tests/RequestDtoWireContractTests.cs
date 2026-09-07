using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Requests.User;
using TallaEgg.Core.Requests.Wallet;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That every request body the three APIs bind means the same thing on the wire as it does in C#
/// (issue #235).
/// </summary>
/// <remarks>
/// The sibling guard in <see cref="JsonNamingPolicyConsistencyTests"/> reads source text. These two
/// read behaviour, which is the stronger test and the reason both exist: #229 and #235 were each
/// invisible in C# and visible only on the wire, and a text scan can only ever catch the spellings
/// somebody thought to list. Here the serializer answers for itself.
///
/// Everything is driven off <c>JsonTypeInfo</c> — the serializer's own contract for a type — rather
/// than off property names, so the tests do not assume the naming convention they are guarding.
///
/// <c>AbandonOutboxMessageRequest</c> and <c>UpdateUserRoleRequest</c> are absent because they are
/// declared inside the API projects' top-level statements, which this project does not reference.
/// Every request DTO that lives in <c>TallaEgg.Core</c> is here.
/// </remarks>
public class RequestDtoWireContractTests
{
    /// <summary>
    /// Every type bound as a request body by a <c>MapPost</c>/<c>MapPut</c> in Orders, Users or
    /// Wallet. A new endpoint that takes a new body type belongs on this list.
    /// </summary>
    public static TheoryData<Type> RequestDtos =>
    [
        typeof(OrderDto),
        typeof(AcceptQuoteRequest),
        typeof(PublishQuoteRequest),
        typeof(ResolvePendingQuoteRequest),
        typeof(UpdateAutoQuoteSpreadRequest),
        typeof(SetAutoQuoteEnabledRequest),
        typeof(SetSymbolActiveRequest),
        typeof(TradeDto),
        typeof(RegisterUserRequest),
        typeof(UpdatePhoneRequest),
        typeof(UpdateUserStatusRequest),
        typeof(WalletRequest),
    ];

    /// <summary>
    /// What the three hosts actually read and write: <c>JsonSerializerDefaults.Web</c> is what
    /// <c>Microsoft.AspNetCore.Http.Json.JsonOptions</c> defaults to, camelCase and
    /// case-insensitive both.
    /// </summary>
    private static JsonSerializerOptions WebOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    /// <summary>
    /// A value distinguishable from the type's default and from every other member's value, as the
    /// JSON text the serializer would emit for it. Null for a type this test cannot fabricate — a
    /// collection or a nested object — which simply leaves that member out of the payload.
    /// </summary>
    private static string? SentinelJson(Type declared, int index)
    {
        var type = Nullable.GetUnderlyingType(declared) ?? declared;

        if (type == typeof(string))
        {
            return JsonSerializer.Serialize($"value-{index}");
        }

        if (type == typeof(Guid))
        {
            return JsonSerializer.Serialize(new Guid(index + 1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]));
        }

        if (type == typeof(bool))
        {
            // Only one value differs from the default, so two bool members cannot get distinct
            // sentinels. That costs nothing: both tests below compare per member, not across.
            return "true";
        }

        if (type == typeof(DateTime))
        {
            return JsonSerializer.Serialize(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(index));
        }

        if (type.IsEnum)
        {
            // Web options write enums as numbers. An enum whose every member is 0 has no value
            // distinguishable from its default, so it is skipped rather than guessed at.
            var distinct = Enum.GetValues(type)
                .Cast<object>()
                .FirstOrDefault(value => Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0);

            return distinct is null
                ? null
                : Convert.ToInt64(distinct, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
        }

        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)
            || type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            return (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }

    /// <summary>
    /// Each wire name the serializer declares for <paramref name="type"/>, paired with a sentinel
    /// value, skipping the members no sentinel can be built for.
    /// </summary>
    private static List<(string WireName, string Sentinel)> Sentinels(Type type, JsonSerializerOptions options)
    {
        var members = new List<(string, string)>();
        var index = 0;

        foreach (var property in options.GetTypeInfo(type).Properties)
        {
            var sentinel = SentinelJson(property.PropertyType, index);
            if (sentinel is not null)
            {
                members.Add((property.Name, sentinel));
            }

            index++;
        }

        return members;
    }

    private static string Payload(IEnumerable<(string WireName, string Sentinel)> members)
    {
        var json = new StringBuilder("{");
        json.AppendJoin(",", members.Select(m => $"{JsonSerializer.Serialize(m.WireName)}:{m.Sentinel}"));
        return json.Append('}').ToString();
    }

    private static string RawValue(string json, string wireName)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(wireName, out var value)
            ? value.GetRawText()
            : "<absent>";
    }

    /// <summary>
    /// Fill every field with a value of its own, read the body, write it back out, and require every
    /// field to still hold what was sent. One assertion, and it covers the whole class of defect:
    /// a field that is read under one name and written under another fails it, and so does a pair
    /// of names sharing one value, because the pair collapses to whichever was read last and the
    /// other field comes back changed.
    /// </summary>
    /// <remarks>
    /// The crossed names of #235 failed exactly here: sending <c>symbol</c> and <c>asset</c>
    /// separately returned both as whichever arrived last.
    /// </remarks>
    [Theory]
    [MemberData(nameof(RequestDtos))]
    public void RequestDto_SerializedAfterBeingRead_ReturnsEveryFieldUnchanged(Type type)
    {
        var options = WebOptions();
        var members = Sentinels(type, options);
        Assert.NotEmpty(members);

        var sent = Payload(members);
        var bound = JsonSerializer.Deserialize(sent, type, options);
        Assert.NotNull(bound);

        var returned = JsonSerializer.Serialize(bound, type, options);

        var lost = members
            .Where(m => RawValue(returned, m.WireName) != m.Sentinel)
            .Select(m => $"  {m.WireName}: sent {m.Sentinel}, came back {RawValue(returned, m.WireName)}")
            .ToList();

        Assert.True(
            lost.Count == 0,
            $"{type.Name} does not survive a round trip through JSON, so a client cannot trust that "
                + "the server read what it sent:" + Environment.NewLine + string.Join(Environment.NewLine, lost)
                + Environment.NewLine + "Sent: " + sent + Environment.NewLine + "Back: " + returned);
    }

    /// <summary>
    /// One value, one wire name. Sending a single field at a time and watching which C# properties
    /// move says whether two names are really two fields: two names that disturb the same
    /// properties are one field with a spare spelling, and which of them wins then depends on the
    /// order of the keys in the request body, which carries no meaning in JSON.
    /// </summary>
    /// <remarks>
    /// A name that disturbs nothing is the other half of the same rule — it is written out but
    /// cannot be read back in, so the schema advertises a field the server ignores.
    ///
    /// Only members a sentinel can be built for are probed; a collection or nested object is out of
    /// scope here, as <see cref="SentinelJson"/> says. None of the twelve DTOs has one today.
    /// </remarks>
    [Theory]
    [MemberData(nameof(RequestDtos))]
    public void RequestDtoWireName_EachOne_GovernsExactlyOneValue(Type type)
    {
        var options = WebOptions();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        var untouched = JsonSerializer.Deserialize("{}", type, options);
        Assert.NotNull(untouched);

        // Compared as JSON, not with Equals: Equals(object, object) is reference equality for
        // anything that does not override it, so a property holding an inline-initialised list
        // would read as "moved" on every probe. Every probe then looks like it changes something,
        // and the inert-name assertion below retires itself without saying so.
        string Rendered(PropertyInfo property, object instance) =>
            JsonSerializer.Serialize(property.GetValue(instance), property.PropertyType, options);

        var governs = new Dictionary<string, string>(StringComparer.Ordinal);
        var inert = new List<string>();

        foreach (var (wireName, sentinel) in Sentinels(type, options))
        {
            var probed = JsonSerializer.Deserialize(Payload([(wireName, sentinel)]), type, options);
            Assert.NotNull(probed);

            var moved = properties
                .Where(p => !string.Equals(Rendered(p, probed), Rendered(p, untouched), StringComparison.Ordinal))
                .Select(p => p.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();

            if (moved.Count == 0)
            {
                inert.Add(wireName);
                continue;
            }

            governs[wireName] = string.Join("+", moved);
        }

        var shared = governs
            .GroupBy(entry => entry.Value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"  {string.Join(" and ", group.Select(e => $"\"{e.Key}\""))} both set {group.Key}")
            .ToList();

        Assert.True(
            shared.Count == 0,
            $"{type.Name} publishes more than one wire name for the same value, which is #235. "
                + "Whichever key comes last in the request body wins, silently:"
                + Environment.NewLine + string.Join(Environment.NewLine, shared));

        Assert.True(
            inert.Count == 0,
            $"{type.Name} writes fields it cannot read back — the schema advertises them, the server "
                + "ignores them: " + string.Join(", ", inert));
    }
}
