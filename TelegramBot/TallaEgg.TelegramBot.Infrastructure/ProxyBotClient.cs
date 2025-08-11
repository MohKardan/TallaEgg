using System;
using System.Net;
using Telegram.Bot;

namespace TallaEgg.TelegramBot
{
    public class ProxyBotClient
    {
        public static ITelegramBotClient CreateWithProxy(string token)
        {
            try
            {
                // Get system proxy
                var proxy = WebRequest.GetSystemWebProxy();
                var proxyUri = proxy.GetProxy(new Uri("https://api.telegram.org"));
                
                Console.WriteLine($"🔧 Using proxy: {proxyUri}");
                
                if (proxyUri != new Uri("https://api.telegram.org"))
                {
                    // Create bot client with proxy
                    var handler = new HttpClientHandler
                    {
                        Proxy = proxy,
                        UseProxy = true
                    };
                    
                    var httpClient = new HttpClient(handler);
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    
                    return new TelegramBotClient(token, httpClient);
                }
                else
                {
                    // No proxy needed
                    return new TelegramBotClient(token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error configuring proxy: {ex.Message}");
                Console.WriteLine("Falling back to direct connection...");
                return new TelegramBotClient(token);
            }
        }
    }
}
