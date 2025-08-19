using TallaEgg.Core.Models;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.Interfaces
{
    public interface ISymbolRepository
    {
        Task<IEnumerable<Symbol>> GetAllAsync();
        Task<IEnumerable<Symbol>> GetActiveAsync();
        Task<IEnumerable<Symbol>> GetByStatusAsync(SymbolStatus status);
        Task<Symbol?> GetByNameAsync(string name);
        Task<Symbol?> GetByIdAsync(Guid id);
        Task<Symbol> AddAsync(Symbol symbol);
        Task<Symbol> UpdateAsync(Symbol symbol);
        Task DeleteAsync(Guid id);
        Task<bool> ExistsAsync(string name);
    }
}
