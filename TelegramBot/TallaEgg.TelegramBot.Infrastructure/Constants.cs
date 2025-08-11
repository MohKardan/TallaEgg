using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.TelegramBot.Infrastructure
{
    public class Constants
    {
        public const string DeveloperChatId = "-4777000333"; 
        public const string SupportErrorMessage = "مشکلی پیش آمده لطفا با پشتیبانی تماس بگیرید."; 
    }

    public static class ButtonTextsConstants
    {
        public const string MainMenu = "💰 منوی اصلی";
        public const string Spot = "💰 نقدی";
        public const string Future = "📈 آتی";
        public const string Accounting = "📊 حسابداری";
        public const string Help = "❓ راهنما";
        public const string Wallet = "💳 کیف پول";
        public const string History = "📋 تاریخچه";
        public const string MakeOrder = "📋 ثبت سفارش";
        public const string TakeOrder = "📋 بازار";
        
    }
    public static class BotTexts
    {
        public const string MainMenu = "💰 منوی اصلی";
        public const string BtnSpot = "💰 نقدی";
        public const string BtnFutures = "📈 آتی";
        public const string BtnAccounting = "📊 حسابداری";
        public const string BtnHelp = "❓ راهنما";
        public const string BtnBack = "🔙 بازگشت";
        public const string BtnSharePhone = "📱 اشتراک‌گذاری شماره تلفن";
        public const string BtnPlaceOrder = "📝 ثبت سفارش";
        public const string BtnBuy = "🛒 خرید";
        public const string BtnSell = "🛍️ فروش";
        public const string BtnConfirm = "✅ تایید";
        public const string BtnCancel = "❌ لغو";
        public const string MsgEnterInvite = "برای شروع، لطفاً کد دعوت خود را وارد کنید:\n/start [کد_دعوت]";
        public const string MsgPhoneRequest = "لطفاً شماره تلفن خود را به اشتراک بگذارید تا بتوانید از خدمات ربات استفاده کنید.";
        public const string MsgWelcome = "🎉 خوش آمدید!\nثبت‌نام شما با موفقیت انجام شد.\n\nلطفاً شماره تلفن خود را به اشتراک بگذارید تا بتوانید از خدمات ربات استفاده کنید.";
        public const string MsgPhoneSuccess = "✅ شماره تلفن شما با موفقیت ثبت شد!\n\nحالا می‌توانید از خدمات ربات استفاده کنید.";
        public const string MsgMainMenu = "🎯 منوی اصلی\n\nلطفاً یکی از گزینه‌های زیر را انتخاب کنید:";
        public const string MsgSelectTradingType = "نوع معامله خود را انتخاب کنید:";
        public const string MsgSelectOrderType = "نوع سفارش خود را انتخاب کنید:";
        public const string MsgSelectAsset = "نماد معاملاتی مورد نظر را انتخاب کنید:";
        public const string MsgEnterAmount = "لطفاً مقدار واحد را وارد کنید:";
        public const string MsgOrderConfirmation = "📋 تایید سفارش\n\nنماد: {0}\nنوع: {1}\nمقدار: {2} واحد\nقیمت: {3:N0} تومان\nمبلغ کل: {4:N0} تومان\n\nآیا سفارش را تایید می‌کنید؟";
        public const string MsgInsufficientBalance = "موجودی کافی نیست. موجودی شما: {0} واحد";
        public const string MsgOrderSuccess = "✅ سفارش شما با موفقیت ثبت شد!";
        public const string MsgOrderFailed = "❌ خطا در ثبت سفارش: {0}";
        public const string MakeOrderSpot = "📋 ثبت سفارش";
        public const string TakeOrder = "📋 بازار";
    }
    public static class inlineCallBackData
    {
        public const string buy_futures = "buy_futures";
        public const string sell_futures = "sell_futures";
        public const string trading_spot = "trading_spot";
        public const string trading_futures = "trading_futures";
        public const string order_buy = "order_buy";
        public const string order_sell = "order_sell";
        public const string confirm_order = "confirm_order";
        public const string cancel_order = "cancel_order";
        public const string charge_card = "charge_card";
        public const string charge_bank = "charge_bank";
        public const string back_to_main = "back_to_main";
    }

}
