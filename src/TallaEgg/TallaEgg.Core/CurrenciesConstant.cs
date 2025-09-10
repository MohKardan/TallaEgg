namespace TallaEgg.Core
{
    public class CurrenciesConstant
    {
        // 🔹 ثابت‌ها برای استفاده راحت در کد
        public const string Maua = "MAUA";
        public const string Rial = "IRR";

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



}