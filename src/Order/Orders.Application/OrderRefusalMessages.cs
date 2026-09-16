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
/// </remarks>
internal static class OrderRefusalMessages
{
    /// <summary>The customer's balance and credit together do not cover the order.</summary>
    public const string InsufficientFunds = "موجودی یا اعتبار شما برای این معامله کافی نیست.";
}
