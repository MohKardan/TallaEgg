namespace TallaEgg.Core
{
    public class CurrenciesConstant
    {
        // 🔹 ثابت‌ها برای استفاده راحت در کد
        public const string Maua = "MAUA";
        public const string Rial = "IRR";
        public const string Credit_MAUA = "CREDIT_MAUA";

        // 🔹 مجموعه‌ای از اطلاعات ارزها
        public static readonly List<CurrencyInfo> AllCurrencies = new List<CurrencyInfo>
        {
            new CurrencyInfo
            {
                Code = Maua,
                PersianName = "طلا آبشده",
                Unit = "گرم",
                DecimalPlaces = 8,
                IsTradable = true
            },
            new CurrencyInfo
            {
                Code = Rial,
                PersianName = "ریال",
                Unit = "﷼",
                DecimalPlaces = 0,
                IsTradable = false
            },
            new CurrencyInfo
            {
                Code = Credit_MAUA,
                PersianName = "اعتبار طلا",
                Unit = "گرم",
                DecimalPlaces = 0,
                IsTradable = false
            }
        };


        // 🔹 ثابت‌های جفت‌های معاملاتی
        public const string BTC_USDT = "BTC/USDT";
        public const string ETH_USDT = "ETH/USDT";
        public const string ADA_USD = "ADA/USD";
        public const string BTC_IRR = "BTC/IRR";
        public const string ETH_IRR = "ETH/IRR";
        public const string MAUA_IRR = "MAUA/IRR";

        // 🔹 مجموعه‌ای از اطلاعات جفت‌های معاملاتی
        public static readonly List<TradingPairInfo> AllTradingPairs = new List<TradingPairInfo>
        {
            new TradingPairInfo
            {
                Symbol = BTC_USDT,
                BaseAsset = "BTC",
                QuoteAsset = "USDT",
                PersianName = "بیت‌کوین/تتر",
                IsActive = true,
                MinQuantity = 0.001m,
                MaxQuantity = 1000m,
                PriceDecimalPlaces = 2,
                QuantityDecimalPlaces = 6,
                MinNotional = 10m // حداقل ارزش معامله
            },
            new TradingPairInfo
            {
                Symbol = ETH_USDT,
                BaseAsset = "ETH",
                QuoteAsset = "USDT",
                PersianName = "اتریوم/تتر",
                IsActive = true,
                MinQuantity = 0.01m,
                MaxQuantity = 10000m,
                PriceDecimalPlaces = 2,
                QuantityDecimalPlaces = 4,
                MinNotional = 10m
            },
            new TradingPairInfo
            {
                Symbol = BTC_IRR,
                BaseAsset = "BTC",
                QuoteAsset = "IRR",
                PersianName = "بیت‌کوین/ریال",
                IsActive = true,
                MinQuantity = 0.0001m,
                MaxQuantity = 100m,
                PriceDecimalPlaces = 0,
                QuantityDecimalPlaces = 8,
                MinNotional = 1000000m // 1 میلیون ریال
            },
            new TradingPairInfo
            {
                Symbol = MAUA_IRR,
                BaseAsset = "MAUA",
                QuoteAsset = "IRR",
                PersianName = "طلا آبشده/ریال",
                IsActive = true,
                MinQuantity = 0.1m,
                MaxQuantity = 1000m,
                PriceDecimalPlaces = 0,
                QuantityDecimalPlaces = 3,
                MinNotional = 100000m // 100 هزار ریال
            }
        };

        // 🔹 دیکشنری برای دسترسی سریع (case-insensitive)
        private static readonly Dictionary<string, CurrencyInfo> _map =
            AllCurrencies.ToDictionary(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);

        // 🔹 دریافت کد همه ارزها (با فرمت اصلی)
        public static List<string> GetAllCodes() =>
            AllCurrencies.Select(c => c.Code).ToList();

        // 🔹 گرفتن مشخصات ارز (case-insensitive)
        public static CurrencyInfo GetCurrencyInfo(string code) =>
            _map.TryGetValue(code, out var info) ? info : null;

        // 🔹 بررسی معتبر بودن ارز (case-insensitive)
        public static bool IsValidCurrency(string code) =>
            _map.ContainsKey(code);
    }



    public class CurrencyInfo
    {
        public string Code { get; set; }          // مثل "MAUA" یا "IRR"
        public string PersianName { get; set; }   // نام فارسی ارز
        public string Unit { get; set; }          // واحد نمایش
        public int DecimalPlaces { get; set; }    // تعداد اعشار
        public bool IsTradable { get; set; }      // قابل معامله بودن
    }

    public class TradingPairInfo
    {
        /// <summary>نماد جفت معاملاتی (مثل BTC/USDT)</summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>دارایی پایه (مثل BTC)</summary>
        public string BaseAsset { get; set; } = string.Empty;

        /// <summary>دارایی نقل‌قول (مثل USDT)</summary>
        public string QuoteAsset { get; set; } = string.Empty;

        /// <summary>نام فارسی</summary>
        public string PersianName { get; set; } = string.Empty;

        /// <summary>آیا فعال است؟</summary>
        public bool IsActive { get; set; }

        /// <summary>حداقل مقدار قابل معامله</summary>
        public decimal MinQuantity { get; set; }

        /// <summary>حداکثر مقدار قابل معامله</summary>
        public decimal MaxQuantity { get; set; }

        /// <summary>تعداد اعشار قیمت</summary>
        public int PriceDecimalPlaces { get; set; }

        /// <summary>تعداد اعشار مقدار</summary>
        public int QuantityDecimalPlaces { get; set; }

        /// <summary>حداقل ارزش معامله</summary>
        public decimal MinNotional { get; set; }
    }

}