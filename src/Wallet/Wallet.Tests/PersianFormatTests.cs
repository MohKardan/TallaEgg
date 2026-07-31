using TallaEgg.Core;
using TallaEgg.Core.Utilties;

namespace Wallet.Tests;

/// <summary>
/// Guards the formatting used for every money value shown to users. A regression here
/// silently misrepresents prices and balances in the bot, so it is worth pinning down.
/// </summary>
public class PersianFormatTests
{
    [Fact]
    public void ToPersianDigits_ConvertsLatinDigitsAndLeavesOtherCharacters()
    {
        Assert.Equal("۱۲۳۴۵۶۷۸۹۰", PersianFormat.ToPersianDigits("1234567890"));
        Assert.Equal("نسخه ۱.۰.۰", PersianFormat.ToPersianDigits("نسخه 1.0.0"));
        Assert.Equal("", PersianFormat.ToPersianDigits(null));
    }

    [Fact]
    public void Number_UsesPersianDigitsAndPersianThousandsSeparator()
    {
        var result = PersianFormat.Number(79_500_000m);

        // Persian digits and the Persian thousands separator (U+066C), no Latin ones.
        // Ordinal comparison matters here: the default culture-aware search treats the
        // Persian digit ۰ as equivalent to ASCII 0 and would pass either way.
        Assert.Equal("۷۹٬۵۰۰٬۰۰۰", Strip(result));
        Assert.DoesNotContain(",", result, StringComparison.Ordinal);
        Assert.DoesNotContain("0", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Number_WrapsValueInALeftToRightIsolateSoItDoesNotScrambleInPersianText()
    {
        var result = PersianFormat.Number(1000m);

        // The isolate is what keeps the number from being reordered by the bidi algorithm
        // when embedded in a Persian sentence. It replaced a pair of RLM marks, which made
        // the inside right-to-left — harmless for a single number like this one, and the
        // reason the fault only surfaced on a date, where two number runs swapped places.
        //
        // Ordinal: a culture-sensitive comparison ignores invisible formatting characters,
        // so these assertions would hold whatever the string really contained.
        Assert.StartsWith(PersianFormat.Lri, result, StringComparison.Ordinal);
        Assert.EndsWith(PersianFormat.Pdi, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Number_WithDecimals_UsesPersianDecimalSeparator()
    {
        Assert.Equal("۱۸٬۳۵۲٬۶۴۷٫۸۶", Strip(PersianFormat.Number(18_352_647.86m, 2)));
    }

    [Fact]
    public void Amount_UsesAssetPrecisionAndDropsTrailingZeroDecimals()
    {
        // Toman has no decimals.
        Assert.Equal("۱۴۶٬۸۲۱٬۱۸۳", Strip(PersianFormat.Amount(146_821_183m, CurrenciesConstant.Toman)));

        // Gold has 2 decimals, but a whole number should not show "۸٫۰۰".
        Assert.Equal("۸", Strip(PersianFormat.Amount(8m, CurrenciesConstant.Maua)));

        // A fractional gold amount keeps its decimals.
        Assert.Equal("۸٫۵", Strip(PersianFormat.Amount(8.5m, CurrenciesConstant.Maua)));
    }

    [Fact]
    public void Symbol_ReturnsPersianPairNameInsteadOfLatinCode()
    {
        Assert.Equal("آبشده/تومان", PersianFormat.Symbol(CurrenciesConstant.MAUA_IRT));

        // Unknown pairs still resolve to Persian asset names rather than leaking Latin codes.
        Assert.DoesNotContain("MAUA", PersianFormat.Symbol("MAUA/IRT"));
    }

    [Fact]
    public void Asset_And_Unit_ReturnPersianNames()
    {
        Assert.Equal("تومان", PersianFormat.Asset(CurrenciesConstant.Toman));
        Assert.Equal("آبشده", PersianFormat.Asset(CurrenciesConstant.Maua));
        Assert.Equal("گرم", PersianFormat.Unit(CurrenciesConstant.Maua));
        Assert.Equal("تومان", PersianFormat.Unit(CurrenciesConstant.Toman));
    }

    /// <summary>Removes the direction guards so the visible text can be asserted directly.</summary>
    private static string Strip(string value) => value
        .Replace(PersianFormat.Lri, "")
        .Replace(PersianFormat.Pdi, "")
        .Replace(PersianFormat.Rlm, "");
}
