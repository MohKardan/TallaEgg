namespace TallaEgg.Core;

/// <summary>
/// Refusal sentences a customer reads, shared by every path that can refuse for the same cause.
/// </summary>
/// <remarks>
/// <para>
/// Persian, because these reach the customer through the bot — see STANDARDS.md section 1, which
/// keeps Persian for what a user sees and English for everything else.
/// </para>
///
/// <para>
/// This lives in the shared kernel rather than in either service because the same refusal is
/// produced on both sides of the wire: <c>Orders.Application</c> refuses an order or a quote fill,
/// and the bot refuses before it ever submits one. <c>Orders.Application.OrderRefusalMessages</c>
/// held the sentence first (issue #284) but is <c>internal</c>, and could not simply be made public
/// for the bot to use: <c>Orders.Application</c> references the bot's Infrastructure project, so a
/// reference the other way would be a cycle.
/// </para>
///
/// <para>
/// The point of naming a refusal once is that the paths cannot drift apart while saying they refuse
/// for the same reason. They already had: the service said «موجودی یا اعتبار شما برای این معامله
/// کافی نیست» while the bot said «❌ موجودی شما برای این سفارش کافی نیست» and then appended the
/// wallet client's own status line as the explanation (issues #284, #290).
/// </para>
///
/// <para>
/// **A refusal a customer reads must name a cause they can act on.** That is the whole reason these
/// are separate constants rather than one generic message: "your funds are short" and "we could not
/// check your funds" ask for completely different things from the person reading them, and joining
/// them sent customers to top up an account that was never the problem.
/// </para>
/// </remarks>
public static class RefusalMessages
{
    /// <summary>
    /// The customer's balance and credit together do not cover the order.
    ///
    /// <para>
    /// "balance or credit", not "balance": credit in one currency can back a position in the other,
    /// so a customer with an empty balance may still be able to trade. See
    /// <c>docs/decisions/004-credit-is-cross-asset.md</c>.
    /// </para>
    /// </summary>
    public const string InsufficientFunds = "موجودی یا اعتبار شما برای این معامله کافی نیست.";

    /// <summary>
    /// The balance check itself did not complete — the wallet service could not be reached, or
    /// answered an error. Nothing is known about the customer's funds.
    ///
    /// <para>
    /// Never substitute <see cref="InsufficientFunds"/> here. The customer has done nothing wrong,
    /// there is nothing for them to top up, and telling them otherwise costs them a trip to the
    /// gold shop.
    /// </para>
    /// </summary>
    public const string BalanceCheckFailed = "بررسی موجودی انجام نشد. لطفاً دوباره تلاش کنید.";
}
