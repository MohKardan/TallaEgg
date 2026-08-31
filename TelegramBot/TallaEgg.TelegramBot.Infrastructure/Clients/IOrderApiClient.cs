using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Order;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public interface IOrderApiClient
{
    
    Task<ApiResponse<PagedResult<OrderHistoryDto>>> GetUserOrdersAsync(Guid userId, int pageNumber = 1, int pageSize = 10);
    Task<ApiResponse<PagedResult<TradeHistoryDto>>> GetUserTradesAsync(Guid userId, int pageNumber = 1, int pageSize = 10);
    Task<ApiResponse<List<OrderHistoryDto>>> GetUserActiveOrdersAsync(Guid userId);
    Task<ApiResponse<List<OrderHistoryDto>>> GetAllActiveOrdersAsync();
    Task<(bool success, string message)> SubmitOrderAsync(OrderDto order);
    Task<(bool success, string message)> CancelOrderAsync(Guid orderId);

    // ── Dealer model (issue #48) ────────────────────────────────────────────────
    // Prices cross this boundary per base unit, toman per gram; the mesghal conversion happens only
    // in the bot's display layer.

    /// <summary>
    /// Publishes the admin's quote. Places nothing in the book and locks no collateral.
    ///
    /// <para>
    /// A quote too far from the one currently published is <b>not</b> published: it comes back in
    /// <c>pending</c> for an admin to confirm (issue #158). <c>success</c> is still true, because
    /// nothing went wrong — the price is simply waiting on a human. Callers must check
    /// <c>pending</c> before telling anyone the price is live.
    /// </para>
    /// </summary>
    Task<(bool success, string message, PendingQuoteDto? pending)> PublishQuoteAsync(
        string symbol, decimal buyPrice, decimal sellPrice, Guid publishedByUserId);

    /// <summary>The active quote for a symbol, or null if none has been published.</summary>
    Task<QuoteDto?> GetActiveQuoteAsync(string symbol);
    /// <summary>Published quotes for a symbol, newest first, including replaced ones.</summary>
    Task<PagedResult<QuoteDto>> GetQuoteHistoryAsync(string symbol, int pageNumber = 1, int pageSize = 5);

    /// <summary>Best bid and ask, used by the market-order path.</summary>
    Task<ApiResponse<BestPricesDto>> GetBestPricesAsync(string symbol);

    /// <summary>A customer fills a quote. No price is sent; the server reads it from the quote.</summary>
    Task<(bool success, string message)> AcceptQuoteAsync(
        Guid userId, string symbol, OrderSide side, decimal quantity);
    
    /// <summary>
    /// Cancels all of a user's active orders.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <param name="reason">Optional cancellation reason.</param>
    /// <returns>Success, a message, and how many orders were cancelled.</returns>
    Task<(bool success, string message, int cancelledCount)> CancelAllUserActiveOrdersAsync(Guid userId, string? reason = null);
    
    Task<ApiResponse<bool>> NotifyMatchingEngineAsync(NotifyMatchingEngineRequest request);

    // ── Automatic quotes (issue #90) ────────────────────────────────────────────

    /// <summary>A symbol's current automatic-quote settings.</summary>
    /// <summary>
    /// Quotes the plausibility band is holding until an admin answers (issue #158). Null means
    /// Orders could not be reached, which is not the same as nothing waiting.
    /// </summary>
    Task<IReadOnlyList<PendingQuoteDto>?> GetPendingQuotesAsync();

    /// <summary>Approves a held quote, publishing it now. Fails if it was already answered or has expired.</summary>
    Task<(bool success, string message)> ApprovePendingQuoteAsync(Guid pendingQuoteId, Guid adminUserId);

    /// <summary>Rejects a held quote. Nothing is published and the previous quote stands.</summary>
    Task<(bool success, string message)> RejectPendingQuoteAsync(Guid pendingQuoteId, Guid adminUserId);

    Task<AutoQuoteSettingsDto?> GetAutoQuoteSettingsAsync(string symbol);

    /// <summary>Changes a symbol's automatic-quote spread.</summary>
    Task<(bool success, string message)> UpdateAutoQuoteSpreadAsync(string symbol, decimal spreadPercent, Guid updatedByUserId);

    /// <summary>Turns a symbol's automatic quoting on or off.</summary>
    Task<(bool success, string message)> SetAutoQuoteEnabledAsync(string symbol, bool isEnabled, Guid updatedByUserId);

    // ── Symbol enable/disable ───────────────────────────────────────────────────

    /// <summary>The symbols currently tradable.</summary>
    Task<List<string>> GetActiveSymbolsAsync();

    /// <summary>Enables or disables a symbol.</summary>
    Task<(bool success, string message)> SetSymbolActiveAsync(string symbol, bool isActive, Guid updatedByUserId);

    // ── Profit and loss (issue #93) ─────────────────────────────────────────────

    /// <summary>The user's position and profit or loss across every symbol they have traded.</summary>
    Task<ApiResponse<PositionsResponseDto>> GetPositionsAsync(Guid userId);
}