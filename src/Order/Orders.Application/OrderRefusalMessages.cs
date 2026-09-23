namespace Orders.Application;

/// <summary>
/// Refusal texts a customer reads, shared by the paths that can refuse for the same cause.
/// </summary>
/// <remarks>
/// Persian because these reach the customer through the bot; see STANDARDS.md section 1.
///
/// <para>
/// This exists because the two paths drifted. <c>POST /api/orders</c> built its insufficient-funds
/// refusal by appending the wallet client's message, which on that branch is the client's success
/// text, so the customer was told «موجودی ناکافی: اعتبار و موجودی کاربر بررسی شد» — a log status
/// line pasted onto a reason (issue #284). Naming the sentence once keeps the two paths from
/// saying different things about the same refusal again.
/// </para>
///
/// <para>
/// The sentences themselves now live in <see cref="TallaEgg.Core.RefusalMessages"/>, because the
/// bot refuses for the same causes before it ever submits an order and could not see this class:
/// it is <c>internal</c>, and making it public was not an option because
/// <c>Orders.Application</c> references the bot's Infrastructure project, so a reference back would
/// be a cycle (issue #290). This class stays as the name its own callers use.
/// </para>
/// </remarks>
internal static class OrderRefusalMessages
{
    /// <summary>The customer's balance and credit together do not cover the order.</summary>
    public const string InsufficientFunds = TallaEgg.Core.RefusalMessages.InsufficientFunds;

    /// <summary>The balance check did not complete, so nothing is known about the funds.</summary>
    public const string BalanceCheckFailed = TallaEgg.Core.RefusalMessages.BalanceCheckFailed;
}
