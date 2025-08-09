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
                PhoneNumber = entity.PhoneNumber
            };
        }

        /// <summary>
        ///  // todo باید کامل شود
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
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
                PhoneNumber = dto.PhoneNumber,
                LastActiveAt = dto.LastActiveAt
            };
        }
    }
}
