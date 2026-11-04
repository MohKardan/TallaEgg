"use client";

import Navbar from "@/components/Navbar";
import Footer from "@/components/Footer";
import Link from "next/link";
import { useState } from "react";
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
} from "lucide-react";

export default function HomePage() {
  // Calculator state
  const [unit, setUnit] = useState<"gram" | "mesghal">("gram");
  const [amountInput, setAmountInput] = useState<string>("5");
  const numericAmount = Math.max(0, parseFloat(amountInput) || 0);

  // Representative live benchmark rates (Toman)
  const gramRate = 4350000; // 4,350,000 Toman per gram of 18k melted gold
  const mesghalRate = gramRate * 4.3318; // 1 mesghal = 4.3318 grams of 17k/18k

  const totalCalculated =
    unit === "gram" ? numericAmount * gramRate : numericAmount * mesghalRate;

  return (
    <>
      <Navbar />

      <main className="relative flex-1">
        {/* Ambient atmospheric glows */}
        <div className="ambient-glow top-0 right-1/4 -translate-y-1/2 opacity-70" />
        <div className="ambient-glow top-[600px] left-[-150px] opacity-40" />
        <div className="ambient-glow bottom-[300px] right-[-100px] opacity-50" />

        {/* ----------------- HERO SECTION ----------------- */}
        <section className="relative pt-12 pb-20 md:pt-20 md:pb-32 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="flex flex-col items-center text-center max-w-4xl mx-auto space-y-8">
            {/* Pill Badge */}
            <div className="inline-flex items-center gap-2 rounded-full border border-amber-500/30 bg-amber-500/10 px-4 py-1.5 text-xs font-medium text-amber-300 backdrop-blur-md shadow-goldGlowSubtle">
              <Sparkles className="h-3.5 w-3.5 text-amber-400" />
              <span>پلتفرم تخصصی بازارساز طلای آبشده • کارمزد معامله ۰٪</span>
            </div>

            {/* Main Headline */}
            <h1 className="text-4xl sm:text-5xl md:text-6xl lg:text-7xl font-extrabold tracking-tight leading-[1.25] text-white">
              معامله آنی و هوشمندانه{" "}
              <span className="gold-gradient-text block mt-2">
                طلای آبشده ۱۸ عیار
              </span>
            </h1>

            {/* Subhead with clear, honest value proposition */}
            <p className="text-base sm:text-lg md:text-xl text-slate-300 max-w-2xl font-light leading-relaxed">
              خرید و فروش مستقیم با میز بازارساز اختصاصی طلااگ؛ بدون صف انتظار، بدون کارمزد مخفی، همراه با تحویل شمش معتبر و تسویه ریالی لحظه‌ای بر بستر امن دفترکل.
            </p>

            {/* Action Buttons */}
            <div className="flex flex-col sm:flex-row items-center gap-4 w-full sm:w-auto pt-4">
              <Link
                href="/login"
                className="w-full sm:w-auto inline-flex items-center justify-center gap-3 rounded-xl bg-gradient-to-r from-amber-400 via-amber-500 to-amber-600 px-8 py-4 text-base font-bold text-slate-950 shadow-goldGlow hover:brightness-110 active:scale-[0.98] transition-all"
              >
                <Wallet className="h-5 w-5" />
                <span>ورود و مشاهده موجودی</span>
              </Link>
              <a
                href="https://t.me/TallaEgg_Bot"
                target="_blank"
                rel="noopener noreferrer"
                className="w-full sm:w-auto inline-flex items-center justify-center gap-3 rounded-xl border border-amber-500/30 bg-[#121620]/90 px-8 py-4 text-base font-semibold text-amber-300 hover:bg-amber-500/10 active:scale-[0.98] transition-all backdrop-blur-md"
              >
                <Send className="h-5 w-5" />
                <span>شروع معامله در ربات تلگرام</span>
              </a>
            </div>

            {/* Quick Guarantees */}
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-6 pt-6 text-xs text-slate-400 border-t border-slate-800/80 w-full max-w-xl">
              <div className="flex items-center justify-center gap-2">
                <CheckCircle2 className="h-4 w-4 text-amber-400" />
                <span>تسویه آنی دفترکل</span>
              </div>
              <div className="flex items-center justify-center gap-2">
                <CheckCircle2 className="h-4 w-4 text-amber-400" />
                <span>بدون مالیات و اجرت</span>
              </div>
              <div className="col-span-2 sm:col-span-1 flex items-center justify-center gap-2">
                <CheckCircle2 className="h-4 w-4 text-amber-400" />
                <span>طلای با انگ و ری‌گیری معتبر</span>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- LIVE RATES SECTION ----------------- */}
        <section id="rates" className="relative py-16 bg-[#0a0d14]/70 border-y border-amber-500/10">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex flex-col md:flex-row md:items-end justify-between mb-8 gap-4">
              <div>
                <span className="text-xs font-semibold text-amber-400 tracking-wider uppercase">
                  قیمت‌های زنده بازار
                </span>
                <h2 className="text-2xl sm:text-3xl font-bold text-white mt-1">
                  مظنه لحظه‌ای طلااگ
                </h2>
              </div>
              <p className="text-xs text-slate-400 font-light">
                بروزرسانی پیوسته از منابع معتبر بازار طلا و ارز • کارمزد خرید و فروش: ۰ تومان
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {/* Card 1: MAUA/IRT (Melted Gold) */}
              <div className="glass-card-interactive rounded-2xl p-6 relative overflow-hidden">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-2.5">
                    <div className="h-9 w-9 rounded-lg bg-amber-500/15 border border-amber-500/30 flex items-center justify-center text-amber-400 font-bold text-sm">
                      آبشده
                    </div>
                    <div>
                      <h3 className="font-bold text-white text-base">طلای آبشده ۱۸ عیار</h3>
                      <span className="text-[11px] text-slate-400">MAUA / IRT (گرم)</span>
                    </div>
                  </div>
                  <span className="inline-flex items-center gap-1 rounded-md bg-emerald-500/10 px-2 py-1 text-[11px] font-medium text-emerald-400 border border-emerald-500/20">
                    <TrendingUp className="h-3 w-3" />
                    فعال
                  </span>
                </div>

                <div className="space-y-3 pt-2">
                  <div className="flex items-center justify-between text-sm py-2 border-b border-slate-800">
                    <span className="text-slate-400">قیمت خرید شما:</span>
                    <span className="font-bold text-white tracking-wide">
                      ۴,۳۵۵,۰۰۰ <span className="text-xs font-normal text-slate-400">تومان</span>
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-sm py-2 border-b border-slate-800">
                    <span className="text-slate-400">قیمت فروش شما:</span>
                    <span className="font-bold text-amber-400 tracking-wide">
                      ۴,۳۴۵,۰۰۰ <span className="text-xs font-normal text-slate-400">تومان</span>
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-slate-400 pt-1">
                    <span>اسپرد رقابتی بازارساز:</span>
                    <span className="text-slate-300">۱۰,۰۰۰ تومان (۰.۲٪)</span>
                  </div>
                </div>

                <div className="mt-5 pt-3">
                  <a
                    href="https://t.me/TallaEgg_Bot"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="w-full flex items-center justify-center gap-2 rounded-lg bg-amber-500/10 border border-amber-500/30 py-2.5 text-xs font-semibold text-amber-300 hover:bg-amber-500/20 transition-colors"
                  >
                    <span>معامله آبشده در ربات</span>
                    <ChevronLeft className="h-4 w-4" />
                  </a>
                </div>
              </div>

              {/* Card 2: SEKE_BAHAR (Coin) */}
              <div className="glass-card-interactive rounded-2xl p-6 relative overflow-hidden">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-2.5">
                    <div className="h-9 w-9 rounded-lg bg-amber-500/15 border border-amber-500/30 flex items-center justify-center text-amber-400 font-bold text-sm">
                      سکه
                    </div>
                    <div>
                      <h3 className="font-bold text-white text-base">تمام سکه بهار آزادی</h3>
                      <span className="text-[11px] text-slate-400">SEKE_BAHAR / IRT</span>
                    </div>
                  </div>
                  <span className="inline-flex items-center gap-1 rounded-md bg-emerald-500/10 px-2 py-1 text-[11px] font-medium text-emerald-400 border border-emerald-500/20">
                    <TrendingUp className="h-3 w-3" />
                    فعال
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
                    <span>اسپرد رقابتی:</span>
                    <span className="text-slate-300">۲۵۰,۰۰۰ تومان</span>
                  </div>
                </div>

                <div className="mt-5 pt-3">
                  <a
                    href="https://t.me/TallaEgg_Bot"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="w-full flex items-center justify-center gap-2 rounded-lg bg-amber-500/10 border border-amber-500/30 py-2.5 text-xs font-semibold text-amber-300 hover:bg-amber-500/20 transition-colors"
                  >
                    <span>معامله سکه در ربات</span>
                    <ChevronLeft className="h-4 w-4" />
                  </a>
                </div>
              </div>

              {/* Card 3: XAU/USD (Spot Gold) */}
              <div className="glass-card-interactive rounded-2xl p-6 relative overflow-hidden">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-2.5">
                    <div className="h-9 w-9 rounded-lg bg-amber-500/15 border border-amber-500/30 flex items-center justify-center text-amber-400 font-bold text-sm">
                      انس
                    </div>
                    <div>
                      <h3 className="font-bold text-white text-base">انس جهانی طلا</h3>
                      <span className="text-[11px] text-slate-400">XAU / USD</span>
                    </div>
                  </div>
                  <span className="inline-flex items-center gap-1 rounded-md bg-amber-500/10 px-2 py-1 text-[11px] font-medium text-amber-400 border border-amber-500/20">
                    شاخص
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
                    <span className="text-slate-400">واحد مرجع:</span>
                    <span className="font-medium text-slate-200">
                      ۳۱.۱۰۳۵ گرم طلای ۲۴ عیار
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-slate-400 pt-1">
                    <span>منبع معتبر:</span>
                    <span className="text-slate-300">سوئیس‌کوت / زنده</span>
                  </div>
                </div>

                <div className="mt-5 pt-3">
                  <div className="w-full text-center py-2.5 text-xs text-slate-400 bg-slate-900/50 rounded-lg border border-slate-800">
                    مبنای قیمت‌گذاری آبشده
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- INTERACTIVE CALCULATOR SECTION ----------------- */}
        <section id="calculator" className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="glass-card rounded-3xl p-8 sm:p-12 relative overflow-hidden border border-amber-500/20 shadow-goldGlowSubtle">
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-center">
              <div className="lg:col-span-6 space-y-4">
                <span className="text-xs font-semibold text-amber-400 uppercase tracking-wider">
                  محاسبه‌گر ارزش
                </span>
                <h2 className="text-2xl sm:text-3xl font-extrabold text-white">
                  ماشین‌حساب هوشمند ارزش طلای آبشده
                </h2>
                <p className="text-sm text-slate-300 font-light leading-relaxed">
                  وزن مورد نظر خود را بر حسب گرم یا مثقال وارد کنید تا ارزش دقیق ریالی آن بر اساس نرخ لحظه‌ای محاسبه شود. در طلااگ، هیچ کارمزدی اضافه بر مظنه پرداخت نمی‌کنید.
                </p>

                {/* Preset quick buttons */}
                <div className="pt-2">
                  <span className="text-xs text-slate-400 block mb-2">انتخاب‌های سریع:</span>
                  <div className="flex flex-wrap gap-2">
                    {[1, 5, 10, 20, 50].map((val) => (
                      <button
                        key={val}
                        onClick={() => setAmountInput(String(val))}
                        className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${
                          numericAmount === val
                            ? "bg-amber-500 text-slate-950 font-bold"
                            : "bg-slate-800/80 text-slate-300 hover:bg-slate-700"
                        }`}
                      >
                        {val} {unit === "gram" ? "گرم" : "مثقال"}
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* Calculator Box */}
              <div className="lg:col-span-6 bg-[#0c0f17] rounded-2xl p-6 sm:p-8 border border-amber-500/25 space-y-6">
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
                    مقدار وزن ({unit === "gram" ? "گرم" : "مثقال"}):
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
                    <span className="absolute left-4 top-1/2 -translate-y-1/2 text-xs text-slate-400 font-medium bg-slate-800/80 px-2 py-1 rounded">
                      {unit === "gram" ? "Gram" : "Mesghal"}
                    </span>
                  </div>
                </div>

                {/* Results breakdown */}
                <div className="rounded-xl bg-amber-500/5 border border-amber-500/20 p-5 space-y-3">
                  <div className="flex items-center justify-between text-xs text-slate-300">
                    <span>مظنه واحد محاسبه:</span>
                    <span className="font-semibold text-white">
                      {(unit === "gram" ? gramRate : mesghalRate).toLocaleString("fa-IR")} تومان
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-slate-300">
                    <span>کارمزد معامله (Spread-only):</span>
                    <span className="font-semibold text-emerald-400">۰ تومان (رایگان)</span>
                  </div>
                  <div className="pt-3 border-t border-amber-500/20 flex items-center justify-between">
                    <span className="text-sm font-bold text-white">ارزش تقریبی معامله:</span>
                    <span className="text-xl sm:text-2xl font-black text-amber-400">
                      {Math.round(totalCalculated).toLocaleString("fa-IR")}{" "}
                      <span className="text-xs font-normal text-slate-300">تومان</span>
                    </span>
                  </div>
                </div>

                {/* CTA */}
                <a
                  href="https://t.me/TallaEgg_Bot"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="w-full flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-400 to-amber-600 py-3.5 text-sm font-bold text-slate-950 shadow-goldGlowSubtle hover:brightness-110 transition-all"
                >
                  <Send className="h-4 w-4" />
                  <span>ثبت سفارش در ربات تلگرام</span>
                </a>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- FEATURES SECTION ----------------- */}
        <section id="features" className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="text-center max-w-3xl mx-auto mb-16 space-y-3">
            <span className="text-xs font-semibold text-amber-400 uppercase tracking-wider">
              چرا طلااگ؟
            </span>
            <h2 className="text-3xl sm:text-4xl font-extrabold text-white">
              مزیت‌های ساختاری پلتفرم معاملات طلااگ
            </h2>
            <p className="text-sm text-slate-400 font-light">
              طراحی‌شده بر اساس استانداردهای معامله‌گری بین‌المللی و نیازهای فعالان بازار سنتی طلا.
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
                بدون صف و معطلی برای تطبیق سفارش با سایر کاربران؛ طرف حساب معامله همواره میز بازارساز متعهد طلااگ است و اجرای سفارش در کسر ثانیه صورت می‌گیرد.
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
                مراحل ساده
              </span>
              <h2 className="text-3xl font-extrabold text-white">
                چگونه در طلااگ معامله کنیم؟
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
                <h3 className="text-lg font-bold text-white">عضویت در ربات تلگرام</h3>
                <p className="text-xs leading-relaxed text-slate-300 font-light">
                  وارد ربات رسمی تلگرام شوید و دستور <code>/start</code> را ارسال کنید. با ارسال شماره همراه خود احراز هویت اولیه انجام می‌شود.
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
                <h3 className="text-lg font-bold text-white">تأیید حساب و شارژ کیف پول</h3>
                <p className="text-xs leading-relaxed text-slate-300 font-light">
                  پس از تأیید حساب کاربری، کیف پول ریالی یا اعتباری خود را شارژ کنید یا طلای آبشده فیزیکی تحویل دهید تا موجودی شما شارژ شود.
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
                <h3 className="text-lg font-bold text-white">معامله آنلاین و مدیریت دارایی</h3>
                <p className="text-xs leading-relaxed text-slate-300 font-light">
                  از طریق ربات تلگرام یا ورود به پورتال وب، موجودی کیف پول خود را کنترل کرده و در مظنه‌های دلخواه سفارش خرید یا فروش ثبت کنید.
                </p>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- FINAL CTA SECTION ----------------- */}
        <section className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="relative rounded-3xl p-10 sm:p-16 overflow-hidden border border-amber-500/30 bg-gradient-to-br from-[#151a26] via-[#10141e] to-[#090b10] text-center space-y-6 shadow-goldGlow">
            <div className="max-w-2xl mx-auto space-y-4">
              <h2 className="text-3xl sm:text-4xl font-black text-white">
                آماده معامله هوشمند طلای آبشده هستید؟
              </h2>
              <p className="text-sm text-slate-300 font-light leading-relaxed">
                هم‌اکنون به شبکه معامله‌گران طلااگ بپیوندید و امنیت، سرعت و شفافیت را در معاملات روزانه خود تجربه کنید.
              </p>
            </div>

            <div className="flex flex-col sm:flex-row items-center justify-center gap-4 pt-4">
              <a
                href="https://t.me/TallaEgg_Bot"
                target="_blank"
                rel="noopener noreferrer"
                className="w-full sm:w-auto inline-flex items-center justify-center gap-3 rounded-xl bg-gradient-to-r from-amber-400 via-amber-500 to-amber-600 px-8 py-4 text-sm font-bold text-slate-950 shadow-goldGlow hover:brightness-110 transition-all"
              >
                <Send className="h-4 w-4" />
                <span>ورود به ربات معامله‌گری @TallaEgg_Bot</span>
              </a>
              <Link
                href="/login"
                className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-xl border border-slate-700 bg-slate-800/60 px-6 py-4 text-sm font-semibold text-slate-200 hover:bg-slate-700/80 transition-all"
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
