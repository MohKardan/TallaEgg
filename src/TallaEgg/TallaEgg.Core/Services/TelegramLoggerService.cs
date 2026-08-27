using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace TallaEgg.Core.Services
{
    public class TelegramLoggerService : ITelegramLogger
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _botToken;

        public TelegramLoggerService(IHttpClientFactory httpClientFactory, string botToken)
        {
            _httpClientFactory = httpClientFactory;
            _botToken = botToken;
        }

        /// <summary>
        /// The version line appended to every message, or nothing when the entry assembly has no
        /// version — which is the case under a test host, where GetEntryAssembly is the runner.
        /// Computed once: it cannot change while the process lives.
        /// </summary>
        private static readonly string VersionSuffix = BuildVersionSuffix();

        private static string BuildVersionSuffix()
        {
            var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            return assemblyVersion is null
                ? string.Empty
                : $"\n V:{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
        }

        /// <summary>
        /// Send To Main Chanell
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        /// 
        public async Task Notif(string message, string chatId= "-1002988196234", string parseMode = "")
        {


            string text = /*$"StoreName:{_appSettings.StoreName}\n" +*/ message;
            var _options = new JsonSerializerOptions
            {
                //Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            text += VersionSuffix;

            string _message = JsonSerializer.Serialize(new { Message = text, BotId = _botToken, ChatId = chatId, ParseMode = parseMode }, _options);

            var data = new StringContent(_message, Encoding.UTF8, "application/json");

            await Send(data);
        }

        /// <summary>
        /// Send To Main Chanell
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message"></param>
        /// <param name="dto"></param>
        /// <returns></returns>
        /// 
        public async Task Notif<T>(string message, T dto, string chatId = "-1002988196234", string parseMode = "")
        {


            string text = /*$"StoreName:{_appSettings.StoreName}\n" +*/ message;

            var _options = new JsonSerializerOptions
            {
                //Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            text += JsonSerializer.Serialize(dto, _options);
            text += VersionSuffix;

            string _message = JsonSerializer.Serialize(new { Message = text, BotId = _botToken, ChatId = chatId, ParseMode = parseMode }, _options);

            var data = new StringContent(_message, Encoding.UTF8, "application/json");

            await Send(data);
        }


        public async Task LogAsync<T>(string message, T dto, string chatId = "-822161060", string parseMode = "")
        {


            string text = /*$"StoreName:{_appSettings.StoreName}\n" +*/ message;

            var _options = new JsonSerializerOptions
            {
                //Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            text += JsonSerializer.Serialize(dto, _options);
            text += VersionSuffix;

            string _message = JsonSerializer.Serialize(new { Message = text, BotId = _botToken, ChatId = chatId, ParseMode = parseMode }, _options);

            var data = new StringContent(_message, Encoding.UTF8, "application/json");

            await Send(data);
        }


        public async Task LogAsync(string log, string chatId = "-822161060")
        {


            try
            {
                var _options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                    WriteIndented = true
                };
                string text = /*$"StoreName:{_appSettings.StoreName}\n" +*/ log + "\n";
             
                
                    text += VersionSuffix;
                    var _text = JsonSerializer.Serialize(new { Message = text, BotId = _botToken, ChatId = chatId }, _options);
                

                var data = new StringContent(_text, Encoding.UTF8, "application/json");
                await Send(data);
            }
            catch (Exception)
            {
                // Deliberately silent: this is the logger itself. Throwing would turn a failed
                // log into a failed operation, and logging the failure would recurse. Losing an
                // informational message is the cheaper outcome; the error path below does persist.
            }

        }

        /// <summary>
        /// Send To Exception Chanell
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public async Task ErrorAsync(Exception ex, string message = "")
        {



            try
            {
                var _options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                    WriteIndented = true
                };

                string text = /*$"StoreName:{_appSettings.StoreName} " + "\n" +*/ message + "\n" + System.Text.Json.JsonSerializer.Serialize(ex.Message, _options);
                text += "\n" + System.Text.Json.JsonSerializer.Serialize(string.IsNullOrEmpty(ex.StackTrace) ? "no stack trace" : ex.StackTrace, _options);
                text += "\n" + JsonSerializer.Serialize(string.IsNullOrEmpty(ex.Source) ? "no source" : ex.Source, _options);
                //text += "\n" + ex.InnerException != null ? "Inner:" + JsonSerializer.Serialize(ex.InnerException?.Message) : "No Inner";

                text += VersionSuffix;


                string _ex = JsonSerializer.Serialize(
                  new
                  {

                      Message = text,
                      BotId = _botToken,
                      ChatId = "-890016025",
                      Type = "Error"
                  }, _options);

                var data = new StringContent(_ex, Encoding.UTF8, "application/json");
                await Send(data);
            }
            catch (Exception eex)
            {
                // Last resort: Telegram is unreachable, so persist both the exception being
                // reported and the one that stopped it from being reported. Writing only the
                // former, as this used to, leaves no way to tell why the send failed.
                await File.AppendAllTextAsync("SendExceptions.txt", Newtonsoft.Json.JsonConvert.SerializeObject(
                    new { Reported = ex, SendFailure = eex }, Newtonsoft.Json.Formatting.Indented));
                await File.AppendAllTextAsync("SendExceptions.txt", "====================================================");
            }

        }

        private async Task Send(StringContent data)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();

                string APIURL = $"https://telegram-notifier.mldsalehi.workers.dev";

                await httpClient.PostAsync(APIURL, data);
            }
            catch (Exception ex)

            {
                await File.AppendAllTextAsync("SendExceptions.txt", Newtonsoft.Json.JsonConvert.SerializeObject(ex, Newtonsoft.Json.Formatting.Indented));
                await File.AppendAllTextAsync("SendExceptions.txt", "====================================================");

                Console.WriteLine(ex.Message);
            }
        }

    }
}
