using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.Enums.User;

namespace TallaEgg.Core.DTOs.User
{
    /// <summary>
    /// Data transfer object for user information
    /// </summary>
    public class UserDto
    {
        /// <summary>
        /// Unique identifier for the user
        /// </summary>
        public Guid Id { get; set; }
        
        /// <summary>
        /// Telegram ID of the user
        /// </summary>
        public long TelegramId { get; set; }
        
        /// <summary>
        /// User's phone number (optional)
        /// </summary>
        public string? PhoneNumber { get; set; }
        
        /// <summary>
        /// Telegram username (optional)
        /// </summary>
        public string? Username { get; set; }
        
        /// <summary>
        /// User's first name (optional)
        /// </summary>
        public string? FirstName { get; set; }
        
        /// <summary>
        /// User's last name (optional)
        /// </summary>
        public string? LastName { get; set; }
        
        /// <summary>
        /// Date and time when the user was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Date and time when the user was last active (optional)
        /// </summary>
        public DateTime? LastActiveAt { get; set; }
        
        /// <summary>
        /// Indicates if the user account is active
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Current status of the user account
        /// </summary>
        public UserStatus Status { get; set; }
    }
}
