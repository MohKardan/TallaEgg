using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.Mappers
{
    public interface IMapper<TEntity, TDto>
    {
        /// <summary>
        /// Maps an entity, or returns null when given null. Callers already rely on that — every
        /// UserService lookup returns UserDto? and passes a repository result straight through —
        /// so the signature says it rather than leaving each caller to discover it.
        /// </summary>
        TDto? Map(TEntity? entity);

        TEntity? MapBack(TDto? dto);

        IEnumerable<TDto> MapList(IEnumerable<TEntity> entities);
        IEnumerable<TEntity> MapBackList(IEnumerable<TDto> dtos);
    }
}
