using TallaEgg.Core.Enums.User;

namespace TallaEgg.Core.Requests.User
{
    /// <summary>
    /// Request model for updating user status
    /// </summary>
    public class UpdateUserStatusRequest
    {
        public long TelegramId { get; set; }
        public UserStatus NewStatus { get; set; }
    }

}
