using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.Requests.User
{
    /// <summary>
    /// Request model for user registration
    /// </summary>
    public class RegisterUserRequest
    {
        /// <summary>
        /// Telegram ID of the user (required)
        /// </summary>
        public long TelegramId { get; set; }
        
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
        /// Invitation code for registration (required)
        /// </summary>
        public string InvitationCode { get; set; }
    }
}
