using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace TallaEgg.TelegramBot.Infrastructure
{
    public class NetworkTest
    {
        public static async Task TestConnectivityAsync()
        {
            Console.WriteLine("🌐 Testing network connectivity...");
            
            var testUrls = new[]
            {
                "https://api.telegram.org",
                "https://httpbin.org/get",
                "https://google.com"
            };

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            foreach (var url in testUrls)
            {
                try
                {
                    Console.WriteLine($"Testing {url}...");
                    var response = await client.GetAsync(url);
                    Console.WriteLine($"✅ {url} - Status: {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ {url} - Error: {ex.Message}");
                }
            }

            Console.WriteLine("Network test completed.");
        }
    }
}
