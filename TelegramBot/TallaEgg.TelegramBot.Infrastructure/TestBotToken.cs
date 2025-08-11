using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TallaEgg.TelegramBot
{
    public class TestBotToken
    {
        public static async Task<bool> TestTokenAsync(string token)
        {
            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync($"https://api.telegram.org/bot{token}/getMe");
                var content = await response.Content.ReadAsStringAsync();
                
                Console.WriteLine($"Status Code: {response.StatusCode}");
                Console.WriteLine($"Response: {content}");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<dynamic>(content);
                    Console.WriteLine($"✅ Bot is valid: {result.ok}");
                    Console.WriteLine($"Bot Username: {result.result.username}");
                    Console.WriteLine($"Bot Name: {result.result.first_name}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ Bot token is invalid or expired");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error testing bot token: {ex.Message}");
                return false;
            }
        }
    }
}
