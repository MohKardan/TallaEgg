namespace Orders.Core;

public interface IQuoteRepository
{
    /// <summary>The active quote for a symbol, or null if the admin has not published one yet.</summary>
    Task<Quote?> GetActiveAsync(string symbol);

    /// <summary>
    /// Publishes a new quote and deactivates the symbol's previous one, both in a single transaction.
    ///
    /// Atomicity matters: if the deactivate and the insert run separately there is an instant when
    /// either two quotes are active or none is. In the first case it is undefined which price the
    /// customer trades at; in the second a perfectly valid trade is refused.
    /// </summary>
    Task<Quote> PublishAsync(Quote quote);

    /// <summary>
    /// Published quotes for a symbol, newest first, including ones already replaced.
    ///
    /// The superseded rows are the point. Deactivating rather than deleting was a deliberate
    /// choice when quotes were introduced (#48), so that it stays possible to see which price
    /// was in force when any given trade happened. Without a way to read them, that history
    /// existed but nobody could look at it.
    /// </summary>
    Task<(IReadOnlyList<Quote> Items, int TotalCount)> GetHistoryAsync(string symbol, int pageNumber, int pageSize);

    /// <summary>
    /// Distinct symbols that currently have an active published quote. Nothing else needs this
    /// across every symbol at once — <see cref="GetActiveAsync"/> already answers "does this one
    /// symbol have a quote". It exists for the startup check in issue #73: a symbol with an
    /// active quote that isn't in Dealer mode is a contradiction only a query across all symbols
    /// can see.
    /// </summary>
    Task<IReadOnlyList<string>> GetActiveSymbolsAsync();
}
