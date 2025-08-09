using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.Mappers
{
    public interface IMapper<TEntity, TDto>
    {
        TDto Map(TEntity entity);
        TEntity MapBack(TDto dto);

        IEnumerable<TDto> MapList(IEnumerable<TEntity> entities);
        IEnumerable<TEntity> MapBackList(IEnumerable<TDto> dtos);
    }
}
