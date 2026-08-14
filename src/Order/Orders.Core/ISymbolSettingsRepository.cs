namespace Orders.Core;

public interface ISymbolSettingsRepository
{
    /// <summary>
    /// The settings row for a symbol, creating and persisting the inactive-by-default row the
    /// first time it is asked for. The caller never has to special-case "no row yet".
    /// </summary>
    Task<SymbolSettings> GetOrCreateAsync(string symbol);

    /// <summary>Symbols currently active, for the bot's symbol picker and the auto-quote loop.</summary>
    Task<IReadOnlyList<string>> GetActiveSymbolsAsync();

    Task SaveAsync(SymbolSettings settings);
}
