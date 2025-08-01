namespace Orders.Core;

public interface IPriceRepository
{
    Task<Price?> GetByAssetAsync(string asset);
    Task<Price> CreateAsync(Price price);
    Task<Price> UpdateAsync(Price price);
    Task<IEnumerable<Price>> GetAllAsync();
} 