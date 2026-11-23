"use client";

import Navbar from "@/components/Navbar";
import Footer from "@/components/Footer";
import Link from "next/link";
import { useState } from "react";
import { brandConfig } from "@/config/brand.config";
import {
  BrandHeroShowcase,
  ZarorbitSymbolSvg,
} from "@/components/BrandLogo";
import ZarorbitOrbitalMotion from "@/components/ZarorbitOrbitalMotion";
import {
  Send,
  ShieldCheck,
  BookOpenCheck,
  Scale,
  Clock,
  ChevronLeft,
  Wallet,
  FileCheck2,
  Calculator,
  Compass,
  Building2,
  ArrowLeftRight,
} from "lucide-react";

// Iranian market mesghal equivalence for 18k (750) vs 17k (705) melted gold:
// 1 mesghal = 4.6083g of 705 gold ≈ 4.3318g of 750 (18k) gold.
const MESGHAL_TO_18K_GRAM = 4.3318;

export default function HomePage() {
  const [unit, setUnit] = useState<"gram" | "mesghal">("gram");
  const [amountInput, setAmountInput] = useState<string>("10");
  const [mesghalPriceInput, setMesghalPriceInput] = useState<string>("");

  const numericAmount = Math.max(0, parseFloat(amountInput) || 0);
  const parsedMesghalPrice = Math.max(
    0,
    parseFloat(mesghalPriceInput.replace(/,/g, "")) || 0
  );
  const gramRate =
    parsedMesghalPrice > 0 ? parsedMesghalPrice / MESGHAL_TO_18K_GRAM : 0;

  const equivalentGrams =
    unit === "gram" ? numericAmount : numericAmount * MESGHAL_TO_18K_GRAM;
  const equivalentMesghals =
    unit === "mesghal" ? numericAmount : numericAmount / MESGHAL_TO_18K_GRAM;

  const estimatedTotal =
    parsedMesghalPrice > 0
      ? Math.round(equivalentMesghals * parsedMesghalPrice)
      : 0;

  return (
    <>
      <Navbar />

      <main className="relative flex-1">
        {/* ----------------- HERO SECTION (PRODUCT-FIRST REALITY) ----------------- */}
        <section className="relative pt-12 pb-20 md:pt-20 md:pb-28 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
            <div className="lg:col-span-7 space-y-7 text-center lg:text-right">
              <div className="inline-flex items-center gap-2.5 rounded-full border border-[#2B303A] bg-[#1F2329] px-4 py-1.5 text-xs font-semibold text-[#C9A45A]">
                <ZarorbitSymbolSvg sizePx={16} />
                <span className="bidi-isolate">
                  {brandConfig.slogan.categoryEn}
                </span>
              </div>

              <h1 className="text-4xl sm:text-5xl md:text-[50px] font-black tracking-tight leading-[1.28] text-[#F2EEE5]">
                زیرساخت شفاف{" "}
                <span className="text-[#C9A45A] block mt-2">
                  معاملات طلای آبشده و دفترکل متقابل.
                </span>
              </h1>

              <p className="text-base sm:text-lg text-[#9CA3AF] max-w-2xl font-light leading-relaxed mx-auto lg:mx-0">
                <span className="text-[#F2EEE5] font-semibold">
                  {brandConfig.name.fa}
                </span>{" "}
                (
                <span className="text-[#F2EEE5] font-semibold bidi-isolate">
                  {brandConfig.name.en}
                </span>
                ) سامانه معاملاتی و حسابداری دقیق برای طلافروشی‌ها و مشتریان
                تاییدشده آن‌هاست: اعلام مظنه لحظه‌ای خرید و فروش، ثبت فوری معامله
                در{" "}
                <strong className="text-[#F2EEE5] font-medium">
                  ربات تلگرام
                </strong>
                ، دفترکل اعتبار متقابل و کارمزد معاملاتی{" "}
                <strong className="text-[#C9A45A] font-medium">۰.۰۰٪</strong>{" "}
                بر پایه اسپرد شفاف.
              </p>

              {/* Primary Actions */}
              <div className="flex flex-col sm:flex-row items-center justify-center lg:justify-start gap-4 pt-2">
                <a
                  href={brandConfig.telegram.botUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="w-full sm:w-auto inline-flex items-center justify-center gap-2.5 rounded-[10px] bg-[#C9A45A] px-7 py-3.5 text-sm font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
                >
                  <Send className="h-4 w-4" />
                  <span>شروع در ربات تلگرام</span>
                </a>
                <Link
                  href="/login"
                  className="w-full sm:w-auto inline-flex items-center justify-center gap-2.5 rounded-[10px] bg-[#286B63] px-7 py-3.5 text-sm font-bold text-white hover:bg-[#328076] transition-colors"
                >
                  <Wallet className="h-4 w-4" />
                  <span>ورود به پورتال دفترکل</span>
                </Link>
                <Link
                  href="/vision"
                  className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-[10px] border border-[#6B7485] bg-[#1F2329] px-5 py-3.5 text-sm font-semibold text-[#F2EEE5] hover:border-[#C9A45A] transition-colors"
                >
                  <span>چشم‌انداز اکوسیستم</span>
                  <ChevronLeft className="h-4 w-4" />
                </Link>
              </div>

              {/* Three Operational Guarantees */}
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-5 text-xs text-[#9CA3AF] border-t border-[#2B303A] max-w-2xl mx-auto lg:mx-0">
                <div className="flex items-center gap-2 justify-center lg:justify-start">
                  <ShieldCheck className="h-4 w-4 text-[#48B884] shrink-0" />
                  <span>طرف حساب مستقیم و شفاف</span>
                </div>
                <div className="flex items-center gap-2 justify-center lg:justify-start">
                  <Scale className="h-4 w-4 text-[#C9A45A] shrink-0" />
                  <span>کارمزد تراکنش ۰٪ • اسپرد روشن</span>
                </div>
                <div className="flex items-center gap-2 justify-center lg:justify-start">
                  <BookOpenCheck className="h-4 w-4 text-[#4CB3A5] shrink-0" />
                  <span>ثبت آنی در دفترکل حسابداری</span>
                </div>
              </div>
            </div>

            {/* Hero Institutional Showcase Card */}
            <div className="lg:col-span-5 flex justify-center">
              <div className="w-full max-w-md">
                <BrandHeroShowcase />
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- CORE PLATFORM CAPABILITIES (LIVE TODAY) ----------------- */}
        <section
          id="platform"
          className="py-20 bg-[#101215] border-y border-[#2B303A]"
        >
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex flex-col md:flex-row md:items-end justify-between mb-12 gap-4">
              <div>
                <div className="text-xs font-bold text-[#C9A45A] bidi-isolate uppercase tracking-wider mb-1">
                  ACTIVE TRADING INFRASTRUCTURE
                </div>
                <h2 className="text-2xl sm:text-3xl font-black text-[#F2EEE5]">
                  امکانات عملیاتی امروز برای طلافروشی و مشتریان
                </h2>
              </div>
              <p className="text-xs text-[#9CA3AF] max-w-md leading-relaxed">
                طراحی‌شده برای جایگزینی سفارش‌گیری تلفنی و دفترچه‌های دستی با
                جریان دقیق، سریع و قابل ممیزی در تلگرام و وب.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              <div className="zar-card-interactive p-6 flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between mb-4">
                    <div className="h-10 w-10 rounded-xl bg-[#2B2416] border border-[#82611C] flex items-center justify-center text-[#C9A45A]">
                      <Clock className="h-5 w-5" />
                    </div>
                    <span className="rounded-full bg-[#162A24] border border-[#286B63] px-2.5 py-0.5 text-[11px] font-semibold text-[#48B884]">
                      فعال
                    </span>
                  </div>
                  <h3 className="text-base font-bold text-[#F2EEE5]">
                    اعلام مظنه لحظه‌ای (Dealer Model)
                  </h3>
                  <p className="text-xs text-[#9CA3AF] mt-2 leading-relaxed">
                    انتشار نرخ خرید و فروش توسط مدیر طلافروشی با کنترل بازه
                    معقولیت قیمت؛ مشتری مستقیم روی مظنه معتبر معامله می‌کند.
                  </p>
                </div>
                <div className="mt-5 pt-3 border-t border-[#2B303A] text-[11px] text-[#9CA3AF]">
                  <strong className="text-[#F2EEE5]">مزیت:</strong> حذف اختلاف
                  قیمتی تماس‌های تلفنی
                </div>
              </div>

              <div className="zar-card-interactive p-6 flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between mb-4">
                    <div className="h-10 w-10 rounded-xl bg-[#162A24] border border-[#286B63] flex items-center justify-center text-[#4CB3A5]">
                      <ArrowLeftRight className="h-5 w-5" />
                    </div>
                    <span className="rounded-full bg-[#162A24] border border-[#286B63] px-2.5 py-0.5 text-[11px] font-semibold text-[#48B884]">
                      فعال
                    </span>
                  </div>
                  <h3 className="text-base font-bold text-[#F2EEE5]">
                    اعتبار متقابل بین‌دارایی
                  </h3>
                  <p className="text-xs text-[#9CA3AF] mt-2 leading-relaxed">
                    پشتیبانی از اعتبار ریالی/طلایی تعریف‌شده توسط طلافروشی برای
                    مشتریان شناخته‌شده، بدون نیاز به درگاه پرداخت اجباری در لحظه
                    ثبت.
                  </p>
                </div>
                <div className="mt-5 pt-3 border-t border-[#2B303A] text-[11px] text-[#9CA3AF]">
                  <strong className="text-[#F2EEE5]">تسویه:</strong> همگام با
                  رویه واقعی بازار طلا
                </div>
              </div>

              <div className="zar-card-interactive p-6 flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between mb-4">
                    <div className="h-10 w-10 rounded-xl bg-[#282D35] border border-[#3A414D] flex items-center justify-center text-[#F2EEE5]">
                      <BookOpenCheck className="h-5 w-5" />
                    </div>
                    <span className="rounded-full bg-[#162A24] border border-[#286B63] px-2.5 py-0.5 text-[11px] font-semibold text-[#48B884]">
                      فعال
                    </span>
                  </div>
                  <h3 className="text-base font-bold text-[#F2EEE5]">
                    دفترکل دقیق و تاریخچه ممیزی
                  </h3>
                  <p className="text-xs text-[#9CA3AF] mt-2 leading-relaxed">
                    ثبت تراکنش‌ها، مانده طلایی و ریالی، تاریخچه مظنه‌ها و گزارش
                    معاملات با تاریخ شمسی دقیق برای مشتری و مدیر.
                  </p>
                </div>
                <div className="mt-5 pt-3 border-t border-[#2B303A] text-[11px] text-[#9CA3AF]">
                  <strong className="text-[#F2EEE5]">پورتال وب:</strong> مشاهده
                  موجودی از طریق پنل کیف پول
                </div>
              </div>

              <div className="zar-card-interactive p-6 flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between mb-4">
                    <div className="h-10 w-10 rounded-xl bg-[#2B2416] border border-[#82611C] flex items-center justify-center text-[#C9A45A]">
                      <Building2 className="h-5 w-5" />
                    </div>
                    <span className="rounded-full bg-[#192535] border border-[#2D4D73] px-2.5 py-0.5 text-[11px] font-semibold text-[#6BA4E8]">
                      آماده پایلوت
                    </span>
                  </div>
                  <h3 className="text-base font-bold text-[#F2EEE5]">
                    معماری اختصاصی برای طلافروشان
                  </h3>
                  <p className="text-xs text-[#9CA3AF] mt-2 leading-relaxed">
                    مشتری با کد معرف و تایید طلافروشی خود وارد می‌شود؛ کنترل
                    کامل سقف اعتبار، نرخ و تسویه فیزیکی در اختیار طلافروشی است.
                  </p>
                </div>
                <div className="mt-5 pt-3 border-t border-[#2B303A] text-[11px] text-[#9CA3AF]">
                  <strong className="text-[#F2EEE5]">مالکیت مشتری:</strong> حفظ
                  کامل ارتباط مستقیم طلافروشی
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- WEIGHT & VALUE CALCULATOR (USER-INPUT RATE, ZERO FAKE PRICES) ----------------- */}
        <section id="calculator" className="py-20">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-start">
              <div className="lg:col-span-5 space-y-5">
                <div className="inline-flex items-center gap-2 text-xs font-bold text-[#4CB3A5] bidi-isolate uppercase tracking-wider">
                  <Calculator className="h-4 w-4" />
                  <span>MELTED GOLD CONVERSION TOOL</span>
                </div>
                <h2 className="text-2xl sm:text-3xl font-black text-[#F2EEE5] leading-snug">
                  محاسبه‌گر تبدیل مثقال و گرم (طلای ۱۸ عیار)
                </h2>
                <p className="text-sm text-[#9CA3AF] leading-relaxed">
                  برای جلوگیری از نمایش نرخ‌های غیرواقعی یا منقضی، هیچ قیمت
                  ثابتی در وب‌سایت درج نمی‌شود. مظنه زنده هر مثقال را از ربات یا
                  طلافروشی خود وارد کنید تا معادل گرم ۱۸ عیار و ارزش کل بلافاصله
                  محاسبه شود.
                </p>
                <div className="rounded-xl bg-[#1F2329] border border-[#2B303A] p-4 text-xs text-[#9CA3AF] space-y-2">
                  <div className="flex items-center justify-between">
                    <span>ضریب تبدیل استاندارد مثقال ۱۷ به گرم ۱۸ عیار:</span>
                    <span className="bidi-isolate font-mono font-semibold text-[#C9A45A]">
                      {MESGHAL_TO_18K_GRAM} g
                    </span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span>کارمزد تراکنش فعلی در سامانه:</span>
                    <span className="font-semibold text-[#48B884]">
                      ۰.۰۰٪ (بدون کارمزد جداگانه)
                    </span>
                  </div>
                </div>
              </div>

              <div className="lg:col-span-7 rounded-[20px] bg-[#1F2329] border border-[#2B303A] p-6 sm:p-8 shadow-card space-y-6">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  {/* Unit selector & amount */}
                  <div className="space-y-2">
                    <label
                      htmlFor="gold-amount-input"
                      className="block text-xs font-semibold text-[#F2EEE5]"
                    >
                      مقدار طلا
                    </label>
                    <div className="flex rounded-[10px] overflow-hidden border border-[#6B7485] bg-[#17191D]">
                      <input
                        id="gold-amount-input"
                        type="number"
                        min="0"
                        step="0.01"
                        value={amountInput}
                        onChange={(e) => setAmountInput(e.target.value)}
                        className="w-full bg-transparent px-3.5 py-2.5 text-sm text-[#F2EEE5] tabular-nums focus:outline-none"
                        placeholder="مثلاً ۱۰"
                      />
                      <div className="flex border-r border-[#2B303A]">
                        <button
                          type="button"
                          onClick={() => setUnit("gram")}
                          className={`px-3 text-xs font-semibold transition-colors ${
                            unit === "gram"
                              ? "bg-[#C9A45A] text-[#17191D]"
                              : "text-[#9CA3AF] hover:text-[#F2EEE5]"
                          }`}
                        >
                          گرم
                        </button>
                        <button
                          type="button"
                          onClick={() => setUnit("mesghal")}
                          className={`px-3 text-xs font-semibold transition-colors ${
                            unit === "mesghal"
                              ? "bg-[#C9A45A] text-[#17191D]"
                              : "text-[#9CA3AF] hover:text-[#F2EEE5]"
                          }`}
                        >
                          مثقال
                        </button>
                      </div>
                    </div>
                  </div>

                  {/* Live dealer quote input */}
                  <div className="space-y-2">
                    <label
                      htmlFor="mesghal-rate-input"
                      className="block text-xs font-semibold text-[#F2EEE5]"
                    >
                      مظنه یک مثقال (تومان — اختیاری)
                    </label>
                    <input
                      id="mesghal-rate-input"
                      type="text"
                      inputMode="numeric"
                      value={mesghalPriceInput}
                      onChange={(e) => setMesghalPriceInput(e.target.value)}
                      className="w-full rounded-[10px] border border-[#6B7485] bg-[#17191D] px-3.5 py-2.5 text-sm text-[#F2EEE5] tabular-nums focus:outline-none focus:border-[#C9A45A]"
                      placeholder="مظنه فعلی بازار را وارد کنید"
                    />
                  </div>
                </div>

                {/* Calculated Output Grid */}
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-2">
                  <div className="rounded-xl bg-[#17191D] border border-[#2B303A] p-4">
                    <div className="text-[11px] text-[#9CA3AF] mb-1">
                      معادل گرم (۱۸ عیار)
                    </div>
                    <div className="text-lg font-bold text-[#F2EEE5] tabular-nums">
                      {equivalentGrams.toLocaleString("fa-IR", {
                        maximumFractionDigits: 3,
                      })}{" "}
                      <span className="text-xs font-normal text-[#9CA3AF]">
                        گرم
                      </span>
                    </div>
                  </div>

                  <div className="rounded-xl bg-[#17191D] border border-[#2B303A] p-4">
                    <div className="text-[11px] text-[#9CA3AF] mb-1">
                      معادل مثقال آبشده
                    </div>
                    <div className="text-lg font-bold text-[#4CB3A5] tabular-nums">
                      {equivalentMesghals.toLocaleString("fa-IR", {
                        maximumFractionDigits: 4,
                      })}{" "}
                      <span className="text-xs font-normal text-[#9CA3AF]">
                        مثقال
                      </span>
                    </div>
                  </div>

                  <div className="rounded-xl bg-[#17191D] border border-[#82611C]/60 p-4">
                    <div className="text-[11px] text-[#9CA3AF] mb-1">
                      برآورد ارزش کل
                    </div>
                    <div className="text-lg font-bold text-[#C9A45A] tabular-nums">
                      {estimatedTotal > 0 ? (
                        <>
                          {estimatedTotal.toLocaleString("fa-IR")}{" "}
                          <span className="text-xs font-normal text-[#9CA3AF]">
                            تومان
                          </span>
                        </>
                      ) : (
                        <span className="text-xs font-normal text-[#9CA3AF]">
                          مظنه را وارد کنید
                        </span>
                      )}
                    </div>
                  </div>
                </div>

                {gramRate > 0 && (
                  <div className="flex items-center justify-between text-xs text-[#9CA3AF] border-t border-[#2B303A] pt-3">
                    <span>نرخ محاسبه‌شده هر گرم طلای ۱۸ عیار:</span>
                    <span className="font-semibold text-[#F2EEE5] tabular-nums">
                      {Math.round(gramRate).toLocaleString("fa-IR")} تومان
                    </span>
                  </div>
                )}
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- TRANSPARENCY & ORBITAL ARCHITECTURE ----------------- */}
        <section
          id="transparency"
          className="py-20 bg-[#101215] border-y border-[#2B303A]"
        >
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
              <div className="lg:col-span-6 space-y-6">
                <div className="text-xs font-bold text-[#C9A45A] bidi-isolate uppercase tracking-wider">
                  TRANSPARENCY &amp; OPERATIONAL BOUNDARIES
                </div>
                <h2 className="text-2xl sm:text-3xl font-black text-[#F2EEE5]">
                  شفافیت معاملاتی و مرزهای حقوقی
                </h2>
                <p className="text-sm text-[#9CA3AF] leading-relaxed">
                  در{" "}
                  <span className="text-[#F2EEE5] font-semibold">
                    {brandConfig.name.fa}
                  </span>
                  ، اعتماد بر پایه بیان دقیق واقعیت فنی و حقوقی ساخته می‌شود، نه
                  شعارهای تبلیغاتی. پیش از هر معامله، چهار اصل زیر برای طرفین
                  روشن است:
                </p>

                <div className="rounded-2xl bg-[#1F2329] border border-[#2B303A] p-5 space-y-3.5 text-xs">
                  <div className="flex items-center gap-2 text-[#C9A45A] font-bold">
                    <FileCheck2 className="h-4 w-4 shrink-0" />
                    <span>اصول شفافیت حساب و دارایی</span>
                  </div>
                  <ul className="space-y-2.5 text-[#9CA3AF] leading-relaxed">
                    <li className="flex items-start gap-2">
                      <span className="text-[#48B884] font-bold">•</span>
                      <span>
                        <strong className="text-[#F2EEE5]">طرف حساب مستقیم:</strong>{" "}
                        معامله میان مشتری تاییدشده و طلافروشی (بازارگردان) ثبت
                        می‌شود و شرایط تسویه فیزیکی و ریالی مستقیماً میان دو طرف
                        تعریف می‌گردد.
                      </span>
                    </li>
                    <li className="flex items-start gap-2">
                      <span className="text-[#48B884] font-bold">•</span>
                      <span>
                        <strong className="text-[#F2EEE5]">ساختار درآمد روشن:</strong>{" "}
                        کارمزد معاملاتی فعلی ۰.۰۰٪ است و مابه‌التفاوت قیمت خرید
                        و فروش (اسپرد) پیش از تایید معامله نمایش داده می‌شود.
                      </span>
                    </li>
                    <li className="flex items-start gap-2">
                      <span className="text-[#48B884] font-bold">•</span>
                      <span>
                        <strong className="text-[#F2EEE5]">ثبت قابل ممیزی:</strong>{" "}
                        هر معامله، اعتبار و تسویه با شناسه یکتا در دفترکل ثبت
                        می‌شود و تاریخچه آن در ربات و پورتال کیف پول قابل بررسی
                        است.
                      </span>
                    </li>
                    <li className="flex items-start gap-2">
                      <span className="text-[#48B884] font-bold">•</span>
                      <span>
                        <strong className="text-[#F2EEE5]">تفکیک امروز از فردا:</strong>{" "}
                        قابلیت‌های فعلی روی طلای آبشده متمرکز است و توسعه
                        دارایی‌های دیجیتال و حکمرانی جمعی به‌صورت مرحله‌ای در
                        صفحه چشم‌انداز پیگیری می‌شود.
                      </span>
                    </li>
                  </ul>
                </div>
              </div>

              <div className="lg:col-span-6">
                <ZarorbitOrbitalMotion />
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- LONG-TERM VISION GATEWAY ----------------- */}
        <section className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="rounded-[24px] bg-[#1F2329] border border-[#2B303A] p-8 sm:p-10 flex flex-col lg:flex-row items-center justify-between gap-8">
            <div className="space-y-3 text-center lg:text-right max-w-2xl">
              <div className="inline-flex items-center gap-2 text-xs font-bold text-[#4CB3A5]">
                <Compass className="h-4 w-4" />
                <span>چشم‌انداز بلندمدت اکوسیستم</span>
              </div>
              <h2 className="text-2xl sm:text-3xl font-black text-[#F2EEE5]">
                از زیرساخت معاملات طلا به سوی زیست‌بوم دارایی‌های واقعی و دیجیتال
              </h2>
              <p className="text-sm text-[#9CA3AF] leading-relaxed">
                معماری ماژولار{" "}
                <span className="bidi-isolate text-[#F2EEE5]">ZARORBIT</span>{" "}
                طوری طراحی شده که پس از تثبیت معاملات طلا، به‌صورت گام‌به‌گام به
                سایر دارایی‌های واقعی (RWA)، دارایی‌های دیجیتال و مشارکت جمعی
                (DAO) گسترش یابد. نقشه راه صادقانه و وضعیت بلوغ هر بخش را در
                صفحه چشم‌انداز ببینید.
              </p>
            </div>
            <div className="flex flex-col sm:flex-row gap-3 shrink-0">
              <Link
                href="/vision"
                className="inline-flex items-center justify-center gap-2 rounded-[10px] bg-[#C9A45A] px-6 py-3.5 text-sm font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
              >
                <span>مشاهده چشم‌انداز و نقشه راه</span>
                <ChevronLeft className="h-4 w-4" />
              </Link>
              <a
                href={brandConfig.telegram.channelUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center justify-center gap-2 rounded-[10px] border border-[#6B7485] px-5 py-3.5 text-sm font-semibold text-[#F2EEE5] hover:border-[#4CB3A5] hover:text-[#4CB3A5] transition-colors"
              >
                <span className="bidi-isolate">
                  @{brandConfig.telegram.channelUsername}
                </span>
              </a>
            </div>
          </div>
        </section>
      </main>

      <Footer />
    </>
  );
}
