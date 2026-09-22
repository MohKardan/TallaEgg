using System.Text.RegularExpressions;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// No screen outside the gold-specific templates may write a currency next to a price itself
/// (issue #304).
///
/// <para>
/// The bot's Persian text had «تومان» beside every figure it showed, which was true of every
/// symbol until XAU/USD. PR #321 replaced those with <c>PersianFormat.QuoteUnit(symbol)</c> in the
/// six templates on the ounce's path — and its own review then found five more screens still
/// saying toman, including the first price screen a customer sees and the trade history the
/// executed-trade message points them at. A test per screen would have missed the sixth just as
/// easily, so this sweeps the source instead.
/// </para>
///
/// <para>
/// <c>Constants.cs</c> is exempt: its remaining occurrences are the gold-only templates, which
/// name mesghals and grams and are unreachable for any other symbol, plus two templates with no
/// call sites at all. Everything else must take its currency from the symbol.
/// </para>
/// </summary>
public class NoHardcodedCurrencyInBotScreensTests
{
    private const string Toman = "تومان";

    /// <summary>
    /// The literal next to a value: either an interpolation (<c>{price} تومان</c>) or a format
    /// placeholder (<c>{3} تومان</c>). A mention inside a comment or a prose sentence is not what
    /// this is looking for.
    /// </summary>
    private static readonly Regex CurrencyAfterAValue =
        new(@"\}\s*" + Toman, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void NoBotScreenOutsideTheGoldTemplates_WritesTheCurrencyItself()
    {
        var offenders = BotSourceFiles()
            .Where(file => !file.Relative.EndsWith("Constants.cs", StringComparison.Ordinal))
            .Where(file => CurrencyAfterAValue.IsMatch(File.ReadAllText(file.FullPath)))
            .Select(file => file.Relative)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "These screens write a currency beside a value instead of taking it from the symbol, " +
            $"so they would label a dollar price as toman:{Environment.NewLine}" +
            string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// A sweep that reaches nothing passes vacuously. It must see the screens the review of #321
    /// found, which are the ones most likely to regress.
    /// </summary>
    [Fact]
    public void TheSweep_ReachesTheScreensThatShowPrices()
    {
        var reached = BotSourceFiles().Select(f => f.Relative).ToHashSet(StringComparer.Ordinal);

        foreach (var expected in new[]
                 {
                     "Messages/BestPricesMessage.cs",
                     "Messages/OrderConfirmationMessage.cs",
                     "Messages/TradeExecutedMessage.cs",
                     "Messages/PendingQuoteMessage.cs",
                     "Messages/QuoteMessage.cs",
                     "Handlers/TradeListHandler.cs",
                     "Handlers/OrderListHandler.cs",
                     "Handlers/ActiveOrdersHandler.cs",
                     "Handlers/QuoteHistoryHandler.cs"
                 })
        {
            Assert.Contains(expected, reached);
        }
    }

    private static IEnumerable<(string FullPath, string Relative)> BotSourceFiles()
    {
        var botRoot = Path.Combine(RepositoryRoot(), "TelegramBot");

        return Directory.EnumerateFiles(botRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(botRoot, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .Select(path => (path, Path.GetRelativePath(botRoot, path).Replace('\\', '/')))
            // One project holds every handler and message; the prefix is noise in a failure list.
            .Select(file => (file.path, file.Item2.Replace("TallaEgg.TelegramBot.Infrastructure/", "")));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TallaEgg.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
