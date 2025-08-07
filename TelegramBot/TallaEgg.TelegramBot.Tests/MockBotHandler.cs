using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Linq;

namespace TallaEgg.TelegramBot.Tests
{
    public class MockBotHandler
    {
        private readonly Dictionary<long, string> _userStates = new();
        private readonly List<string> _botResponses = new();
        private readonly List<string> _userActions = new();

        public List<string> BotResponses => _botResponses;
        public List<string> UserActions => _userActions;

        public async Task HandleUpdateAsync(Update update)
        {
            if (update.Message != null)
            {
                await HandleMessageAsync(update.Message);
            }
            else if (update.CallbackQuery != null)
            {
                await HandleCallbackQueryAsync(update.CallbackQuery);
            }
        }

        private async Task HandleMessageAsync(Message message)
        {
            var text = message.Text ?? "";
            var userId = message.From?.Id ?? 0;
            
            _userActions.Add($"User sent: {text}");

            if (text.StartsWith("/start"))
            {
                await HandleStartCommandAsync(message);
            }
            else if (text == "📝 ثبت سفارش")
            {
                await HandleOrderPlacementAsync(message);
            }
            else if (decimal.TryParse(text, out var amount))
            {
                await HandleAmountInputAsync(message, amount);
            }
            else if (message.Contact != null)
            {
                await HandlePhoneNumberAsync(message);
            }
            else
            {
                await HandleInvitationCodeAsync(message);
            }
        }

        private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            var data = callbackQuery.Data ?? "";
            var userId = callbackQuery.From?.Id ?? 0;
            
            _userActions.Add($"User clicked: {data}");

            if (data == "order_buy" || data == "order_sell")
            {
                await HandleOrderTypeSelectionAsync(callbackQuery, data);
            }
            else if (data.StartsWith("asset_"))
            {
                await HandleAssetSelectionAsync(callbackQuery, data);
            }
            else if (data == "back_to_main")
            {
                await HandleBackToMainAsync(callbackQuery);
            }
        }

        private async Task HandleStartCommandAsync(Message message)
        {
            var parts = message.Text?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts?.Length > 1)
            {
                var invitationCode = parts[1];
                _botResponses.Add($"Processing invitation code: {invitationCode}");
                
                // Simulate successful invitation validation
                _botResponses.Add("✅ کد دعوت معتبر است");
                _botResponses.Add("📱 لطفاً شماره تلفن خود را به اشتراک بگذارید");
            }
            else
            {
                _botResponses.Add("❌ لطفاً کد دعوت را وارد کنید");
            }
        }

        private async Task HandleInvitationCodeAsync(Message message)
        {
            var code = message.Text ?? "";
            _botResponses.Add($"Processing invitation code: {code}");
            _botResponses.Add("✅ کد دعوت معتبر است");
            _botResponses.Add("📱 لطفاً شماره تلفن خود را به اشتراک بگذارید");
        }

        private async Task HandlePhoneNumberAsync(Message message)
        {
            var phone = message.Contact?.PhoneNumber ?? "";
            _botResponses.Add($"Processing phone number: {phone}");
            _botResponses.Add("✅ شماره تلفن شما با موفقیت ثبت شد!");
            _botResponses.Add("🎯 منوی اصلی");
        }

        private async Task HandleOrderPlacementAsync(Message message)
        {
            _botResponses.Add("نوع سفارش خود را انتخاب کنید:");
            _botResponses.Add("🛒 خرید | 🛍️ فروش");
        }

        private async Task HandleOrderTypeSelectionAsync(CallbackQuery callbackQuery, string orderType)
        {
            var userId = callbackQuery.From?.Id ?? 0;
            _userStates[userId] = orderType;
            
            _botResponses.Add($"Order type selected: {orderType}");
            _botResponses.Add("لطفاً نماد مورد نظر را انتخاب کنید:");
            _botResponses.Add("🪙 BTC | 🪙 ETH | 🪙 USDT");
        }

        private async Task HandleAssetSelectionAsync(CallbackQuery callbackQuery, string assetData)
        {
            var userId = callbackQuery.From?.Id ?? 0;
            var asset = assetData.Replace("asset_", "");
            
            _botResponses.Add($"Asset selected: {asset}");
            _botResponses.Add("لطفاً مقدار واحد را وارد کنید:");
            _botResponses.Add($"قیمت: 50,000,000 تومان");
        }

        private async Task HandleAmountInputAsync(Message message, decimal amount)
        {
            var userId = message.From?.Id ?? 0;
            
            _botResponses.Add($"Amount entered: {amount}");
            
            if (_userStates.TryGetValue(userId, out var orderType) && orderType == "order_sell")
            {
                // Simulate balance check for sell orders
                var balance = 0.5m;
                if (amount > balance)
                {
                    _botResponses.Add($"❌ موجودی کافی نیست. موجودی شما: {balance} واحد");
                }
                else
                {
                    _botResponses.Add("✅ سفارش شما با موفقیت ثبت شد!");
                }
            }
            else
            {
                _botResponses.Add("✅ سفارش شما با موفقیت ثبت شد!");
            }
        }

        private async Task HandleBackToMainAsync(CallbackQuery callbackQuery)
        {
            _botResponses.Add("🎯 منوی اصلی");
        }

        public void ClearResponses()
        {
            _botResponses.Clear();
            _userActions.Clear();
            _userStates.Clear();
        }

        public bool ContainsResponse(string expectedResponse)
        {
            return _botResponses.Any(r => r.Contains(expectedResponse));
        }

        public bool ContainsAction(string expectedAction)
        {
            return _userActions.Any(a => a.Contains(expectedAction));
        }
    }
}
