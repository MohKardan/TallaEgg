namespace TallaEgg.Core
{
    public class CurrenciesConstant
    {
        // 🔹 ثابت‌ها برای استفاده راحت در کد
        public const string Maua = "MAUA";

        /// <summary>
        /// واحد پول سیستم: تومان (IRT).
        /// عمداً IRR (ریال) نیست — تمام مبالغ ذخیره‌شده در دیتابیس به تومان هستند
        /// (قیمت‌ها به تومان وارد می‌شوند و هیچ تبدیل ریال/تومانی در کد وجود ندارد).
        /// استفاده از IRR در گذشته باعث ابهام شده بود که عدد ذخیره‌شده ریال است یا تومان.
        /// </summary>
        public const string Toman = "IRT";

        public const string Credit_MAUA = "CREDIT_MAUA";

        /// <summary>
        /// وزن یک مثقال به گرم. قیمت‌ها توسط کاربر و مدیر بر حسب «هر مثقال» وارد
        /// می‌شوند و برای ذخیره‌سازی به «هر گرم» تبدیل می‌شوند.
        /// </summary>
        public const decimal GramsPerMesghal = 4.3318m;

        // 🔹 مجموعه‌ای از اطلاعات ارزها
        public static readonly List<CurrencyInfo> AllCurrencies = new List<CurrencyInfo>
        {
            new CurrencyInfo
            {
                Code = Maua,
                PersianName = "آبشده",
                Unit = "گرم",
                DecimalPlaces = 2,
                IsTradable = true
            },
            new CurrencyInfo
            {
                Code = Toman,
                PersianName = "تومان",
                Unit = "تومان",
                DecimalPlaces = 0,
                IsTradable = false
            },
            new CurrencyInfo
            {
                Code = Credit_MAUA,
                PersianName = "اعتبار آبشده",
                Unit = "گرم",
                DecimalPlaces = 2,
                IsTradable = false
            }
        };


        // 🔹 ثابت‌های جفت‌های معاملاتی
        public const string BTC_USDT = "BTC/USDT";
        public const string ETH_USDT = "ETH/USDT";
        public const string ADA_USD = "ADA/USD";
        public const string BTC_IRT = "BTC/IRT";
        public const string ETH_IRT = "ETH/IRT";
        public const string MAUA_IRT = "MAUA/IRT";

        // 🔹 مجموعه‌ای از اطلاعات جفت‌های معاملاتی
        public static readonly List<TradingPairInfo> AllTradingPairs = new List<TradingPairInfo>
        {
            new TradingPairInfo
            {
                Symbol = BTC_USDT,
                BaseAsset = "BTC",
                QuoteAsset = "USDT",
                PersianName = "بیت‌کوین/تتر",
                IsActive = false,
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
                IsActive = false,
                MinQuantity = 0.01m,
                MaxQuantity = 10000m,
                PriceDecimalPlaces = 2,
                QuantityDecimalPlaces = 4,
                MinNotional = 10m
            },
            new TradingPairInfo
            {
                Symbol = BTC_IRT,
                BaseAsset = "BTC",
                QuoteAsset = Toman,
                PersianName = "بیت‌کوین/تومان",
                IsActive = false,
                MinQuantity = 0.0001m,
                MaxQuantity = 100m,
                PriceDecimalPlaces = 0,
                QuantityDecimalPlaces = 8,
                MinNotional = 1000000m // 1 میلیون تومان
            },
            new TradingPairInfo
            {
                Symbol = MAUA_IRT,
                BaseAsset = Maua,
                QuoteAsset = Toman,
                PersianName = "آبشده/تومان",
                IsActive = true,
                MinQuantity = 0.1m,
                MaxQuantity = 1000m,
                PriceDecimalPlaces = 0,
                QuantityDecimalPlaces = 3,
                MinNotional = 100000m // 100 هزار تومان
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

        // 🔹 دیکشنری جفت‌های معاملاتی برای دسترسی سریع (case-insensitive)
        private static readonly Dictionary<string, TradingPairInfo> _pairMap =
            AllTradingPairs.ToDictionary(p => p.Symbol, p => p, StringComparer.OrdinalIgnoreCase);

        /// <summary>گرفتن مشخصات جفت معاملاتی (case-insensitive). اگر پیدا نشود null.</summary>
        public static TradingPairInfo? GetTradingPairInfo(string symbol) =>
            symbol is not null && _pairMap.TryGetValue(symbol, out var info) ? info : null;

        /// <summary>
        /// نام فارسی ارز برای نمایش به کاربر. اگر ارز ناشناس باشد خود کد برگردانده می‌شود.
        /// </summary>
        public static string GetPersianName(string code) =>
            GetCurrencyInfo(code)?.PersianName ?? code;

        /// <summary>
        /// ورودی کاربر را به کد ارز تبدیل می‌کند. هم کد لاتین («IRT») و هم نام فارسی
        /// («تومان») پذیرفته می‌شود، تا مدیر مجبور نباشد کد انگلیسی به خاطر بسپارد.
        /// اگر ورودی به هیچ ارزی نخورد، null برمی‌گردد.
        /// </summary>
        public static string? ResolveCurrencyCode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var trimmed = input.Trim();

            // ابتدا تطبیق با کد ارز
            if (_map.TryGetValue(trimmed, out var byCode))
                return byCode.Code;

            // سپس تطبیق با نام فارسی
            var byName = AllCurrencies.FirstOrDefault(c =>
                string.Equals(c.PersianName, trimmed, StringComparison.OrdinalIgnoreCase));

            return byName?.Code;
        }

        /// <summary>
        /// فهرست نام‌های فارسی ارزها برای نمایش در پیام خطا (به‌جای کدهای لاتین).
        /// </summary>
        public static string GetPersianNamesList() =>
            string.Join("، ", AllCurrencies.Select(c => c.PersianName));

        /// <summary>
        /// نام فارسی جفت معاملاتی برای نمایش به کاربر (مثل «آبشده/تومان»).
        /// اگر جفت ناشناس باشد، از نام فارسی دو طرف ساخته می‌شود تا هیچ‌وقت
        /// نماد لاتین در متن فارسی نمایش داده نشود.
        /// </summary>
        public static string GetPersianSymbolName(string symbol)
        {
            var pair = GetTradingPairInfo(symbol);
            if (pair is not null && !string.IsNullOrWhiteSpace(pair.PersianName))
                return pair.PersianName;

            var parts = symbol?.Split('/');
            if (parts is { Length: 2 })
                return $"{GetPersianName(parts[0])}/{GetPersianName(parts[1])}";

            return symbol ?? string.Empty;
        }
    }



    public class CurrencyInfo
    {
        public string Code { get; set; }          // مثل "MAUA" یا "IRT"
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