using TallaEgg.Core.Utilties;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The deduplication key an admin top-up or deduction carries (issue #157).
///
/// <para>
/// The live path used to send no key at all, so every deposit row held NULL and the same charge
/// sent twice credited twice. Confirmed against the local database before this was written: of
/// 42 deposit and withdrawal rows, all 42 had a null ReferenceId.
/// </para>
/// </summary>
public class AdminAdjustmentKeyTests
{
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly DateTime At = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// The case the issue is about: the wallet committed, the reply was lost, the admin saw no
    /// confirmation and typed the command again seconds later. Two different Telegram messages,
    /// one charge.
    /// </summary>
    [Fact]
    public async Task ARepeatOfTheSameChargeSecondsLaterProducesTheSameKey()
    {
        var first = AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 100m, At);
        var retry = AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 100m, At.AddSeconds(20));

        Assert.Equal(first, retry);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Beyond the window the admin means it: a second top-up of the same amount to the same
    /// customer an hour later is a real charge and has to be allowed through.
    /// </summary>
    [Fact]
    public void TheSameChargeAnHourLaterProducesADifferentKey()
    {
        var morning = AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 100m, At);
        var later = AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 100m, At.AddHours(1));

        Assert.NotEqual(morning, later);
    }

    [Fact]
    public void ADifferentAmountProducesADifferentKey()
    {
        Assert.NotEqual(
            AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 100m, At),
            AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 200m, At));
    }

    /// <summary>
    /// Amount is part of the key as a number, not as the admin happened to type it. Charging 100
    /// twice inside one window is one charge whichever way it was written.
    /// </summary>
    [Fact]
    public void TrailingZerosOnTheAmountDoNotChangeTheKey()
    {
        Assert.Equal(
            AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 100m, At),
            AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 100.00m, At));
    }

    [Fact]
    public void ADifferentCustomerProducesADifferentKey()
    {
        Assert.NotEqual(
            AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 100m, At),
            AdminAdjustmentKey.ForDeposit(Guid.NewGuid(), "CREDIT_MAUA", 100m, At));
    }

    /// <summary>
    /// Credit is per-asset in storage, so charging a customer's gold ledger and their coin ledger
    /// the same amount in the same minute are two separate charges, not a repeat.
    /// </summary>
    [Fact]
    public void ADifferentAssetProducesADifferentKey()
    {
        Assert.NotEqual(
            AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 100m, At),
            AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_SEKE_BAHAR", 100m, At));
    }

    /// <summary>
    /// Topping a customer up and deducting the same amount inside one window are opposite
    /// operations. Sharing a key would make the deduction look like a repeat of the top-up and
    /// silently do nothing, leaving the customer holding money the admin meant to take back.
    /// </summary>
    [Fact]
    public void ADepositAndAWithdrawalOfTheSameAmountDoNotShareAKey()
    {
        Assert.NotEqual(
            AdminAdjustmentKey.ForDeposit(Customer, "IRT", 100m, At),
            AdminAdjustmentKey.ForWithdrawal(Customer, "IRT", 100m, At));
    }

    /// <summary>
    /// The known limitation, pinned rather than hidden: the window is a bucket, not a sliding
    /// window, so a retry that straddles a boundary gets a different key and is not caught. The
    /// exposure is the retry gap divided by the window — a few seconds in five minutes. Change
    /// this test only alongside a decision to pay for prefix querying in the wallet.
    /// </summary>
    [Fact]
    public void ARetryThatStraddlesABucketBoundaryIsNotCaught()
    {
        var justBefore = new DateTime(2026, 8, 31, 12, 4, 58, DateTimeKind.Utc);

        Assert.NotEqual(
            AdminAdjustmentKey.ForDeposit(Customer, "IRT", 100m, justBefore),
            AdminAdjustmentKey.ForDeposit(Customer, "IRT", 100m, justBefore.AddSeconds(4)));
    }

    /// <summary>The key ends up in a database column and a log line; it has to stay printable ASCII.</summary>
    [Fact]
    public void TheKeyIsPrintableAndReasonablyShort()
    {
        var key = AdminAdjustmentKey.ForDeposit(Customer, "CREDIT_MAUA", 1234.5m, At);

        Assert.StartsWith("admin-deposit:", key);
        Assert.All(key, c => Assert.InRange(c, ' ', '~'));
        Assert.True(key.Length < 128, $"key was {key.Length} characters: {key}");
    }
}
