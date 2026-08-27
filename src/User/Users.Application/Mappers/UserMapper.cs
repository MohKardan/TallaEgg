using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Mappers;
using Users.Core;

namespace Users.Application.Mappers
{
    public class UserMapper : BaseMapper<User, UserDto>
    {
        public override UserDto Map(User entity)
        {
            if (entity == null) return null;

            return new UserDto
            {
                Id = entity.Id,
                TelegramId = entity.TelegramId,
                Username = entity.Username,
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                Status = entity.Status,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                LastActiveAt = entity.LastActiveAt,
                PhoneNumber = entity.PhoneNumber,
                Role = entity.Role
            };
        }

        /// <summary>
        /// Rebuilds the entity from the DTO. Mirrors <see cref="Map"/> field for field; anything the
        /// entity holds that the DTO does not carry cannot be restored here and is left at its
        /// default, so the result is only safe to use as a projection, never to save.
        /// </summary>
        public override User MapBack(UserDto dto)
        {
            if (dto == null) return null;

            return new User
            {
                Id = dto.Id,
                TelegramId = dto.TelegramId,
                Username = dto.Username,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Status = dto.Status,
                IsActive = dto.IsActive,
                CreatedAt = dto.CreatedAt,
                LastActiveAt = dto.LastActiveAt,
                PhoneNumber = dto.PhoneNumber,
                Role = dto.Role
            };
        }
    }
}
