using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.Mappers
{
    public abstract class BaseMapper<TEntity, TDto> : IMapper<TEntity, TDto>
    {
        public abstract TDto Map(TEntity entity);
        public abstract TEntity MapBack(TDto dto);

        public IEnumerable<TDto> MapList(IEnumerable<TEntity> entities)
        {
            return entities?.Select(Map).ToList() ?? new List<TDto>();
        }

        public IEnumerable<TEntity> MapBackList(IEnumerable<TDto> dtos)
        {
            return dtos?.Select(MapBack).ToList() ?? new List<TEntity>();
        }
    }
}
