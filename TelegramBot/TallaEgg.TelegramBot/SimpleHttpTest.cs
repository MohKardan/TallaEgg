using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace TallaEgg.TelegramBot
{
    public class SimpleHttpTest
    {
        public static async Task TestHttpRequestsAsync()
        {
            Console.WriteLine("🔍 Testing HTTP vs HTTPS connectivity...");
            
            var testUrls = new[]
            {
                "http://httpbin.org/get",
                "https://httpbin.org/get",
                "http://api.telegram.org",
                "https://api.telegram.org"
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
                    Console.WriteLine($"   Error Type: {ex.GetType().Name}");
                }
            }
        }
    }
}
