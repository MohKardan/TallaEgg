using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace TallaEgg.TelegramBot
{
    public class ProxyTest
    {
        public static async Task TestWithProxyAsync()
        {
            Console.WriteLine("🔧 Testing with proxy settings...");
            
            try
            {
                // Try to get system proxy
                var proxy = WebRequest.GetSystemWebProxy();
                var proxyUri = proxy.GetProxy(new Uri("https://api.telegram.org"));
                
                Console.WriteLine($"System Proxy: {proxyUri}");
                
                if (proxyUri != new Uri("https://api.telegram.org"))
                {
                    Console.WriteLine("⚠️ System proxy detected. Trying with proxy...");
                    
                    using var handler = new HttpClientHandler
                    {
                        Proxy = proxy,
                        UseProxy = true
                    };
                    
                    using var client = new HttpClient(handler);
                    client.Timeout = TimeSpan.FromSeconds(10);
                    
                    var response = await client.GetAsync("https://api.telegram.org");
                    Console.WriteLine($"✅ With proxy - Status: {response.StatusCode}");
                }
                else
                {
                    Console.WriteLine("ℹ️ No system proxy detected");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Proxy test failed: {ex.Message}");
            }
        }
    }
}
