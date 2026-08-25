using Microsoft.Extensions.Configuration;

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

        public const string SekeBahar = "SEKE_BAHAR";

        public const string Btc = "BTC";

        /// <summary>
        /// وزن یک مثقال به گرم. قیمت‌ها توسط کاربر و مدیر بر حسب «هر مثقال» وارد
        /// می‌شوند و برای ذخیره‌سازی به «هر گرم» تبدیل می‌شوند.
        /// </summary>
        public const decimal GramsPerMesghal = 4.3318m;

        // 🔹 ثابت‌های جفت‌های معاملاتی
        public const string BTC_USDT = "BTC/USDT";
        public const string ETH_USDT = "ETH/USDT";
        public const string ADA_USD = "ADA/USD";
        public const string BTC_IRT = "BTC/IRT";
        public const string ETH_IRT = "ETH/IRT";
        public const string MAUA_IRT = "MAUA/IRT";
        public const string SEKE_BAHAR_IRT = "SEKE_BAHAR/IRT";

        /// <summary>
        /// Compiled defaults for every symbol this platform trades today. A process that never
        /// calls <see cref="Configure"/> sees exactly these — nothing about existing behaviour
        /// depends on a config file being present (the test suite's CI runner has none, by
        /// design — see build-and-test.yml).
        ///
        /// <para>
        /// <b>Adding a new symbol does not require editing this dictionary.</b> A block under
        /// <c>Symbols:{Base}/{Quote}</c> in <c>config/appsettings.global.json</c> — decimal
        /// precision, min/max quantity, Persian display name, and which external price provider
        /// instrument feeds it — is enough; see README's "Adding a trading symbol" section. This
        /// dictionary only needs a new entry if the symbol should also work with zero config
        /// present (i.e. it becomes one of the platform's built-in defaults).
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, TradingPairInfo> DefaultTradingPairs =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [BTC_USDT] = new TradingPairInfo
                {
                    Symbol = BTC_USDT, BaseAsset = "BTC", QuoteAsset = "USDT", PersianName = "بیت‌کوین/تتر",
                    MinQuantity = 0.001m, MaxQuantity = 1000m, PriceDecimalPlaces = 2, QuantityDecimalPlaces = 6,
                    MinNotional = 10m, BaseAssetPersianName = "بیت‌کوین", BaseUnit = "بیت‌کوین", BaseDecimalPlaces = 8
                },
                [ETH_USDT] = new TradingPairInfo
                {
                    Symbol = ETH_USDT, BaseAsset = "ETH", QuoteAsset = "USDT", PersianName = "اتریوم/تتر",
                    MinQuantity = 0.01m, MaxQuantity = 10000m, PriceDecimalPlaces = 2, QuantityDecimalPlaces = 4,
                    MinNotional = 10m, BaseAssetPersianName = "اتریوم", BaseUnit = "اتریوم", BaseDecimalPlaces = 8
                },
                [BTC_IRT] = new TradingPairInfo
                {
                    Symbol = BTC_IRT, BaseAsset = Btc, QuoteAsset = Toman, PersianName = "بیت‌کوین/تومان",
                    MinQuantity = 0.0001m, MaxQuantity = 100m, PriceDecimalPlaces = 0, QuantityDecimalPlaces = 8,
                    MinNotional = 1000000m, BaseAssetPersianName = "بیت‌کوین", BaseUnit = "بیت‌کوین",
                    BaseDecimalPlaces = 8, Aliases = new List<string> { "بیت", "بیتکوین", "بیت‌کوین" }
                },
                [MAUA_IRT] = new TradingPairInfo
                {
                    Symbol = MAUA_IRT, BaseAsset = Maua, QuoteAsset = Toman, PersianName = "آبشده/تومان",
                    MinQuantity = 0.1m, MaxQuantity = 1000m, PriceDecimalPlaces = 0, QuantityDecimalPlaces = 3,
                    MinNotional = 100000m, BaseAssetPersianName = "آبشده", BaseUnit = "گرم", BaseDecimalPlaces = 2
                },
                [SEKE_BAHAR_IRT] = new TradingPairInfo
                {
                    Symbol = SEKE_BAHAR_IRT, BaseAsset = SekeBahar, QuoteAsset = Toman,
                    PersianName = "سکه تمام بهار آزادی/تومان", MinQuantity = 0.01m, MaxQuantity = 50m,
                    PriceDecimalPlaces = 0, QuantityDecimalPlaces = 2, MinNotional = 1000000m,
                    BaseAssetPersianName = "سکه تمام بهار آزادی", BaseUnit = "سکه", BaseDecimalPlaces = 2,
                    Aliases = new List<string> { "سکه" }
                }
            };

        private static Dictionary<string, TradingPairInfo> _pairs =
            new(DefaultTradingPairs, StringComparer.OrdinalIgnoreCase);

        // Toman is structural to the whole system — the one unit of account — rather than "a
        // symbol", so it is a fixed entry instead of being derived from a trading pair like
        // every base asset (and its CREDIT_ ledger) below is.
        private static readonly CurrencyInfo TomanInfo = new()
        { Code = Toman, PersianName = "تومان", Unit = "تومان", DecimalPlaces = 0, IsTradable = false };

        private static Dictionary<string, CurrencyInfo> _currencies = BuildCurrencies(_pairs);

        /// <summary>
        /// The credit-ledger asset code for a tradable base asset, e.g. <c>MAUA</c> →
        /// <c>CREDIT_MAUA</c>. Every tradable asset gets one (see <see cref="BuildCurrencies"/>) —
        /// credit was gold-only back when gold was the only tradable asset; it is a per-asset
        /// ceiling now that there is more than one.
        /// </summary>
        public static string CreditAssetFor(string baseAsset) => "CREDIT_" + baseAsset;

        /// <summary>
        /// Merges the "Symbols" section of the shared config file on top of the compiled
        /// defaults: an unrecognised key becomes a brand-new trading pair, a known one has only
        /// the fields the config block actually sets overridden. Call once at each service's
        /// startup (see each Program.cs). Safe to skip — every symbol traded today has a
        /// compiled default, so a process that never calls this behaves exactly as if it had; the
        /// call only matters for a symbol nobody has written a <see cref="TradingPairInfo"/> for.
        /// </summary>
        public static void Configure(IConfiguration configuration)
        {
            _pairs = MergeWithConfiguration(_pairs, configuration);
            _currencies = BuildCurrencies(_pairs);
        }

        /// <summary>
        /// The pure merge <see cref="Configure"/> applies — split out so it can be unit-tested
        /// against an arbitrary starting dictionary and configuration, without mutating this
        /// class's shared static state (which every test in the process reads).
        /// </summary>
        public static Dictionary<string, TradingPairInfo> MergeWithConfiguration(
            IReadOnlyDictionary<string, TradingPairInfo> current, IConfiguration configuration)
        {
            var merged = new Dictionary<string, TradingPairInfo>(current, StringComparer.OrdinalIgnoreCase);

            foreach (var child in configuration.GetSection("Symbols").GetChildren())
            {
                var info = merged.TryGetValue(child.Key, out var existing) ? Clone(existing) : new TradingPairInfo();
                child.Bind(info);
                info.Symbol = child.Key;

                if (string.IsNullOrWhiteSpace(info.BaseAsset) || string.IsNullOrWhiteSpace(info.QuoteAsset))
                {
                    var parts = child.Key.Split('/');
                    if (parts.Length == 2)
                    {
                        if (string.IsNullOrWhiteSpace(info.BaseAsset)) info.BaseAsset = parts[0];
                        if (string.IsNullOrWhiteSpace(info.QuoteAsset)) info.QuoteAsset = parts[1];
                    }
                }

                merged[child.Key] = info;
            }

            return merged;
        }

        private static TradingPairInfo Clone(TradingPairInfo source) => new()
        {
            Symbol = source.Symbol,
            BaseAsset = source.BaseAsset,
            QuoteAsset = source.QuoteAsset,
            PersianName = source.PersianName,
            MinQuantity = source.MinQuantity,
            MaxQuantity = source.MaxQuantity,
            PriceDecimalPlaces = source.PriceDecimalPlaces,
            QuantityDecimalPlaces = source.QuantityDecimalPlaces,
            MinNotional = source.MinNotional,
            BaseAssetPersianName = source.BaseAssetPersianName,
            BaseUnit = source.BaseUnit,
            BaseDecimalPlaces = source.BaseDecimalPlaces,
            Aliases = new List<string>(source.Aliases)
        };

        private static Dictionary<string, CurrencyInfo> BuildCurrencies(Dictionary<string, TradingPairInfo> pairs)
        {
            var map = new Dictionary<string, CurrencyInfo>(StringComparer.OrdinalIgnoreCase)
            {
                [Toman] = TomanInfo
            };

            foreach (var pair in pairs.Values)
            {
                if (string.IsNullOrWhiteSpace(pair.BaseAsset)) continue;

                var baseName = string.IsNullOrWhiteSpace(pair.BaseAssetPersianName) ? pair.BaseAsset : pair.BaseAssetPersianName;

                map[pair.BaseAsset] = new CurrencyInfo
                {
                    Code = pair.BaseAsset,
                    PersianName = baseName,
                    Unit = pair.BaseUnit,
                    DecimalPlaces = pair.BaseDecimalPlaces,
                    IsTradable = true
                };

                // One credit ledger per tradable asset (issue: multi-symbol شارژ, see the
                // conversation that added this) — same ceiling concept CREDIT_MAUA already was,
                // generalized instead of staying a MAUA-only special case.
                var creditCode = CreditAssetFor(pair.BaseAsset);
                map[creditCode] = new CurrencyInfo
                {
                    Code = creditCode,
                    PersianName = "اعتبار " + baseName,
                    Unit = pair.BaseUnit,
                    DecimalPlaces = pair.BaseDecimalPlaces,
                    IsTradable = false
                };
            }

            return map;
        }

        // 🔹 مجموعه‌ای از اطلاعات ارزها
        public static List<CurrencyInfo> AllCurrencies => _currencies.Values.ToList();

        // 🔹 مجموعه‌ای از اطلاعات جفت‌های معاملاتی
        public static List<TradingPairInfo> AllTradingPairs => _pairs.Values.ToList();

        // 🔹 دریافت کد همه ارزها (با فرمت اصلی)
        public static List<string> GetAllCodes() =>
            _currencies.Values.Select(c => c.Code).ToList();

        // 🔹 گرفتن مشخصات ارز (case-insensitive)
        public static CurrencyInfo GetCurrencyInfo(string code) =>
            _currencies.TryGetValue(code, out var info) ? info : null;

        // 🔹 بررسی معتبر بودن ارز (case-insensitive)
        public static bool IsValidCurrency(string code) =>
            _currencies.ContainsKey(code);

        /// <summary>
        /// گرد کردن یک مبلغ به دقت واقعی همان دارایی (مثلاً تومان: بدون اعشار، آبشده: دو رقم).
        ///
        /// این متد باید در هر جایی که مبلغی محاسبه می‌شود صدا زده شود — به‌ویژه
        /// «مقدار × قیمت» — چون نتیجهٔ آن ضرب تا ۱۸ رقم اعشار پیش می‌رود در حالی که
        /// ستون‌های دیتابیس decimal(28,8) هستند. اگر مقدارِ قفل‌شده و مقدارِ تسویه‌شده
        /// با دقت‌های متفاوت محاسبه شوند، پس از پایان سفارش یک باقی‌ماندهٔ کوچک در
        /// LockedBalance جا می‌ماند که در سیستم مالی یک نشتی تدریجی است.
        ///
        /// از MidpointRounding.ToEven استفاده نمی‌کنیم؛ AwayFromZero رفتار مورد انتظار
        /// در محاسبات مالی است.
        /// </summary>
        public static decimal RoundToCurrencyPrecision(decimal amount, string assetCode)
        {
            var info = GetCurrencyInfo(assetCode);
            // اگر دارایی ناشناخته بود، مقدار را دست‌نخورده برمی‌گردانیم تا رفتار موجود نشکند.
            if (info is null)
                return amount;

            return Math.Round(amount, info.DecimalPlaces, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// گرد کردن مبلغ به دقت دارایی، همیشه <b>رو به بالا</b>.
        ///
        /// برای مبلغی استفاده می‌شود که به‌عنوان وثیقه قفل می‌شود. با گرد کردن به بالا،
        /// مقدار قفل‌شده هرگز کمتر از ارزش واقعی سفارش نیست.
        /// </summary>
        public static decimal CeilingToCurrencyPrecision(decimal amount, string assetCode)
        {
            var info = GetCurrencyInfo(assetCode);
            if (info is null) return amount;

            var factor = Pow10(info.DecimalPlaces);
            return Math.Ceiling(amount * factor) / factor;
        }

        /// <summary>
        /// گرد کردن مبلغ به دقت دارایی، همیشه <b>رو به پایین</b>.
        ///
        /// برای مبلغی استفاده می‌شود که در هر معامله از وثیقه مصرف می‌گردد.
        ///
        /// چرا جهت‌ها عمداً مخالف هم انتخاب شده‌اند (issue #52): مبلغ قفل یک بار برای کل
        /// سفارش حساب می‌شود و مصرف در هر fill جداگانه. اگر هر دو با AwayFromZero گرد
        /// شوند، مجموعِ مصرف‌ها می‌تواند از مقدار قفل‌شده بیشتر شود و گاردِ «وثیقهٔ کافی
        /// نیست» یک معاملهٔ کاملاً معتبر را رد کند.
        ///
        /// با «قفل رو به بالا، مصرف رو به پایین» این نابرابری همیشه برقرار است:
        ///
        ///     Σ Floor(qᵢ × pᵢ) ≤ Σ qᵢ×pᵢ ≤ Σ qᵢ × p_سقف ≤ Q × p_سقف ≤ Ceiling(Q × p_سقف)
        ///
        /// یعنی «مجموع مصرف ≤ مقدار قفل‌شده» یک تضمین ریاضی است، نه یک احتمال — و به
        /// یکسان بودن قیمت fillها هم وابسته نیست، چون خریدار هرگز بیش از قیمت سقف
        /// سفارش خودش پرداخت نمی‌کند. اختلاف باقی‌مانده هنگام تکمیل یا لغو آزاد می‌شود.
        /// </summary>
        public static decimal FloorToCurrencyPrecision(decimal amount, string assetCode)
        {
            var info = GetCurrencyInfo(assetCode);
            if (info is null) return amount;

            var factor = Pow10(info.DecimalPlaces);
            return Math.Floor(amount * factor) / factor;
        }

        private static decimal Pow10(int exponent)
        {
            decimal result = 1m;
            for (var i = 0; i < exponent; i++) result *= 10m;
            return result;
        }

        /// <summary>
        /// دقت ذخیره‌سازی قیمت سفارش. ستون Orders.Price از نوع decimal(18,2) است.
        /// </summary>
        public const int OrderPriceDecimalPlaces = 2;

        /// <summary>
        /// گرد کردن قیمت به همان دقتی که ستون دیتابیس می‌تواند نگه دارد.
        ///
        /// چرا لازم است: قیمت مثقال بر ۴٫۳۳۱۸ تقسیم می‌شود و نتیجه تا ۲۸ رقم اعشار ادامه
        /// دارد (مثلاً ۷۹٬۰۰۰٬۰۰۰ ÷ ۴٫۳۳۱۸ = ۱۸۲۳۷۲۲۲٫۴۰۱۷۷۲۹…). مبلغِ قفل‌شده از همان
        /// مقدارِ کاملِ حافظه حساب می‌شد، ولی خودِ قیمت هنگام ذخیره در ستون به دو رقم
        /// اعشار گرد می‌شد و تسویه بعداً همان مقدار گردشده را از دیتابیس می‌خواند. دو
        /// طرفِ یک تساوی با دو قیمتِ متفاوت حساب می‌شدند و اختلافش تا ابد در
        /// LockedBalance می‌ماند (issue #52).
        ///
        /// با گرد کردن در ورودی، قفل و تسویه هر دو از یک عدد استفاده می‌کنند و
        /// «مبلغ قفل‌شده» از روی خودِ سفارش ذخیره‌شده قابل بازمحاسبه می‌شود.
        ///
        /// عمداً به PriceDecimalPlaces جفت معاملاتی تکیه نمی‌کنیم: برای MAUA/IRT مقدار
        /// آن صفر است در حالی که ستون دو رقم اعشار نگه می‌دارد. گرد کردن به صفر، دقت
        /// قیمت را از ۰٫۰۱ به ۱ تومان بر گرم تغییر می‌داد که یک تصمیم کسب‌وکاری است نه
        /// یک رفع باگ. مرجع اینجا ستون است، چون همان چیزی است که واقعاً محدود می‌کند.
        /// </summary>
        public static decimal RoundOrderPrice(decimal price) =>
            Math.Round(price, OrderPriceDecimalPlaces, MidpointRounding.AwayFromZero);

        /// <summary>گرفتن مشخصات جفت معاملاتی (case-insensitive). اگر پیدا نشود null.</summary>
        public static TradingPairInfo? GetTradingPairInfo(string symbol) =>
            symbol is not null && _pairs.TryGetValue(symbol, out var info) ? info : null;

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
            if (_currencies.TryGetValue(trimmed, out var byCode))
                return byCode.Code;

            // سپس تطبیق با نام فارسی کامل
            var byName = _currencies.Values.FirstOrDefault(c =>
                string.Equals(c.PersianName, trimmed, StringComparison.OrdinalIgnoreCase));

            if (byName is not null)
                return byName.Code;

            // در نهایت کلیدواژهٔ کوتاه («سکه»، «بیت») — همان فهرستی که دستورهای مظنه از آن
            // استفاده می‌کنند (TradingPairInfo.Aliases)، تا مدیر مجبور نباشد برای هر دستور
            // اسم متفاوتی به خاطر بسپارد.
            var bySymbolAlias = ResolveSymbolByAlias(trimmed);
            if (bySymbolAlias is not null)
                return GetTradingPairInfo(bySymbolAlias)?.BaseAsset;

            return null;
        }

        /// <summary>
        /// فهرست نام‌های فارسی ارزهای قابل‌تایپ برای نمایش در پیام خطا (به‌جای کدهای لاتین).
        ///
        /// نسخهٔ CREDIT_ هر دارایی عمداً حذف شده: دستورهای «ش»/«د» خودشان همیشه پیشوند
        /// CREDIT_ را اضافه می‌کنند، پس اگر ادمین «اعتبار آبشده» را مستقیم تایپ کند نتیجه یک
        /// کد دوبار-پیشونددار بی‌معنا («CREDIT_CREDIT_MAUA») می‌شود — این فهرست برای جلوگیری
        /// از همان تایپ است، نه تشویق آن.
        /// </summary>
        public static string GetPersianNamesList() =>
            string.Join("، ", _currencies.Values
                .Where(c => !c.Code.StartsWith("CREDIT_", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.PersianName));

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

        /// <summary>
        /// یک نماد را از روی کلیدواژهٔ فارسی دستورهای ادمین («سکه»، «بیت») برمی‌گرداند —
        /// از <see cref="TradingPairInfo.Aliases"/> هر نماد می‌خواند، پس اضافه‌کردن یک
        /// نماد جدید با کلیدواژهٔ خودش نیازی به تغییر این متد ندارد. کلیدواژهٔ خالی یعنی
        /// آبشده — عادت ادمین از قبل از وجود این نمادهای دیگر. کلیدواژه‌ای که به هیچ
        /// نمادی نمی‌خورد null برمی‌گرداند، تا فراخوان بتواند «کلیدواژه‌ای داده نشده» را
        /// از «کلیدواژهٔ ناشناخته» تشخیص دهد.
        /// </summary>
        public static string? ResolveSymbolByAlias(string? keyword)
        {
            var trimmed = keyword?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return MAUA_IRT;

            foreach (var pair in _pairs.Values)
            {
                if (pair.Aliases.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                    return pair.Symbol;
            }

            return null;
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

        /// <summary>نام فارسی دارایی پایه به‌تنهایی (مثل «آبشده»)</summary>
        public string BaseAssetPersianName { get; set; } = string.Empty;

        /// <summary>واحد نمایش دارایی پایه (مثل «گرم» یا «سکه»)</summary>
        public string BaseUnit { get; set; } = string.Empty;

        /// <summary>تعداد اعشار دارایی پایه، برای گرد کردن مبالغ (CurrenciesConstant.RoundToCurrencyPrecision)</summary>
        public int BaseDecimalPlaces { get; set; }

        /// <summary>کلیدواژه‌های فارسی که دستورهای ادمین در بات برای این نماد می‌پذیرند (مثل «سکه»)</summary>
        public List<string> Aliases { get; set; } = new();
    }
}
