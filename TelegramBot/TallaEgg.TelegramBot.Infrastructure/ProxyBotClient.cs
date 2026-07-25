using System;
using System.Net;
using Telegram.Bot;

namespace TallaEgg.TelegramBot
{
    public class ProxyBotClient
    {
        public static ITelegramBotClient CreateWithProxy(string token)
        {
            // Local override: when the machine can reach Telegram directly (e.g. a TUN-mode VPN),
            // the system HTTP proxy can be unstable on long-poll (getUpdates) and drops the
            // connection ("response ended prematurely"). Set BOT_DIRECT_CONNECTION=1 to bypass the
            // proxy and connect directly. Unset (the default) keeps the original proxy behaviour,
            // which the server deployment relies on.
            if (Environment.GetEnvironmentVariable("BOT_DIRECT_CONNECTION") == "1")
            {
                Console.WriteLine("🔗 Direct connection (proxy bypassed via BOT_DIRECT_CONNECTION=1)");
                var directClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
                return new TelegramBotClient(token, directClient);
            }

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
                    httpClient.Timeout = TimeSpan.FromSeconds(120); // افزایش timeout به 2 دقیقه
                    
                    return new TelegramBotClient(token, httpClient);
                }
                else
                {
                    // No proxy needed - create with extended timeout
                    var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(120); // افزایش timeout به 2 دقیقه
                    return new TelegramBotClient(token, httpClient);
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
