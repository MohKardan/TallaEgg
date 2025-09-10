using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.TelegramBot.Infrastructure
{
    public class Constants
    {
        public const long GroupId = -1002988196234;
        public const string DeveloperChatId = "-4777000333";
        public const string SupportErrorMessage = "مشکلی پیش آمده لطفا با پشتیبانی تماس بگیرید.";
    }
    public static class BotBtns
    {
        public const string BtnMainMenu = "💰 منوی اصلی";
        public const string BtnSpot = "💰 نقدی";
        public const string BtnFutures = "📈 آتی";
        public const string BtnAccounting = "📊 حسابداری";
        public const string BtnHelp = "❓ راهنما";
        public const string BtnBack = "🔙 بازگشت";
        public const string BtnHistory = "📋 تاریخچه";
        public const string BtnOrderHistory = "📋 تاریخچه سفارشات";
        public const string BtnTradeHistory = "📊 تاریخچه معاملات";
        public const string BtnWalletsBalance = "💵 موجودی";
        public const string BtnWallet = "💳 کیف پول";
        public const string BtnSharePhone = "📱 اشتراک‌گذاری شماره تلفن";
        public const string BtnConfirm = "✅ تایید";
        public const string BtnCancel = "❌ لغو";
        /// <summary>
        /// Place Order همان مفهوم Make Order را دارد و به معنای ثبت سفارش است
        /// </summary>
        public const string BtnSpotCreateOrder = "📝 ثبت سفارش نقدی";
        /// <summary>
        /// ثبت قیمت نقدی با ثبت سفارش نقدی هیچ فرقی نمیکند
        /// بخاطر اینکه برای مصطفی قابل درکتر باشه این اسمو روی دکمه ادمین نمایش میدم
        /// </summary>
        public const string BtnSpotSubmitPrice = "📝 ثبت قیمت نقدی";
        public const string BtnSpotMarket = "📈 بازار نقدی";
        public const string BtnSpotMarketBuy = "🛒 خرید نقدی";
        public const string BtnSpotMarketSell = "🛍️ فروش نقدی";
    }
    public static class BotMsgs
    {
        public const string MsgEnterInvite = "برای شروع، لطفاً کد دعوت خود را وارد کنید:\n/start [کد_دعوت]";
        public const string MsgPhoneRequest = "لطفاً شماره تلفن خود را به اشتراک بگذارید تا بتوانید از خدمات ربات استفاده کنید.";
        public const string MsgWelcome = "🎉 خوش آمدید!\nثبت‌نام شما با موفقیت انجام شد.\n\nلطفاً شماره تلفن خود را به اشتراک بگذارید تا بتوانید از خدمات ربات استفاده کنید.";
        public const string MsgPhoneSuccess = "✅ شماره تلفن شما با موفقیت ثبت شد!\n\nلطفا منتظر تایید مدیر بمانید.";
        public const string MsgMainMenu = "🎯 منوی اصلی\n\nلطفاً یکی از گزینه‌های زیر را انتخاب کنید:";
        public const string MsgSelectTradingType = "نوع معامله خود را انتخاب کنید:";
        
        //public const string MsgSelectOrderSide = "سمت و جهت سفارش خود را انتخاب کنید:";
        
        public const string MsgSelectAsset = "نماد معاملاتی مورد نظر را انتخاب کنید:";
        //public const string MsgSelectSymbol = "نماد معاملاتی مورد نظر را انتخاب کنید:";
        
        public const string MsgEnterAmount = "لطفاً مقدار واحد را وارد کنید:";
        public const string MsgOrderConfirmation = "📋 تایید سفارش\n\nنماد: {0}\nنوع: {1}\nمقدار: {2} واحد\nقیمت: {3:N0} تومان\nمبلغ کل: {4:N0} تومان\n\nآیا سفارش را تایید می‌کنید؟";
        public const string MsgOrderConfirmation_MAUA_IRR = "📋 تایید سفارش\n\nنماد: {0}\nنوع: {1}\nمقدار: {2} واحد\nقیمت هر گرم: {3:N0} تومان\nمبلغ کل: {4:N0} تومان\n\nآیا سفارش را تایید می‌کنید؟";
        public const string MsgInsufficientBalance = "موجودی کافی نیست. موجودی شما: {0} واحد";
        public const string MsgOrderSuccess = "✅ سفارش شما با موفقیت ثبت شد!";
        public const string MsgOrderFailed = "❌ خطا در ثبت سفارش: {0}";
        
        public const string MsgMarketPrices = "📊 قیمت‌های بازار\n\nنماد: {0}\nبهترین خرید: {1:N0} تومان\nبهترین فروش: {2:N0} تومان\nاسپرد: {3:N0} تومان\n\nعملیات مورد نظر را انتخاب کنید:";
        public const string MsgEnterQuantity = "لطفاً مقدار {0} را وارد کنید:";
        public const string MsgMarketOrderConfirmation = "📋 تایید سفارش بازار\n\nنماد: {0}\nنوع: {1}\nمقدار: {2} واحد\nقیمت: {3:N0} تومان\nمبلغ کل: {4:N0} تومان\n\nآیا سفارش را تایید می‌کنید؟";
        
        public const string MsgAdminHelp = "🔧 دستورات مدیریتی:\n\n" +
                                          "ش شماره مبلغ نوع - شارژ کیف پول\n" +
                                          "د شماره مبلغ نوع - کسر از موجودی\n" +
                                          "ک جستجو - لیست کاربران\n" +
                                          "م شماره - موجودی کاربر\n" +
                                          "س شماره - تاریخچه معاملات\n" +
                                          "قیمت_خرید-قیمت_فروش - ثبت دو سفارش (مثال: 8523690-8529630)";
    }
    public static class InlineCallBackData
    {
        /// <summary>
        /// قبل این روی دکمه نقدی در منوی اصلی کلیک شده است
        /// BotTexts.BtnSpot
        /// دکمه شیشه ای خرید
        /// </summary>
        public const string buy_spot = "buy_spot";
        public const string sell_spot = "sell_spot";
        public const string trading_spot = "trading_spot";
        public const string trading_futures = "trading_futures";
        public const string order_buy = "order_buy";
        public const string order_sell = "order_sell";
        public const string confirm_order = "confirm_order";
        public const string cancel_order = "cancel_order";
        public const string charge_card = "charge_card";
        public const string charge_bank = "charge_bank";
        public const string back_to_main = "back_to_main";
        
        public const string confirm_market_order = "confirm_market_order";
    }

}
