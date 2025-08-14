namespace TallaEgg.Core.Requests.User
{
    /// <summary>
    /// Request model for updating user phone number
    /// </summary>
    public class UpdatePhoneRequest
    {
        /// <summary>
        /// New phone number to set for the user (required)
        /// </summary>
        public string PhoneNumber { get; set; }
        
        /// <summary>
        /// Telegram ID of the user to update (required)
        /// </summary>
        public long TelegramId { get; set; }
    }
}
