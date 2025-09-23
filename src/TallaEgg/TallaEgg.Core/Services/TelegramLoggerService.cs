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
    public class TelegramLoggerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _botToken;

        public TelegramLoggerService(IHttpClientFactory httpClientFactory, string botToken)
        {
            _httpClientFactory = httpClientFactory;
            _botToken = botToken;
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
            // Version version = Assembly.GetExecutingAssembly().GetName().Version;
            var version = Assembly.GetEntryAssembly()?.GetName().Version;


            string text = /*$"StoreName:{_appSettings.StoreName}\n" +*/ message;
            var _options = new JsonSerializerOptions
            {
                //Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            text += $"\n V:{version.Major}.{version.Minor}.{version.Build}";

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
            //Version version = Assembly.GetExecutingAssembly().GetName().Version;
            var version = Assembly.GetEntryAssembly()?.GetName().Version;


            string text = /*$"StoreName:{_appSettings.StoreName}\n" +*/ message;

            var _options = new JsonSerializerOptions
            {
                //Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            text += JsonSerializer.Serialize(dto, _options);
            text += $"\n V:{version.Major}.{version.Minor}.{version.Build}";

            string _message = JsonSerializer.Serialize(new { Message = text, BotId = _botToken, ChatId = chatId, ParseMode = parseMode }, _options);

            var data = new StringContent(_message, Encoding.UTF8, "application/json");

            await Send(data);
        }


        public async Task LogAsync(string log, Exception ex = null)
        {
            //Version version = Assembly.GetExecutingAssembly().GetName().Version;
            var version = Assembly.GetEntryAssembly()?.GetName().Version;


            try
            {
                var _options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
                    WriteIndented = true
                };
                string text = /*$"StoreName:{_appSettings.StoreName}\n" +*/ log + "\n";
                string _ex;
                if (ex != null)
                {
                    /// Send To Exception Chanell
                    text += JsonSerializer.Serialize(ex.Message, _options);
                    text += JsonSerializer.Serialize(string.IsNullOrEmpty(ex.StackTrace) ? "no stack trace" : ex.StackTrace, _options);
                    text += $"\n V:{version.Major}.{version.Minor}.{version.Build}";
                    _ex = JsonSerializer.Serialize(new { Message = text, BotId = "1831329096:AAHA-kJzBETlafAoHdTGfFYeWdED1kwCLDk", ChatId = "-1001206333249" }, _options);
                }
                else
                {

                    _ex = JsonSerializer.Serialize(new { Message = text, BotId = "1831329096:AAHA-kJzBETlafAoHdTGfFYeWdED1kwCLDk", ChatId = "-618284393" }, _options);
                }

                var data = new StringContent(_ex, Encoding.UTF8, "application/json");
                await Send(data);
            }
            catch (Exception eex)
            {

            }

        }

        /// <summary>
        /// Send To Exception Chanell
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public async Task ErrorAsync(Exception ex, string message = "")
        {
            //  await File.AppendAllTextAsync($"SendExceptions{DateTime.Now.Ticks}.txt",message +"\n \n"+ Newtonsoft.Json.JsonConvert.SerializeObject(ex, Newtonsoft.Json.Formatting.Indented));

         //   Version version = Assembly.GetExecutingAssembly().GetName().Version;
            var version = Assembly.GetEntryAssembly()?.GetName().Version;


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

                text += $"\n V:{version.Major}.{version.Minor}.{version.Build}";


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
                await File.AppendAllTextAsync("SendExceptions.txt", Newtonsoft.Json.JsonConvert.SerializeObject(ex, Newtonsoft.Json.Formatting.Indented));
                await File.AppendAllTextAsync("SendExceptions.txt", "====================================================");
            }

        }

        //public async Task SendFile(string filePath, string fileName)
        //{
        //    try
        //    {

        //        //  var APIURL = "https://tg-notif.chbk.app//file";
        //        var APIURL = "https://tgnotif-production.up.railway.app/file";

        //        using (var httpClient = _httpClientFactory.CreateClient())
        //        using (var form = new MultipartFormDataContent())
        //        using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
        //        {
        //            form.Add(new StreamContent(fileStream), "file", fileName);
        //            form.Add(new StringContent("ChatId"), _appSettings.SuccessChannelChatId);
        //            HttpResponseMessage response = await httpClient.PostAsync(APIURL, form);
        //            response.EnsureSuccessStatusCode();
        //            httpClient.Dispose();
        //            string sd = response.Content.ReadAsStringAsync().Result;
        //        }


        //        //            using (var fileStream = File.OpenRead(filePath))
        //        //{
        //        //	// درست ش.د
        //        //	var fileName = File.ReadAllText(filePath);
        //        //	var fileContent = new StreamContent(fileStream);

        //        //	using (var formData = new MultipartFormDataContent())
        //        //	{
        //        //		formData.Add(fileContent, "file", fileName);
        //        //		var httpClient = _httpClientFactory.CreateClient();
        //        //		string APIURL = $"https://bewildered-moth-umbrella.cyclic.cloud/file";

        //        //		var response = await httpClient.PostAsync(APIURL, formData);
        //        //		var x = await response.Content.ReadAsStringAsync();
        //        //	}
        //        //}
        //    }
        //    catch (Exception ex)

        //    {
        //        await ErrorAsync(ex);
        //        // throw;
        //    }
        //}

        private async Task Send(StringContent data)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                // httpClient.Timeout = TimeSpan.FromSeconds(15);

                string APIURL = $"https://telegram-notifier.mldsalehi.workers.dev";
                //string APIURL = $"https://tgnotif-production.up.railway.app/tgdigi";
                // string APIURL = $"https://tg-notif.chbk.app/tgdigi";
                // string APIURL = $"https://telenotif.onrender.com/tgdigi";
                // string APIURL = $"https://bewildered-moth-umbrella.cyclic.cloud/tgdigi";
                //string APIURL = $"https://tgnotif-7snesbpv.b4a.run/tgdigi";

                /*var response =*/
                await httpClient.PostAsync(APIURL, data);
                //if (!response.IsSuccessStatusCode) throw new Exception();
                //	var x = await response.Content.ReadAsStringAsync();

            }
            catch (Exception ex)

            {
                await File.AppendAllTextAsync("SendExceptions.txt", Newtonsoft.Json.JsonConvert.SerializeObject(ex, Newtonsoft.Json.Formatting.Indented));
                await File.AppendAllTextAsync("SendExceptions.txt", "====================================================");

                Console.WriteLine(ex.Message);
                //try
                //{
                //                var httpClient = _httpClientFactory.CreateClient();

                //                var response = await httpClient.PostAsync(APIURL, data);
                //                var x = await response.Content.ReadAsStringAsync();
                //	Console.WriteLine(x);
                //            }
                //catch (Exception)
                //{
                //	//throw;
                //}
                //// throw;
            }
        }

    }
}
