using System.Reflection;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// یک معاملهٔ تطبیق‌شده باید از یک مسیر ساخته شود، نه دو مسیر (issue #40).
///
/// پیش‌تر دو جا <c>Trade.Create</c> را صدا می‌زدند:
/// <c>MatchingEngineService.CreateMakerTakerTrade</c> با نرخ کارمزد <c>0.000</c>، و
/// <c>OrderMatchingRepository.CreateTrade</c> با نرخ <c>0.001/0.002</c>. موتور اولی را
/// می‌ساخت و دور می‌ریخت و بلافاصله دومی را — از طریق <c>ExecuteAtomicMatchAsync</c> —
/// صدا می‌زد. نسخه‌ای که ذخیره و برای تسویه صف می‌شد همیشه دومی بود.
///
/// چرا این فقط «کد تکراری» نبود: خواندن کد موتور می‌گفت کارمزد صفر است، در حالی که معاملهٔ
/// ذخیره‌شده کارمزد داشت — آن هم به واحد ارز مظنه. نتیجه‌اش این شد که تسویهٔ هر معامله با
/// «کارمزد از مبلغ معامله بیشتر است» رد می‌شد و علتش در کدی پنهان بود که اصلاً اجرا
/// نمی‌شد. تعریف نرخ در یک جا، ولی خوانده‌شدن از جای دیگر، دقیقاً همان چیزی است که این
/// باگ را ساخت.
///
/// این تست ساختاری است، نه رفتاری: نمی‌شود با اجرای یک تطبیق ثابت کرد که مسیر دومی وجود
/// ندارد — مسیر دوم آن زمان هم اجرا می‌شد و هیچ تستی را نمی‌شکست، چون خروجی‌اش دور ریخته
/// می‌شد. تنها چیزی که می‌تواند برگشتنش را بگیرد، شمردن جایگاه‌های فراخوانی است.
/// </summary>
public class SingleTradeCreationPathTests
{
    /// <summary>
    /// اسمبلی‌هایی که می‌توانند معامله بسازند. <c>Orders.Core</c> هم هست چون
    /// <c>Trade.Create</c> آنجا تعریف شده و یک کارخانهٔ دوم در همان‌جا هم به‌اندازهٔ
    /// کارخانه‌ای در لایهٔ بالاتر مشکل‌ساز است.
    /// </summary>
    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(MatchingEngineService).Assembly,   // Orders.Application
        typeof(OrderMatchingRepository).Assembly, // Orders.Infrastructure
        typeof(Trade).Assembly                    // Orders.Core
    ];

    private static readonly MethodInfo TradeCreate =
        typeof(Trade).GetMethod(nameof(Trade.Create), BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException("Trade.Create not found — did it get renamed?");

    [Fact]
    public void TradeCreate_IsCalledFromExactlyOnePlace()
    {
        var callers = FindCallersOf(TradeCreate);

        Assert.Equal(
            [$"{nameof(OrderMatchingRepository)}.CreateTrade"],
            callers);
    }

    /// <summary>
    /// نرخ کارمزد هم باید فقط در همان یک مسیر تعریف شود. اگر جای دیگری نرخ خودش را داشته
    /// باشد، همان تناقضی برمی‌گردد که باعث شد کد یک عدد بگوید و پایگاه‌داده عدد دیگری
    /// نشان بدهد — حتی اگر آن کد معامله‌ای نسازد و فقط نرخ را برای گزارش حساب کند.
    /// </summary>
    [Fact]
    public void MatchingEngine_DoesNotDeclareItsOwnFeeRates()
    {
        var engineFields = typeof(MatchingEngineService)
            .GetFields(BindingFlags.Instance | BindingFlags.Static |
                       BindingFlags.Public | BindingFlags.NonPublic)
            .Select(f => f.Name);

        Assert.DoesNotContain(engineFields, n => n.Contains("FeeRate", StringComparison.OrdinalIgnoreCase));

        var engineMethods = typeof(MatchingEngineService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Static |
                        BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.DeclaringType == typeof(MatchingEngineService));

        Assert.DoesNotContain(engineMethods, m => m.ReturnType == typeof(Trade));
    }

    // ── پیمایش IL ───────────────────────────────────────────────────────────────

    /// <summary>
    /// نام «تایپ.متد» هر جایی که <paramref name="target"/> را صدا می‌زند، مرتب‌شده.
    ///
    /// متدهای async به یک ماشین حالتِ تولیدشدهٔ کامپایلر تبدیل می‌شوند، پس فراخوانی واقعی
    /// داخل <c>MoveNext</c> یک تایپ تودرتوی مخفی می‌نشیند. اسم آن تایپ (مثلاً
    /// <c>&lt;ExecuteAtomicMatchAsync&gt;d__12</c>) به تایپ و متد اصلی برگردانده می‌شود تا
    /// پیام شکست تست قابل‌خواندن بماند.
    /// </summary>
    private static string[] FindCallersOf(MethodInfo target) =>
        ProductionAssemblies
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                          BindingFlags.Public | BindingFlags.NonPublic |
                                          BindingFlags.DeclaredOnly)
                              .Cast<MethodBase>()
                              .Concat(t.GetConstructors(BindingFlags.Instance | BindingFlags.Static |
                                                        BindingFlags.Public | BindingFlags.NonPublic)))
            .Where(m => Calls(m, target))
            .Select(DisplayName)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    private const byte OpCall = 0x28;
    private const byte OpCallvirt = 0x6F;

    /// <summary>
    /// آیا بدنهٔ <paramref name="caller"/> شامل <c>call</c>/<c>callvirt</c> به
    /// <paramref name="target"/> هست؟
    ///
    /// این پیمایش تقریبی است: هر بایت <c>0x28</c>/<c>0x6F</c> را می‌بیند و ۴ بایت بعدی را
    /// به‌عنوان توکن متادیتا حل می‌کند، بدون اینکه طول دستورها را دقیق دنبال کند. یعنی
    /// ممکن است بایتی که در واقع عملوند است را دستور بپندارد. برای این تست بی‌خطر است:
    /// چنین تصادفی باید دقیقاً به <c>Trade.Create</c> حل شود تا مثبت کاذب بسازد، و از طرف
    /// دیگر هیچ فراخوانی واقعی‌ای را از دست نمی‌دهد — که سمت مهم ماجراست، چون کار این تست
    /// پیدا کردن مسیر دومِ برگشته است.
    /// </summary>
    private static bool Calls(MethodBase caller, MethodInfo target)
    {
        byte[]? il;
        try { il = caller.GetMethodBody()?.GetILAsByteArray(); }
        catch { return false; } // متدهای abstract/extern بدنه ندارند

        if (il is null) return false;

        var typeArgs = caller.DeclaringType?.IsGenericType == true
            ? caller.DeclaringType.GetGenericArguments()
            : null;
        var methodArgs = caller.IsGenericMethod ? caller.GetGenericArguments() : null;

        for (var i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != OpCall && il[i] != OpCallvirt) continue;

            var token = BitConverter.ToInt32(il, i + 1);

            try
            {
                if (caller.Module.ResolveMethod(token, typeArgs, methodArgs) is MethodInfo m &&
                    m.MetadataToken == target.MetadataToken &&
                    m.Module == target.Module)
                {
                    return true;
                }
            }
            catch
            {
                // توکن معتبر نبود — یعنی این بایت اصلاً دستور نبوده. رد شو.
            }
        }

        return false;
    }

    /// <summary>
    /// <c>&lt;X&gt;d__7.MoveNext</c> را به <c>DeclaringType.X</c> برمی‌گرداند تا خروجی تست
    /// نام متد واقعی را نشان بدهد، نه نام تولیدشدهٔ کامپایلر.
    /// </summary>
    private static string DisplayName(MethodBase method)
    {
        var type = method.DeclaringType!;
        var name = method.Name;

        if (type.Name.StartsWith('<') && type.DeclaringType is not null)
        {
            name = type.Name[1..type.Name.IndexOf('>')];
            type = type.DeclaringType;
        }

        return $"{type.Name}.{name}";
    }
}
