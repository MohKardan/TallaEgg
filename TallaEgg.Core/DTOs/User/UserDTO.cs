using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.Enums.User;

namespace TallaEgg.Core.DTOs.User
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public long TelegramId { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActiveAt { get; set; }
        public bool IsActive { get; set; }
        public UserStatus Status { get; set; }
    }
}
