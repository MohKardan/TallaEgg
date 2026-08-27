using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The Orders columns must hold at least as many decimal places as the assets they store.
///
/// <para>
/// They did not. <c>Orders.Amount</c>, <c>RemainingAmount</c> and <c>Price</c> were
/// <c>decimal(18, 2)</c> while <c>Trades.Quantity</c> and <c>Wallets.Balance</c> were
/// <c>decimal(28, 8)</c>. Gold hid it: MAUA's precision is 2, exactly what the column held. Bitcoin
/// exposed it — its precision is 8, so a sale of <c>2.111 BTC</c> was stored as <c>2.11</c>.
/// </para>
///
/// <para>
/// That one rounding stranded money. Collateral is locked from the quantity the caller supplied
/// (2.111), the trade executes on the quantity that was stored (2.11), and
/// <c>OrderCollateralReconciler</c> recomputes the residue from the stored order — so it computed
/// <c>2.11 - 2.11 = 0</c>, released nothing, and left 0.001 BTC and its rial equivalent locked with
/// no order holding them and no path in the product to get them back.
/// </para>
///
/// <para>
/// <b>Why a model test and not a behavioural one:</b> the tests run against SQLite, which ignores
/// decimal scale entirely — it will happily round-trip 2.111 through a column declared
/// <c>decimal(18, 2)</c>. No test that inserts and reads a row can fail on this, under any provider
/// the suite can run. The scale only bites on SQL Server, in production. So the assertion is made
/// against the configured model instead, where it holds for every provider.
/// </para>
/// </summary>
public class OrderColumnPrecisionTests
{
    /// <summary>Columns holding a quantity or a price of a tradable asset.</summary>
    private static readonly string[] DecimalColumns = ["Amount", "RemainingAmount", "Price"];

    /// <summary>
    /// Every one of them must have at least the scale of the most precise asset on the platform.
    /// Adding a symbol with more decimal places than the column holds re-creates the bug, and this
    /// is what says so.
    /// </summary>
    [Fact]
    public void OrderDecimalColumns_HoldEveryTradableAssetsPrecision()
    {
        var required = CurrenciesConstant.AllTradingPairs.Max(p => p.BaseDecimalPlaces);

        using var context = new OrdersDbContext(
            new DbContextOptionsBuilder<OrdersDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options);

        var order = context.Model.FindEntityType(typeof(Orders.Core.Order))!;

        foreach (var column in DecimalColumns)
        {
            var scale = order.FindProperty(column)!.GetScale();

            Assert.True(scale is not null && scale >= required,
                $"Orders.{column} is configured with scale {scale?.ToString() ?? "(none)"}, but the " +
                $"most precise tradable asset needs {required}. A quantity with more decimal places " +
                "than the column holds is rounded on the way in, while the collateral was locked " +
                "from the value the caller gave — and the difference is locked with nothing holding it.");
        }
    }

    /// <summary>
    /// And they must not be narrower than the wallet they settle against, whatever the assets
    /// happen to need today: an order for a quantity the wallet can hold but the order cannot is
    /// the same defect arriving from the other side.
    /// </summary>
    [Fact]
    public void OrderDecimalColumns_AreNoNarrowerThanTheWalletTheySettleAgainst()
    {
        const int walletScale = 8;   // Wallets.Balance and LockedBalance, decimal(28, 8)

        using var context = new OrdersDbContext(
            new DbContextOptionsBuilder<OrdersDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options);

        var order = context.Model.FindEntityType(typeof(Orders.Core.Order))!;

        foreach (var column in DecimalColumns)
        {
            var scale = order.FindProperty(column)!.GetScale();

            Assert.True(scale is not null && scale >= walletScale,
                $"Orders.{column} has scale {scale?.ToString() ?? "(none)"} against the wallet's " +
                $"{walletScale}. Money moves at the wallet's precision; an order that cannot express " +
                "what the wallet can settle will always leave a remainder behind.");
        }
    }
}
