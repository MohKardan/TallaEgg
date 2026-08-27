using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.Mappers
{
    public abstract class BaseMapper<TEntity, TDto> : IMapper<TEntity, TDto>
    {
        public abstract TDto? Map(TEntity? entity);
        public abstract TEntity? MapBack(TDto? dto);

        /// <summary>
        /// For an entity the caller has already established exists — one it just created, or one
        /// it guarded with a not-found check. A null back from <see cref="Map"/> here would mean
        /// the mapper broke its own contract, not that the entity was missing, so it throws rather
        /// than passing a null on to code that has no reason to expect one.
        /// </summary>
        public TDto MapRequired(TEntity entity)
        {
            return Map(entity) ?? throw new InvalidOperationException(
                $"{GetType().Name}.Map returned null for a {typeof(TEntity).Name} that is not null.");
        }

        // OfType drops anything Map returned null for, so a list never carries holes a caller
        // would have to check for one by one.
        public IEnumerable<TDto> MapList(IEnumerable<TEntity> entities)
        {
            return entities?.Select(Map).OfType<TDto>().ToList() ?? new List<TDto>();
        }

        public IEnumerable<TEntity> MapBackList(IEnumerable<TDto> dtos)
        {
            return dtos?.Select(MapBack).OfType<TEntity>().ToList() ?? new List<TEntity>();
        }
    }
}
