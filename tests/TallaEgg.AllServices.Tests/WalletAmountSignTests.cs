using System.Reflection;
using TallaEgg.Core.ErrorHandling;
using Wallet.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Every method on <see cref="WalletEntity"/> that moves an amount must refuse a non-positive one.
///
/// <para>
/// <c>LockBalance</c> did not (#122). Its two lines are <c>LockedBalance += amount</c> and
/// <c>Balance -= amount</c>, so a negative argument runs them backwards: the reservation shrinks
/// and the spendable balance grows. "Locking" minus a million adds a million.
/// </para>
///
/// <para>
/// The method carries a long comment explaining why the <i>sufficiency</i> rule is absent — the
/// credit ceiling lives in a separate <c>CREDIT_</c> row this entity cannot see, so only the caller
/// can evaluate it. That reasoning is correct and still stands. It is about <i>how much</i> is
/// available, and it had been quietly extended to cover <i>which direction the arithmetic runs</i>,
/// which it says nothing about. <c>IncreaseBalance</c>, <c>DecreaseBalance</c> and
/// <c>ConsumeLockedBalance</c> had the check the whole time; only the two halves of the lock pair
/// were missing it.
/// </para>
///
/// <para>
/// <b>Why the reflection test below matters more than the explicit ones:</b> nobody wrote the check
/// wrongly here — a method was added without it and nothing noticed. Tests naming each method
/// reproduce exactly that gap for the next method someone adds. The reflection test covers methods
/// that do not exist yet, which is the only kind of coverage that would have caught this one.
/// </para>
/// </summary>
public class WalletAmountSignTests
{
    private static WalletEntity NewWallet() =>
        WalletEntity.Create(Guid.NewGuid(), "IRT");

    // ── The specific defect ─────────────────────────────────────────────────────

    /// <summary>
    /// The exact shape of #122: locking a negative amount raised the spendable balance.
    /// </summary>
    [Fact]
    public void LockingANegativeAmount_DoesNotMintMoney()
    {
        var wallet = NewWallet();
        wallet.IncreaseBalance(1_000m);

        Assert.Throws<BusinessRuleException>(() => wallet.LockBalance(-1_000_000m));

        Assert.Equal(1_000m, wallet.Balance);
        Assert.Equal(0m, wallet.LockedBalance);
    }

    /// <summary>
    /// The mirror operation, which was guarded in WalletRepository but not in the entity.
    /// </summary>
    [Fact]
    public void UnlockingANegativeAmount_DoesNotDrainTheBalance()
    {
        var wallet = NewWallet();
        wallet.IncreaseBalance(1_000m);
        wallet.LockBalance(400m);

        Assert.Throws<BusinessRuleException>(() => wallet.UnLockBalance(-1_000m));

        Assert.Equal(600m, wallet.Balance);
        Assert.Equal(400m, wallet.LockedBalance);
    }

    /// <summary>
    /// Zero is refused too. It is not a money bug on its own, but a zero lock produces an order
    /// with no collateral behind it — the situation the C-5 ordering exists to prevent — and it is
    /// reachable: a sell of 0.004 MAUA rounds to 0.00 at the asset's two decimal places.
    /// </summary>
    [Fact]
    public void LockingZero_IsRefusedRatherThanTreatedAsANoOp()
    {
        var wallet = NewWallet();
        wallet.IncreaseBalance(1_000m);

        Assert.Throws<BusinessRuleException>(() => wallet.LockBalance(0m));

        Assert.Equal(1_000m, wallet.Balance);
        Assert.Equal(0m, wallet.LockedBalance);
    }

    /// <summary>A positive lock still works exactly as before.</summary>
    [Fact]
    public void APositiveLock_MovesTheAmountFromBalanceToLocked()
    {
        var wallet = NewWallet();
        wallet.IncreaseBalance(1_000m);

        wallet.LockBalance(250m);

        Assert.Equal(750m, wallet.Balance);
        Assert.Equal(250m, wallet.LockedBalance);
    }

    // ── The one that covers methods nobody has written yet ──────────────────────

    /// <summary>
    /// Fails naming any public method taking a single <see cref="decimal"/> amount that accepts a
    /// negative or zero value.
    ///
    /// <para>
    /// Reflection rather than a list of names on purpose. A list is a copy of what exists today,
    /// and #122 was not a method with a wrong check — it was a method with no check that no list
    /// happened to mention.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(-1_000_000)]
    [InlineData(0)]
    public void EveryAmountTakingMethod_RefusesANonPositiveAmount(decimal badAmount)
    {
        var methods = typeof(WalletEntity)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Where(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(decimal);
            })
            .ToList();

        Assert.NotEmpty(methods);

        var accepted = new List<string>();

        foreach (var method in methods)
        {
            // Funded well past the bad amount, so nothing is refused for lack of balance and the
            // only reason to throw is the sign.
            var wallet = NewWallet();
            wallet.IncreaseBalance(10_000_000m);
            wallet.LockBalance(5_000_000m);

            try
            {
                method.Invoke(wallet, new object[] { badAmount });
                accepted.Add(method.Name);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is BusinessRuleException)
            {
                // Refused, which is the point.
            }
        }

        Assert.True(accepted.Count == 0,
            $"These WalletEntity methods accepted an amount of {badAmount}, which lets a caller " +
            "move money in the wrong direction or record a no-op reservation:" + Environment.NewLine +
            string.Join(Environment.NewLine, accepted.Select(n => "  WalletEntity." + n)) +
            Environment.NewLine +
            "Add `if (amount <= 0) throw new BusinessRuleException(...)` as the first statement.");
    }
}
