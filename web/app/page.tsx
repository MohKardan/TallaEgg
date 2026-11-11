"use client";

import Navbar from "@/components/Navbar";
import Footer from "@/components/Footer";
import Link from "next/link";
import Image from "next/image";
import { useState } from "react";
import { brandConfig } from "@/config/brand.config";
import { BrandEmblem, BrandHeroShowcase } from "@/components/BrandLogo";
import ZarorbitOrbitalMotion from "@/components/ZarorbitOrbitalMotion";
import {
  Sparkles,
  ArrowRight,
  Send,
  ShieldCheck,
  TrendingUp,
  Zap,
  Repeat,
  Scale,
  Award,
  ChevronLeft,
  CheckCircle2,
  Lock,
  Wallet,
  Coins,
  Flame,
  Check,
  X as CloseIcon,
  HelpCircle,
  Building2,
} from "lucide-react";

export default function HomePage() {
  // Calculator state
  const [unit, setUnit] = useState<"gram" | "mesghal">("gram");
  const [amountInput, setAmountInput] = useState<string>("10");
  const numericAmount = Math.max(0, parseFloat(amountInput) || 0);

  // Representative live benchmark rates (Toman)
  const gramRate = 4350000; // 4,350,000 Toman per gram of 18k melted gold
  const mesghalRate = Math.round(gramRate * 4.3318); // 1 mesghal = 4.3318 grams of 17k/18k

  const totalCalculated =
    unit === "gram" ? numericAmount * gramRate : numericAmount * mesghalRate;

  // Comparison savings calculation (traditional jewelry shops charge ~7% making fee + 9% tax on fee)
  const traditionalFeeEst = Math.round(totalCalculated * 0.035);

  return (
    <>
      <Navbar />

      <main className="relative flex-1">
        {/* Ambient atmospheric glows */}
        <div className="ambient-glow top-0 right-1/4 -translate-y-1/2 opacity-70" />
        <div className="ambient-glow top-[700px] left-[-150px] opacity-40" />
        <div className="ambient-glow bottom-[400px] right-[-100px] opacity-50" />

        {/* ----------------- HERO SECTION ----------------- */}
        <section className="relative pt-12 pb-20 md:pt-20 md:pb-32 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
            {/* Right Column (Text & Value Proposition in RTL) */}
            <div className="lg:col-span-7 space-y-8 text-center lg:text-right">
              {/* Pill Badge */}
              <div className="inline-flex items-center gap-2.5 rounded-full border border-amber-500/40 bg-gradient-to-r from-amber-500/15 to-transparent px-4 py-1.5 text-xs font-semibold text-amber-300 backdrop-blur-md shadow-goldGlowSubtle">
                <div className="h-4 w-4 rounded-full overflow-hidden border border-amber-400/40 shrink-0">
                  <Image
                    src={brandConfig.assets.iconNoOrbit}
                    alt="Icon"
                    width={16}
                    height={16}
                    className="object-cover"
                  />
                </div>
                <span>{brandConfig.slogan.en} • کارمزد معامله ۰.۰۰٪</span>
              </div>

              {/* Main Headline */}
              <h1 className="text-4xl sm:text-5xl md:text-6xl font-black tracking-tight leading-[1.25] text-white">
                مدار پیوسته و هوشمند{" "}
                <span className="gold-gradient-text block mt-2">
                  معاملات طلای آبشده ۱۸ عیار
                </span>
              </h1>

              {/* Subhead with clear, honest value proposition */}
              <p className="text-base sm:text-lg text-slate-300 max-w-2xl font-light leading-relaxed mx-auto lg:mx-0">
                خرید و فروش مستقیم در کسر ثانیه با میز بازارساز اختصاصی {brandConfig.name.fa}؛ بدون صف انتظار، بدون کارمزد مخفی و مالیات، همراه با تسویه آنی دفترکل و تضمین تحویل فیزیکی شمش معتبر.
              </p>

              {/* Action Buttons */}
              <div className="flex flex-col sm:flex-row items-center justify-center lg:justify-start gap-4 pt-2">
                <Link
                  href="/login"
                  className="w-full sm:w-auto inline-flex items-center justify-center gap-3 rounded-xl bg-gradient-to-r from-amber-400 via-amber-500 to-amber-600 px-8 py-4 text-base font-bold text-slate-950 shadow-goldGlow hover:brightness-110 active:scale-[0.98] transition-all"
                >
                  <Wallet className="h-5 w-5" />
                  <span>ورود به پورتال و مشاهده موجودی</span>
                </Link>
                <a
                  href={brandConfig.telegram.botUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="w-full sm:w-auto inline-flex items-center justify-center gap-3 rounded-xl border border-amber-500/40 bg-[#121620]/90 px-8 py-4 text-base font-semibold text-amber-300 hover:bg-amber-500/10 active:scale-[0.98] transition-all backdrop-blur-md"
                >
                  <Send className="h-5 w-5" />
                  <span>معامله در ربات تلگرام</span>
                </a>
              </div>

              {/* Quick Guarantees (Small Falcon Icon on each) */}
              <div className="grid grid-cols-2 sm:grid-cols-3 gap-4 pt-4 text-xs text-slate-300 border-t border-slate-800/80 w-full max-w-xl mx-auto lg:mx-0">
                <div className="flex items-center gap-2 justify-center lg:justify-start">
                  <div className="h-5 w-5 rounded-md overflow-hidden border border-amber-500/30 shrink-0">
                    <Image
                      src={brandConfig.assets.iconNoOrbit}
                      alt="Icon"
                      width={20}
                      height={20}
                      className="object-cover"
                    />
                  </div>
                  <span>تسویه آنی دفترکل</span>
                </div>
                <div className="flex items-center gap-2 justify-center lg:justify-start">
                  <div className="h-5 w-5 rounded-md overflow-hidden border border-amber-500/30 shrink-0">
                    <Image
                      src={brandConfig.assets.iconNoOrbit}
                      alt="Icon"
                      width={20}
                      height={20}
                      className="object-cover"
                    />
                  </div>
                  <span>کارمزد معامله صفر</span>
                </div>
                <div className="col-span-2 sm:col-span-1 flex items-center gap-2 justify-center lg:justify-start">
                  <div className="h-5 w-5 rounded-md overflow-hidden border border-amber-500/30 shrink-0">
                    <Image
                      src={brandConfig.assets.iconNoOrbit}
                      alt="Icon"
                      width={20}
                      height={20}
                      className="object-cover"
                    />
                  </div>
                  <span>طلای با انگ و ری‌گیری رسمی</span>
                </div>
              </div>
            </div>

            {/* Left Column (Flagship 3D Showcase Card in RTL) */}
            <div className="lg:col-span-5 flex justify-center">
              <div className="relative w-full max-w-md">
                {/* Large Orbital Binary Rings Logo Showcase */}
                <BrandHeroShowcase />
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- LIVE BENCHMARK RATES ----------------- */}
        <section id="rates" className="relative py-16 bg-[#0a0d14]/80 border-y border-amber-500/10">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex flex-col md:flex-row md:items-end justify-between mb-8 gap-4">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <span className="flex h-2 w-2 rounded-full bg-emerald-400 animate-ping" />
                  <span className="text-xs font-bold text-amber-400 tracking-wider uppercase">
                    مظنه زنده بازار {brandConfig.name.en}
                  </span>
                </div>
                <h2 className="text-2xl sm:text-3xl font-bold text-white">
                  نرخ لحظه‌ای بازارساز {brandConfig.name.fa}
                </h2>
              </div>
              <p className="text-xs text-slate-400 font-light">
                بروزرسانی مستقیم از هسته معاملات • بدون کارمزد خرید و فروش (کارمزد: ۰ تومان)
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {/* Card 1: MAUA/IRT (Melted Gold 18k) */}
              <div className="glass-card-interactive rounded-2xl p-6 relative overflow-hidden">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-3">
                    <div className="h-10 w-10 rounded-xl overflow-hidden border border-amber-500/40 shadow-sm shrink-0">
                      <Image
                        src={brandConfig.assets.iconNoOrbit}
                        alt="MAUA"
                        width={40}
                        height={40}
                        className="object-cover"
                      />
                    </div>
                    <div>
                      <h3 className="font-bold text-white text-base">طلای آبشده ۱۸ عیار</h3>
                      <span className="text-[11px] text-slate-400 font-mono">MAUA / IRT (گرم)</span>
                    </div>
                  </div>
                  <span className="inline-flex items-center gap-1 rounded-md bg-emerald-500/10 px-2.5 py-1 text-[11px] font-medium text-emerald-400 border border-emerald-500/20">
                    <TrendingUp className="h-3 w-3" />
                    بازارساز فعال
                  </span>
                </div>

                <div className="space-y-3 pt-2">
                  <div className="flex items-center justify-between text-sm py-2 border-b border-slate-800">
                    <span className="text-slate-400">قیمت خرید شما (Ask):</span>
                    <span className="font-bold text-white tracking-wide">
                      ۴,۳۵۵,۰۰۰ <span className="text-xs font-normal text-slate-400">تومان</span>
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-sm py-2 border-b border-slate-800">
                    <span className="text-slate-400">قیمت فروش شما (Bid):</span>
                    <span className="font-bold text-amber-400 tracking-wide">
                      ۴,۳۴۵,۰۰۰ <span className="text-xs font-normal text-slate-400">تومان</span>
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-slate-400 pt-1">
                    <span>اسپرد رقابتی بازارساز:</span>
                    <span className="text-emerald-400 font-semibold">۱۰,۰۰۰ تومان (۰.۲٪)</span>
                  </div>
                </div>

                <div className="mt-5 pt-3">
                  <a
                    href={brandConfig.telegram.botUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="w-full flex items-center justify-center gap-2 rounded-xl bg-amber-500/15 border border-amber-500/30 py-2.5 text-xs font-semibold text-amber-300 hover:bg-amber-500/25 transition-colors"
                  >
                    <span>معامله آبشده در ربات</span>
                    <ChevronLeft className="h-4 w-4" />
                  </a>
                </div>
              </div>

              {/* Card 2: SEKE_BAHAR (Coin) */}
              <div className="glass-card-interactive rounded-2xl p-6 relative overflow-hidden">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-3">
                    <div className="h-10 w-10 rounded-xl overflow-hidden border border-amber-500/40 shadow-sm shrink-0">
                      <Image
                        src={brandConfig.assets.iconNoOrbit}
                        alt="SEKE"
                        width={40}
                        height={40}
                        className="object-cover"
                      />
                    </div>
                    <div>
                      <h3 className="font-bold text-white text-base">تمام سکه بهار آزادی</h3>
                      <span className="text-[11px] text-slate-400 font-mono">SEKE_BAHAR / IRT</span>
                    </div>
                  </div>
                  <span className="inline-flex items-center gap-1 rounded-md bg-emerald-500/10 px-2.5 py-1 text-[11px] font-medium text-emerald-400 border border-emerald-500/20">
                    <TrendingUp className="h-3 w-3" />
                    بازارساز فعال
                  </span>
                </div>

                <div className="space-y-3 pt-2">
                  <div className="flex items-center justify-between text-sm py-2 border-b border-slate-800">
                    <span className="text-slate-400">قیمت خرید شما:</span>
                    <span className="font-bold text-white tracking-wide">
                      ۴۸,۲۰۰,۰۰۰ <span className="text-xs font-normal text-slate-400">تومان</span>
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-sm py-2 border-b border-slate-800">
                    <span className="text-slate-400">قیمت فروش شما:</span>
                    <span className="font-bold text-amber-400 tracking-wide">
                      ۴۷,۹۵۰,۰۰۰ <span className="text-xs font-normal text-slate-400">تومان</span>
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-slate-400 pt-1">
                    <span>اسپرد رقابتی سکه:</span>
                    <span className="text-slate-300 font-semibold">۲۵۰,۰۰۰ تومان</span>
                  </div>
                </div>

                <div className="mt-5 pt-3">
                  <a
                    href={brandConfig.telegram.botUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="w-full flex items-center justify-center gap-2 rounded-xl bg-amber-500/15 border border-amber-500/30 py-2.5 text-xs font-semibold text-amber-300 hover:bg-amber-500/25 transition-colors"
                  >
                    <span>معامله سکه در ربات</span>
                    <ChevronLeft className="h-4 w-4" />
                  </a>
                </div>
              </div>

              {/* Card 3: XAU/USD (Spot Gold) */}
              <div className="glass-card-interactive rounded-2xl p-6 relative overflow-hidden">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-3">
                    <div className="h-10 w-10 rounded-xl overflow-hidden border border-amber-500/40 shadow-sm shrink-0">
                      <Image
                        src={brandConfig.assets.iconNoOrbit}
                        alt="XAU"
                        width={40}
                        height={40}
                        className="object-cover"
                      />
                    </div>
                    <div>
                      <h3 className="font-bold text-white text-base">انس جهانی طلا</h3>
                      <span className="text-[11px] text-slate-400 font-mono">XAU / USD</span>
                    </div>
                  </div>
                  <span className="inline-flex items-center gap-1 rounded-md bg-amber-500/10 px-2.5 py-1 text-[11px] font-medium text-amber-400 border border-amber-500/20">
                    شاخص مرجع
                  </span>
                </div>

                <div className="space-y-3 pt-2">
                  <div className="flex items-center justify-between text-sm py-2 border-b border-slate-800">
                    <span className="text-slate-400">نرخ جهانی لحظه‌ای:</span>
                    <span className="font-bold text-white tracking-wide" dir="ltr">
                      $2,745.80
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-sm py-2 border-b border-slate-800">
                    <span className="text-slate-400">معادل وزنی هر انس:</span>
                    <span className="font-medium text-slate-200">
                      ۳۱.۱۰۳۵ گرم طلای ۲۴ عیار
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-slate-400 pt-1">
                    <span>منبع معتبر جهانی:</span>
                    <span className="text-slate-300">Swissquote Data Feed</span>
                  </div>
                </div>

                <div className="mt-5 pt-3">
                  <div className="w-full text-center py-2.5 text-xs text-slate-400 bg-slate-900/60 rounded-xl border border-slate-800">
                    پایه قیمت‌گذاری آبشده در بازار داخلی
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- DIGITAL GOLD ORBIT ANIMATION SECTION ----------------- */}
        <section id="orbit-engine" className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-center">
            {/* Right Column (RTL): Digital & Physical Architecture Narrative */}
            <div className="lg:col-span-7 space-y-6 text-center lg:text-right">
              <div className="inline-flex items-center gap-2 rounded-full border border-amber-500/35 bg-amber-500/10 px-4 py-1.5 text-xs font-semibold text-amber-300">
                <span className="h-2 w-2 rounded-full bg-amber-400 animate-pulse" />
                <span>DIGITAL &amp; PHYSICAL GOLD CORE</span>
              </div>

              <h2 className="text-3xl sm:text-4xl font-black text-white leading-tight">
                پیوند طلای فیزیکی ۱۸ عیار با{" "}
                <span className="gold-gradient-text">
                  مدار دیجیتال دفترکل زَر اوربیت
                </span>
              </h2>

              <p className="text-sm sm:text-base text-slate-300 font-light leading-relaxed max-w-2xl mx-auto lg:mx-0">
                در {brandConfig.name.fa} هر گرم طلای فیزیکی اصیل به صورت یکپارچه و دقیق بر بستر دفترکل باینری دوطرفه ثبت و پایش می‌شود. تراکنش‌های لحظه‌ای بدون صف، تسویه شفاف و قابلیت تبدیل مستقیم دارایی دیجیتال به شمش فیزیکی معتبر، هسته اصلی مدار نقدینگی ما را می‌سازد.
              </p>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-2">
                <div className="glass-card rounded-2xl p-4 border border-amber-500/20 text-right">
                  <div className="text-xs text-slate-400 mb-1">مدل اجرا</div>
                  <div className="text-sm font-bold text-white">بازارساز مستقیم</div>
                  <div className="text-[11px] text-amber-400 mt-0.5">بدون نیاز به صف انتظار</div>
                </div>
                <div className="glass-card rounded-2xl p-4 border border-amber-500/20 text-right">
                  <div className="text-xs text-slate-400 mb-1">ثبت و حسابداری</div>
                  <div className="text-sm font-bold text-white">دفترکل دوطرفه</div>
                  <div className="text-[11px] text-emerald-400 mt-0.5">تراز ریاضیاتی تغییرناپذیر</div>
                </div>
                <div className="glass-card rounded-2xl p-4 border border-amber-500/20 text-right">
                  <div className="text-xs text-slate-400 mb-1">پشتوانه دارایی</div>
                  <div className="text-sm font-bold text-white">عیار ۷۵۰ استاندارد</div>
                  <div className="text-[11px] text-amber-300 mt-0.5">تحویل فیزیکی معتبر</div>
                </div>
              </div>
            </div>

            {/* Left Column (RTL): Animated 3D Spinning Z + Orbiting 0/1 Particles */}
            <div className="lg:col-span-5 flex justify-center">
              <ZarorbitOrbitalMotion className="w-full max-w-[430px]" />
            </div>
          </div>
        </section>

        {/* ----------------- INTERACTIVE 3D BULLION SIMULATOR & CALCULATOR ----------------- */}
        <section id="calculator" className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="glass-card rounded-3xl p-8 sm:p-12 relative overflow-hidden border border-amber-500/30 shadow-goldGlow">
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-center">
              {/* Left Column: Explanation & 3D Bullion Visualizer */}
              <div className="lg:col-span-6 space-y-6">
                <div>
                  <span className="text-xs font-bold text-amber-400 uppercase tracking-wider">
                    محاسبه‌گر ارزش لحظه‌ای
                  </span>
                  <h2 className="text-2xl sm:text-3xl font-black text-white mt-1">
                    ماشین‌حساب هوشمند و پیش‌نمایش شمش طلای آبشده
                  </h2>
                  <p className="text-sm text-slate-300 font-light leading-relaxed mt-2">
                    وزن مورد نظر خود را انتخاب یا وارد کنید تا ارزش دقیق آن بر اساس مظنه لحظه‌ای بازار محاسبه شود.
                  </p>
                </div>

                {/* 3D Bullion Bar Interactive Simulation Preview */}
                <div className="rounded-2xl bg-gradient-to-br from-[#0c0f18] to-[#07090e] border border-amber-500/30 p-6 relative overflow-hidden text-center">
                  <div className="text-[11px] text-amber-400/90 font-medium mb-3">
                    پیش‌نمایش شمش طلای فیزیکی استاندارد زَر اوربیت
                  </div>

                  {/* Bullion Bar Simulation */}
                  <div className="mx-auto w-56 sm:w-64 h-28 rounded-xl gold-bullion-surface flex flex-col items-center justify-between p-3.5 text-slate-950 font-bold select-none shadow-2xl border border-yellow-200/50 transform hover:scale-105 transition-all duration-300 cursor-default">
                    <div className="flex items-center justify-between w-full text-[10px] uppercase tracking-wider border-b border-black/20 pb-1">
                      <span>{brandConfig.name.en}</span>
                      <span className="font-mono">999.9 PURE</span>
                    </div>
                    <div className="my-auto text-center">
                      <div className="text-xl sm:text-2xl font-black tracking-tight">
                        {numericAmount.toLocaleString("fa-IR")} {unit === "gram" ? "گرم" : "مثقال"}
                      </div>
                      <div className="text-[10px] text-slate-900 font-medium -mt-1">
                        طلای ۱۸ عیار استاندارد (۷۵۰)
                      </div>
                    </div>
                    <div className="flex items-center justify-between w-full text-[9px] text-slate-900 border-t border-black/20 pt-1">
                      <span>انگ رسمی معتبر</span>
                      <span>ASSAY GUARANTEED</span>
                    </div>
                  </div>

                  {/* Savings Comparison Note */}
                  <div className="mt-4 inline-flex items-center gap-2 rounded-lg bg-emerald-500/10 border border-emerald-500/25 px-3 py-1.5 text-xs text-emerald-300">
                    <Sparkles className="h-4 w-4 text-emerald-400 shrink-0" />
                    <span>
                      صرفه‌جویی شما در {brandConfig.name.fa} نسبت به طلافروشی‌های سنتی:{" "}
                      <strong>{traditionalFeeEst.toLocaleString("fa-IR")} تومان</strong> (معاف از مالیات و اجرت)
                    </span>
                  </div>
                </div>

                {/* Quick Presets */}
                <div>
                  <span className="text-xs text-slate-400 block mb-2">وزن‌های پرکاربرد معامله‌گران:</span>
                  <div className="flex flex-wrap gap-2">
                    {[1, 5, 10, 20, 50, 100].map((val) => (
                      <button
                        key={val}
                        onClick={() => {
                          setUnit("gram");
                          setAmountInput(String(val));
                        }}
                        className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
                          numericAmount === val && unit === "gram"
                            ? "bg-amber-500 text-slate-950 font-bold shadow-md"
                            : "bg-slate-800/80 text-slate-300 hover:bg-slate-700"
                        }`}
                      >
                        {val} گرم
                      </button>
                    ))}
                    {[1, 2, 5].map((val) => (
                      <button
                        key={`m-${val}`}
                        onClick={() => {
                          setUnit("mesghal");
                          setAmountInput(String(val));
                        }}
                        className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
                          numericAmount === val && unit === "mesghal"
                            ? "bg-amber-500 text-slate-950 font-bold shadow-md"
                            : "bg-slate-800/80 text-slate-300 hover:bg-slate-700"
                        }`}
                      >
                        {val} مثقال
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* Right Column: Input & Live Calculation Results */}
              <div className="lg:col-span-6 bg-[#0c0f17] rounded-2xl p-6 sm:p-8 border border-amber-500/30 space-y-6">
                {/* Unit Switcher */}
                <div className="flex rounded-xl bg-slate-900 p-1 border border-slate-800">
                  <button
                    onClick={() => setUnit("gram")}
                    className={`flex-1 py-2 text-xs font-bold rounded-lg transition-all ${
                      unit === "gram"
                        ? "bg-amber-500 text-slate-950 shadow-sm"
                        : "text-slate-400 hover:text-white"
                    }`}
                  >
                    گرم (عیار ۱۸ / ۷۵۰)
                  </button>
                  <button
                    onClick={() => setUnit("mesghal")}
                    className={`flex-1 py-2 text-xs font-bold rounded-lg transition-all ${
                      unit === "mesghal"
                        ? "bg-amber-500 text-slate-950 shadow-sm"
                        : "text-slate-400 hover:text-white"
                    }`}
                  >
                    مثقال طلا (۴.۳۳ گرم)
                  </button>
                </div>

                {/* Amount Input */}
                <div>
                  <label
                    htmlFor="gold-calc-input"
                    className="block text-xs text-slate-400 mb-2"
                  >
                    وزن طلا ({unit === "gram" ? "گرم" : "مثقال"}):
                  </label>
                  <div className="relative">
                    <input
                      id="gold-calc-input"
                      type="number"
                      step="0.01"
                      min="0.01"
                      value={amountInput}
                      onChange={(e) => setAmountInput(e.target.value)}
                      className="w-full rounded-xl bg-slate-900/90 border border-slate-700/80 pl-24 pr-4 py-3.5 text-lg font-bold text-white focus:border-amber-400 focus:outline-none focus:ring-1 focus:ring-amber-400 text-left"
                      dir="ltr"
                    />
                    <span className="absolute left-4 top-1/2 -translate-y-1/2 text-xs text-slate-400 font-medium bg-slate-800/80 px-2.5 py-1 rounded">
                      {unit === "gram" ? "Gram" : "Mesghal"}
                    </span>
                  </div>
                </div>

                {/* Results breakdown */}
                <div className="rounded-xl bg-amber-500/5 border border-amber-500/20 p-5 space-y-3">
                  <div className="flex items-center justify-between text-xs text-slate-300">
                    <span>مظنه پایه محاسبه ({unit === "gram" ? "هر گرم ۱۸" : "هر مثقال"}):</span>
                    <span className="font-semibold text-white">
                      {(unit === "gram" ? gramRate : mesghalRate).toLocaleString("fa-IR")} تومان
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-slate-300">
                    <span>کارمزد پلتفرم زَر اوربیت:</span>
                    <span className="font-semibold text-emerald-400">۰ تومان (کاملاً رایگان)</span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-slate-300">
                    <span>مالیات ارزش افزوده:</span>
                    <span className="font-semibold text-emerald-400">۰ تومان (معاف)</span>
                  </div>
                  <div className="pt-3 border-t border-amber-500/20 flex items-center justify-between">
                    <span className="text-sm font-bold text-white">ارزش تقریبی معامله:</span>
                    <span className="text-2xl sm:text-3xl font-black text-amber-400">
                      {Math.round(totalCalculated).toLocaleString("fa-IR")}{" "}
                      <span className="text-xs font-normal text-slate-300">تومان</span>
                    </span>
                  </div>
                </div>

                {/* CTA Buttons */}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <a
                    href={brandConfig.telegram.botUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-400 to-amber-600 py-3.5 text-xs font-bold text-slate-950 shadow-goldGlowSubtle hover:brightness-110 transition-all"
                  >
                    <Send className="h-4 w-4" />
                    <span>ثبت سفارش در ربات</span>
                  </a>
                  <Link
                    href="/login"
                    className="flex items-center justify-center gap-2 rounded-xl border border-slate-700 bg-slate-800/80 py-3.5 text-xs font-semibold text-slate-200 hover:bg-slate-700 transition-all"
                  >
                    <Wallet className="h-4 w-4" />
                    <span>ورود به کیف پول</span>
                  </Link>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- COMPARISON TABLE: ZARORBIT VS TRADITIONAL BAZAAR ----------------- */}
        <section className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="text-center max-w-3xl mx-auto mb-14 space-y-3">
            <span className="text-xs font-semibold text-amber-400 uppercase tracking-wider">
              شفافیت در معامله
            </span>
            <h2 className="text-3xl sm:text-4xl font-black text-white">
              چرا معامله در {brandConfig.name.fa} به صرفه‌تر است؟
            </h2>
            <p className="text-sm text-slate-400 font-light">
              مقایسه بی‌طرفانه شرایط خرید و فروش طلای آبشده در پلتفرم زَر اوربیت در برابر بازار سنتی طلافروشان.
            </p>
          </div>

          <div className="glass-card rounded-2xl overflow-hidden border border-amber-500/20 shadow-xl">
            <div className="overflow-x-auto">
              <table className="w-full text-right text-sm">
                <thead>
                  <tr className="bg-[#0e121c] border-b border-amber-500/20 text-slate-300">
                    <th className="py-4 px-6 font-bold">شاخص معامله</th>
                    <th className="py-4 px-6 font-bold text-amber-400 bg-amber-500/10">
                      پلتفرم آنلاین {brandConfig.name.fa}
                    </th>
                    <th className="py-4 px-6 font-bold text-slate-400">بازار سنتی و طلافروشی‌ها</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60 text-slate-300">
                  <tr>
                    <td className="py-4 px-6 font-medium text-white">کارمزد خرید و فروش</td>
                    <td className="py-4 px-6 font-bold text-emerald-400 bg-amber-500/5">
                      ۰.۰۰٪ (بدون حتی ۱ ریال کارمزد)
                    </td>
                    <td className="py-4 px-6 text-slate-400">۱.۵٪ تا ۳.۵٪ کارمزد معامله</td>
                  </tr>
                  <tr>
                    <td className="py-4 px-6 font-medium text-white">اجرت ساخت مصنوعات</td>
                    <td className="py-4 px-6 font-bold text-emerald-400 bg-amber-500/5">
                      ۰ تومان (شمش و آبشده بدون اجرت)
                    </td>
                    <td className="py-4 px-6 text-slate-400">۱۵٪ تا ۳۵٪ اجرت ساخت</td>
                  </tr>
                  <tr>
                    <td className="py-4 px-6 font-medium text-white">مالیات بر ارزش افزوده</td>
                    <td className="py-4 px-6 font-bold text-emerald-400 bg-amber-500/5">
                      معاف از مالیات
                    </td>
                    <td className="py-4 px-6 text-slate-400">۹٪ مالیات روی اجرت و سود</td>
                  </tr>
                  <tr>
                    <td className="py-4 px-6 font-medium text-white">سرعت و مدل تطبیق سفارش</td>
                    <td className="py-4 px-6 font-bold text-amber-400 bg-amber-500/5">
                      آنی با میز بازارساز اختصاصی (بدون صف)
                    </td>
                    <td className="py-4 px-6 text-slate-400">منوط به حضور فیزیکی یا توافق تلفنی</td>
                  </tr>
                  <tr>
                    <td className="py-4 px-6 font-medium text-white">تضمین عیار و اصالت</td>
                    <td className="py-4 px-6 font-bold text-emerald-400 bg-amber-500/5">
                      انگ معتبر ری‌گیری رسمی + تحویل شمش
                    </td>
                    <td className="py-4 px-6 text-slate-400">متغیر بسته به اعتبار طلافروش</td>
                  </tr>
                  <tr>
                    <td className="py-4 px-6 font-medium text-white">تسویه مالی و ثبت سند</td>
                    <td className="py-4 px-6 font-bold text-amber-400 bg-amber-500/5">
                      تسویه آنی بر بستر دفترکل امن دوطرفه
                    </td>
                    <td className="py-4 px-6 text-slate-400">کارت‌به‌کارت با سقف محدود بانکی</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </section>

        {/* ----------------- FEATURES SECTION ----------------- */}
        <section id="features" className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="text-center max-w-3xl mx-auto mb-16 space-y-3">
            <span className="text-xs font-semibold text-amber-400 uppercase tracking-wider">
              مزایای ساختاری
            </span>
            <h2 className="text-3xl sm:text-4xl font-black text-white">
              چهار ستون اعتماد و کارایی در {brandConfig.name.fa}
            </h2>
            <p className="text-sm text-slate-400 font-light">
              ترکیب امنیت طلافروشی‌های اصیل با تکنولوژی پیشرفته فین‌تک برای خلق امن‌ترین بستر معاملات.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {/* Feature 1 */}
            <div className="glass-card-interactive rounded-2xl p-7 space-y-4">
              <div className="h-12 w-12 rounded-xl bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400">
                <Zap className="h-6 w-6" />
              </div>
              <h3 className="text-lg font-bold text-white">مدل بازارساز اختصاصی</h3>
              <p className="text-xs leading-relaxed text-slate-300 font-light">
                بدون صف و معطلی برای تطبیق سفارش با سایر کاربران؛ طرف حساب معامله همواره میز بازارساز متعهد {brandConfig.name.fa} است و اجرای سفارش در کسر ثانیه صورت می‌گیرد.
              </p>
            </div>

            {/* Feature 2 */}
            <div className="glass-card-interactive rounded-2xl p-7 space-y-4">
              <div className="h-12 w-12 rounded-xl bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400">
                <Sparkles className="h-6 w-6" />
              </div>
              <h3 className="text-lg font-bold text-white">معاملات با کارمزد صفر</h3>
              <p className="text-xs leading-relaxed text-slate-300 font-light">
                هیچ درصد یا مبلغ ثابتی به عنوان کارمزد کسر نمی‌شود. درآمد پلتفرم صرفاً از اسپرد شفاف قیمت خرید و فروش حاصل می‌شود.
              </p>
            </div>

            {/* Feature 3 */}
            <div className="glass-card-interactive rounded-2xl p-7 space-y-4">
              <div className="h-12 w-12 rounded-xl bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400">
                <Repeat className="h-6 w-6" />
              </div>
              <h3 className="text-lg font-bold text-white">اعتبار معاملاتی چنددارایی</h3>
              <p className="text-xs leading-relaxed text-slate-300 font-light">
                امکان بهره‌مندی از اعتبار شناور بین‌دارایی (Cross-Asset Credit) که به شما اجازه می‌دهد متناسب با تضمینات، به حجم بالاتری از معاملات دسترسی پیدا کنید.
              </p>
            </div>

            {/* Feature 4 */}
            <div className="glass-card-interactive rounded-2xl p-7 space-y-4">
              <div className="h-12 w-12 rounded-xl bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400">
                <ShieldCheck className="h-6 w-6" />
              </div>
              <h3 className="text-lg font-bold text-white">دفترکل دوطرفه و امن</h3>
              <p className="text-xs leading-relaxed text-slate-300 font-light">
                معماری دفترکل مالی (Ledger-backed) هر تراکنش را به صورت دقیق و تراز ثبت می‌کند؛ داده‌ها ضد جعل، تغییرناپذیر و قابل ردیابی لحظه‌ای هستند.
              </p>
            </div>
          </div>
        </section>

        {/* ----------------- HOW IT WORKS (GUIDE) ----------------- */}
        <section id="guide" className="py-20 bg-[#07090e]/80 border-t border-amber-500/10">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="text-center max-w-2xl mx-auto mb-16 space-y-3">
              <span className="text-xs font-semibold text-amber-400 uppercase tracking-wider">
                مراحل ساده شروع
              </span>
              <h2 className="text-3xl font-extrabold text-white">
                چگونه در {brandConfig.name.fa} معامله کنیم؟
              </h2>
              <p className="text-xs text-slate-400 font-light">
                در ۳ گام بدون نیاز به کاغذبازی و مراجعات حضوری فعالیت خود را آغاز کنید.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-8 relative">
              {/* Step 1 */}
              <div className="glass-card rounded-2xl p-8 relative space-y-4 text-center sm:text-right">
                <div className="flex items-center justify-between">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-500/20 text-amber-400 font-black text-base border border-amber-500/40">
                    ۱
                  </div>
                  <Send className="h-5 w-5 text-slate-500" />
                </div>
                <h3 className="text-lg font-bold text-white">اتصال به ربات تلگرام یا وب</h3>
                <p className="text-xs leading-relaxed text-slate-300 font-light">
                  وارد ربات رسمی تلگرام @{brandConfig.telegram.botUsername} شوید و با یک کلیک شماره همراه خود را تأیید کنید تا حساب کاربری شما فعال شود.
                </p>
              </div>

              {/* Step 2 */}
              <div className="glass-card rounded-2xl p-8 relative space-y-4 text-center sm:text-right">
                <div className="flex items-center justify-between">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-500/20 text-amber-400 font-black text-base border border-amber-500/40">
                    ۲
                  </div>
                  <Lock className="h-5 w-5 text-slate-500" />
                </div>
                <h3 className="text-lg font-bold text-white">شارژ کیف پول یا تحویل طلای آبشده</h3>
                <p className="text-xs leading-relaxed text-slate-300 font-light">
                  کیف پول ریالی خود را از طریق درگاه یا پایا شارژ نمایید یا طلای آبشده فیزیکی را جهت سپرده‌گذاری در خزانه تحویل دهید.
                </p>
              </div>

              {/* Step 3 */}
              <div className="glass-card rounded-2xl p-8 relative space-y-4 text-center sm:text-right">
                <div className="flex items-center justify-between">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-500/20 text-amber-400 font-black text-base border border-amber-500/40">
                    ۳
                  </div>
                  <Wallet className="h-5 w-5 text-slate-500" />
                </div>
                <h3 className="text-lg font-bold text-white">معامله آنلاین و تحویل شمش فیزیکی</h3>
                <p className="text-xs leading-relaxed text-slate-300 font-light">
                  در هر ساعت از شبانه‌روز با بهترین مظنه سفارش ثبت کنید، سود کسب کنید یا هر زمان که مایل بودید شمش طلای خود را فیزیکی تحویل بگیرید.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- FINAL CTA SECTION ----------------- */}
        <section className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="relative rounded-3xl p-10 sm:p-16 overflow-hidden border border-amber-500/30 bg-gradient-to-br from-[#151a26] via-[#10141e] to-[#090b10] text-center space-y-6 shadow-goldGlow">
            <div className="max-w-2xl mx-auto space-y-4">
              <div className="flex justify-center mb-3">
                <BrandEmblem sizePx={88} useLargeLogo />
              </div>
              <h2 className="text-3xl sm:text-4xl font-black text-white">
                آماده معامله هوشمند طلای آبشده در {brandConfig.name.fa} هستید؟
              </h2>
              <p className="text-sm text-slate-300 font-light leading-relaxed">
                هم‌اکنون به شبکه معامله‌گران {brandConfig.name.fa} بپیوندید و امنیت، سرعت و شفافیت را در معاملات روزانه خود تجربه کنید.
              </p>
            </div>

            <div className="flex flex-col sm:flex-row items-center justify-center gap-4 pt-4">
              <a
                href={brandConfig.telegram.botUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="w-full sm:w-auto inline-flex items-center justify-center gap-3 rounded-xl bg-gradient-to-r from-amber-400 via-amber-500 to-amber-600 px-8 py-4 text-sm font-bold text-slate-950 shadow-goldGlow hover:brightness-110 transition-all"
              >
                <Send className="h-4 w-4" />
                <span>ورود به ربات معامله‌گری @{brandConfig.telegram.botUsername}</span>
              </a>
              <Link
                href="/login"
                className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-xl border border-slate-700 bg-slate-800/80 px-6 py-4 text-sm font-semibold text-slate-200 hover:bg-slate-700 transition-all"
              >
                <span>ورود به پرتال وب</span>
                <ChevronLeft className="h-4 w-4" />
              </Link>
            </div>
          </div>
        </section>
      </main>

      <Footer />
    </>
  );
}
