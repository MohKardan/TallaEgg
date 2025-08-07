using System;
using System.Threading.Tasks;

namespace TallaEgg.TelegramBot.Tests
{
    public class SimpleTestRunner
    {
        public static async Task RunTestsAsync()
        {
            Console.WriteLine("🤖 TallaEgg Telegram Bot Automated Testing");
            Console.WriteLine("==========================================");

            var testRunner = new TestRunner();
            await testRunner.RunAllTestsAsync();
        }
    }
}
