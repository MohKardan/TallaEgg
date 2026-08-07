namespace Orders.Core;

public interface IAutoQuoteSettingsRepository
{
    /// <summary>
    /// The settings row for a symbol, creating and persisting the disabled-by-default row the
    /// first time it is asked for. The caller never has to special-case "no row yet".
    /// </summary>
    Task<AutoQuoteSettings> GetOrCreateAsync(string symbol);

    Task SaveAsync(AutoQuoteSettings settings);
}
