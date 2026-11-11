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
  TrendingUp,
  Layers,
  Coins,
  Gem,
  Users,
  Cpu,
  ChevronLeft,
  Wallet,
  Scale,
  FileCheck2,
  Info,
} from "lucide-react";

export default function HomePage() {
  const [unit, setUnit] = useState<"gram" | "mesghal">("gram");
  const [amountInput, setAmountInput] = useState<string>("10");
  const numericAmount = Math.max(0, parseFloat(amountInput) || 0);

  // Illustrative benchmark rates for RWA calculator (not live exchange guarantees)
  const gramRate = 4350000;
  const mesghalRate = Math.round(gramRate * 4.3318);
  const totalCalculated =
    unit === "gram" ? numericAmount * gramRate : numericAmount * mesghalRate;
  const illustrativeSpreadEst = Math.round(totalCalculated * 0.0023);

  return (
    <>
      <Navbar />

      <main className="relative flex-1">
        {/* ----------------- HERO SECTION ----------------- */}
        <section className="relative pt-12 pb-20 md:pt-20 md:pb-28 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-center">
            {/* Text & Ecosystem Positioning */}
            <div className="lg:col-span-7 space-y-7 text-center lg:text-right">
              <div className="inline-flex items-center gap-2.5 rounded-full border border-[#2B303A] bg-[#1F2329] px-4 py-1.5 text-xs font-semibold text-[#C9A45A]">
                <ZarorbitSymbolSvg sizePx={16} />
                <span className="bidi-isolate">
                  {brandConfig.slogan.categoryEn}
                </span>
                <span className="text-[#4CB3A5]">• Beta</span>
              </div>

              <h1 className="text-4xl sm:text-5xl md:text-[52px] font-black tracking-tight leading-[1.25] text-[#F2EEE5]">
                دارایی‌های واقعی و دیجیتال.{" "}
                <span className="text-[#C9A45A] block mt-2">
                  مالکیت و مشارکت جمعی.
                </span>
              </h1>

              <p className="text-base sm:text-lg text-[#9CA3AF] max-w-2xl font-light leading-relaxed mx-auto lg:mx-0">
                <span className="text-[#F2EEE5] font-semibold bidi-isolate">
                  {brandConfig.name.en}
                </span>{" "}
                یک زیست‌بوم روبه‌توسعه برای اتصال شفاف{" "}
                <strong className="text-[#F2EEE5] font-medium">
                  دارایی‌های دنیای واقعی (RWA با نقطه شروع طلای ۱۸ عیار)
                </strong>{" "}
                و{" "}
                <strong className="text-[#F2EEE5] font-medium">
                  دارایی‌های دیجیتال (رمزارزها و NFTها)
                </strong>{" "}
                است که با رویکرد جامعه‌محور و گام‌به‌گام به سوی حکمرانی{" "}
                <span className="bidi-isolate">DAO</span> حرکت می‌کند.
              </p>

              {/* CTAs */}
              <div className="flex flex-col sm:flex-row items-center justify-center lg:justify-start gap-4 pt-2">
                <Link
                  href="/login"
                  className="w-full sm:w-auto inline-flex items-center justify-center gap-2.5 rounded-[10px] bg-[#C9A45A] px-7 py-3.5 text-sm font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
                >
                  <Wallet className="h-4 w-4" />
                  <span>ورود به پورتال و دفترکل</span>
                </Link>
                <a
                  href={brandConfig.telegram.botUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="w-full sm:w-auto inline-flex items-center justify-center gap-2.5 rounded-[10px] bg-[#286B63] px-7 py-3.5 text-sm font-bold text-white hover:bg-[#328076] transition-colors"
                >
                  <Send className="h-4 w-4" />
                  <span>دسترسی آزمایشی در تلگرام</span>
                </a>
                <Link
                  href="/#ecosystem"
                  className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-[10px] border border-[#6B7485] bg-[#1F2329] px-5 py-3.5 text-sm font-semibold text-[#F2EEE5] hover:border-[#C9A45A] transition-colors"
                >
                  <span>نقشه اکوسیستم</span>
                  <ChevronLeft className="h-4 w-4" />
                </Link>
              </div>

              {/* Three Principles Summary */}
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 pt-5 text-xs text-[#9CA3AF] border-t border-[#2B303A] max-w-2xl mx-auto lg:mx-0">
                <div className="flex items-center gap-2 justify-center lg:justify-start">
                  <ShieldCheck className="h-4 w-4 text-[#48B884] shrink-0" />
                  <span>تفکیک صریح دارایی و سند</span>
                </div>
                <div className="flex items-center gap-2 justify-center lg:justify-start">
                  <Scale className="h-4 w-4 text-[#C9A45A] shrink-0" />
                  <span>اسپرد شفاف؛ کارمزد تراکنش ۰٪</span>
                </div>
                <div className="flex items-center gap-2 justify-center lg:justify-start">
                  <Users className="h-4 w-4 text-[#4CB3A5] shrink-0" />
                  <span>مسیر تدریجی مشارکت جامعه</span>
                </div>
              </div>
            </div>

            {/* Hero Architectural Card */}
            <div className="lg:col-span-5 flex justify-center">
              <div className="w-full max-w-md">
                <BrandHeroShowcase />
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- ECOSYSTEM TAXONOMY (RWA + CRYPTO + NFT + DAO) ----------------- */}
        <section
          id="ecosystem"
          className="py-20 bg-[#101215] border-y border-[#2B303A]"
        >
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex flex-col md:flex-row md:items-end justify-between mb-12 gap-4">
              <div>
                <div className="text-xs font-bold text-[#C9A45A] bidi-isolate uppercase tracking-wider mb-1">
                  ECOSYSTEM ARCHITECTURE &amp; ASSET TAXONOMY
                </div>
                <h2 className="text-2xl sm:text-3xl font-black text-[#F2EEE5]">
                  معماری چندلایه دارایی‌ها و مرز بلوغ هر ماژول
                </h2>
              </div>
              <p className="text-xs text-[#9CA3AF] max-w-md leading-relaxed">
                در{" "}
                <span className="bidi-isolate text-[#F2EEE5]">ZARORBIT</span>،
                طلا تنها نقطه شروع تجاری است و هر کلاس دارایی با وضعیت بلوغ
                واقعی خود تفکیک می‌شود.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              {/* RWA Card */}
              <div className="zar-card-interactive p-6 flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between mb-4">
                    <div className="h-10 w-10 rounded-xl bg-[#2B2416] border border-[#82611C] flex items-center justify-center text-[#C9A45A]">
                      <Coins className="h-5 w-5" />
                    </div>
                    <span className="rounded-full bg-[#192535] border border-[#2D4D73] px-2.5 py-0.5 text-[11px] font-semibold text-[#6BA4E8]">
                      Beta • Focus
                    </span>
                  </div>
                  <h3 className="text-base font-bold text-[#F2EEE5]">
                    دارایی‌های واقعی (RWA)
                  </h3>
                  <p className="text-xs text-[#9CA3AF] mt-2 leading-relaxed">
                    نقطه ورود تجاری با تمرکز بر طلای آبشده ۱۸ عیار؛ با امکان
                    توسعه مشروط به سایر فلزات و کالاهای فیزیکی.
                  </p>
                </div>
                <div className="mt-5 pt-3 border-t border-[#2B303A] text-[11px] text-[#9CA3AF] space-y-1">
                  <div>
                    <strong className="text-[#F2EEE5]">ماهیت:</strong> تعهد
                    قراردادی دفترکل
                  </div>
                  <div>
                    <strong className="text-[#F2EEE5]">تحویل فیزیکی:</strong>{" "}
                    مشروط به حداقل و ضوابط
                  </div>
                </div>
              </div>

              {/* Crypto Card */}
              <div className="zar-card-interactive p-6 flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between mb-4">
                    <div className="h-10 w-10 rounded-xl bg-[#162A24] border border-[#286B63] flex items-center justify-center text-[#4CB3A5]">
                      <Layers className="h-5 w-5" />
                    </div>
                    <span className="rounded-full bg-[#2B2416] border border-[#6B501D] px-2.5 py-0.5 text-[11px] font-semibold text-[#E2A93B]">
                      In Development
                    </span>
                  </div>
                  <h3 className="text-base font-bold text-[#F2EEE5]">
                    رمزارزها (Crypto)
                  </h3>
                  <p className="text-xs text-[#9CA3AF] mt-2 leading-relaxed">
                    طراحی لایه پشتیبانی از دارایی‌های دیجیتال مجاز با اعلام صریح
                    شبکه، کارمزد و مرز مسئولیت نگهداری.
                  </p>
                </div>
                <div className="mt-5 pt-3 border-t border-[#2B303A] text-[11px] text-[#9CA3AF] space-y-1">
                  <div>
                    <strong className="text-[#F2EEE5]">وضعیت:</strong> در حال
                    توسعه زیرساخت
                  </div>
                  <div>
                    <strong className="text-[#F2EEE5]">تضمین بازدهی:</strong>{" "}
                    ندارد (دارایی پرریسک)
                  </div>
                </div>
              </div>

              {/* NFT Card */}
              <div className="zar-card-interactive p-6 flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between mb-4">
                    <div className="h-10 w-10 rounded-xl bg-[#282D35] border border-[#3A414D] flex items-center justify-center text-[#F2EEE5]">
                      <Gem className="h-5 w-5" />
                    </div>
                    <span className="rounded-full bg-[#17191D] border border-[#3A414D] px-2.5 py-0.5 text-[11px] font-semibold text-[#9CA3AF]">
                      Planned
                    </span>
                  </div>
                  <h3 className="text-base font-bold text-[#F2EEE5]">
                    توکن‌های غیرمثلی (NFT)
                  </h3>
                  <p className="text-xs text-[#9CA3AF] mt-2 leading-relaxed">
                    اتصال خالقان و جوامع از طریق رکوردهای منحصربه‌فرد دیجیتال با
                    بیان روشن مرز مالکیت فکری و فیزیکی.
                  </p>
                </div>
                <div className="mt-5 pt-3 border-t border-[#2B303A] text-[11px] text-[#9CA3AF] space-y-1">
                  <div>
                    <strong className="text-[#F2EEE5]">کپی‌رایت:</strong>{" "}
                    خودکار منتقل نمی‌شود
                  </div>
                  <div>
                    <strong className="text-[#F2EEE5]">دارایی فیزیکی:</strong>{" "}
                    نیازمند قرارداد مجزا
                  </div>
                </div>
              </div>

              {/* DAO + AI/XR Card */}
              <div className="zar-card-interactive p-6 flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between mb-4">
                    <div className="h-10 w-10 rounded-xl bg-[#282D35] border border-[#3A414D] flex items-center justify-center text-[#C9A45A]">
                      <Cpu className="h-5 w-5" />
                    </div>
                    <span className="rounded-full bg-[#17191D] border border-[#82611C] border-dashed px-2.5 py-0.5 text-[11px] font-semibold text-[#C9A45A]">
                      In Dev / Conceptual
                    </span>
                  </div>
                  <h3 className="text-base font-bold text-[#F2EEE5]">
                    حکمرانی DAO و افق AI / XR
                  </h3>
                  <p className="text-xs text-[#9CA3AF] mt-2 leading-relaxed">
                    حرکت تدریجی از مشارکت جامعه به سوی پیشنهادها و رأی‌گیری
                    شفاف؛ افق بلندمدت متاورس تعامل‌پذیر و کمک‌تصمیم هوشمند.
                  </p>
                </div>
                <div className="mt-5 pt-3 border-t border-[#2B303A] text-[11px] text-[#9CA3AF] space-y-1">
                  <div>
                    <strong className="text-[#F2EEE5]">مدل DAO:</strong> تدریجی
                    و پاسخگو
                  </div>
                  <div>
                    <strong className="text-[#F2EEE5]">XR / متاورس:</strong>{" "}
                    چشم‌انداز آتی
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- RWA BENCHMARK RATES ----------------- */}
        <section id="rates" className="py-20">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex flex-col md:flex-row md:items-end justify-between mb-8 gap-4">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <span className="h-2 w-2 rounded-full bg-[#4CB3A5]" />
                  <span className="bidi-isolate text-xs font-bold text-[#4CB3A5] uppercase tracking-wider">
                    INITIAL COMMERCIAL FOCUS • RWA BENCHMARKS
                  </span>
                </div>
                <h2 className="text-2xl sm:text-3xl font-bold text-[#F2EEE5]">
                  نمایش ساختار قیمت‌گذاری ماژول طلای ۱۸ عیار (نمونه مرجع)
                </h2>
              </div>
              <p className="text-xs text-[#9CA3AF] font-light">
                نرخ‌های زیر نمونه مرجع رابط کاربری هستند؛ نرخ اجرایی قطعی هنگام
                ثبت سفارش توسط میز اعلام می‌شود.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {/* Card 1: MAUA/IRT */}
              <div className="zar-card-interactive p-6">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-3">
                    <div className="h-10 w-10 rounded-xl bg-[#17191D] border border-[#2B303A] flex items-center justify-center">
                      <ZarorbitSymbolSvg sizePx={24} />
                    </div>
                    <div>
                      <h3 className="font-bold text-[#F2EEE5] text-base">
                        طلای آبشده ۱۸ عیار
                      </h3>
                      <span className="bidi-isolate text-[11px] text-[#9CA3AF] font-mono">
                        MAUA / IRT (1g)
                      </span>
                    </div>
                  </div>
                  <span className="shrink-0 whitespace-nowrap inline-flex items-center gap-1 rounded-full bg-[#162A24] px-2.5 py-0.5 text-[11px] font-medium text-[#48B884] border border-[#286B63]">
                    <TrendingUp className="h-3 w-3" />
                    Beta Focus
                  </span>
                </div>

                <div className="space-y-3 pt-2 tabular-nums">
                  <div className="flex items-center justify-between text-sm py-2 border-b border-[#2B303A]">
                    <span className="text-[#9CA3AF]">
                      نرخ مرجع خرید از میز (Ask):
                    </span>
                    <span className="font-bold text-[#F2EEE5]">
                      ۴,۳۵۵,۰۰۰{" "}
                      <span className="text-xs font-normal text-[#9CA3AF]">
                        تومان
                      </span>
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-sm py-2 border-b border-[#2B303A]">
                    <span className="text-[#9CA3AF]">
                      نرخ مرجع فروش به میز (Bid):
                    </span>
                    <span className="font-bold text-[#C9A45A]">
                      ۴,۳۴۵,۰۰۰{" "}
                      <span className="text-xs font-normal text-[#9CA3AF]">
                        تومان
                      </span>
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-[#9CA3AF] pt-1">
                    <span>اسپرد شفاف خرید/فروش:</span>
                    <span className="text-[#4CB3A5] font-semibold">
                      ۱۰,۰۰۰ تومان (کارمزد: ۰٪)
                    </span>
                  </div>
                </div>

                <div className="mt-5 pt-3">
                  <a
                    href={brandConfig.telegram.botUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="w-full flex items-center justify-center gap-2 rounded-[10px] border border-[#6B7485] bg-[#17191D] py-2.5 text-xs font-semibold text-[#F2EEE5] hover:border-[#C9A45A] hover:text-[#C9A45A] transition-colors"
                  >
                    <span>مشاهده مظنه در ربات</span>
                    <ChevronLeft className="h-4 w-4" />
                  </a>
                </div>
              </div>

              {/* Card 2: SEKE_BAHAR */}
              <div className="zar-card-interactive p-6">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-3">
                    <div className="h-10 w-10 rounded-xl bg-[#17191D] border border-[#2B303A] flex items-center justify-center">
                      <ZarorbitSymbolSvg sizePx={24} />
                    </div>
                    <div>
                      <h3 className="font-bold text-[#F2EEE5] text-base">
                        سکه بهار آزادی (نمونه RWA)
                      </h3>
                      <span className="bidi-isolate text-[11px] text-[#9CA3AF] font-mono">
                        SEKE_BAHAR / IRT
                      </span>
                    </div>
                  </div>
                  <span className="rounded-full bg-[#192535] px-2.5 py-0.5 text-[11px] font-medium text-[#6BA4E8] border border-[#2D4D73]">
                    نمونه مرجع
                  </span>
                </div>

                <div className="space-y-3 pt-2 tabular-nums">
                  <div className="flex items-center justify-between text-sm py-2 border-b border-[#2B303A]">
                    <span className="text-[#9CA3AF]">نرخ نمونه خرید:</span>
                    <span className="font-bold text-[#F2EEE5]">
                      ۴۸,۲۰۰,۰۰۰{" "}
                      <span className="text-xs font-normal text-[#9CA3AF]">
                        تومان
                      </span>
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-sm py-2 border-b border-[#2B303A]">
                    <span className="text-[#9CA3AF]">نرخ نمونه فروش:</span>
                    <span className="font-bold text-[#C9A45A]">
                      ۴۷,۹۵۰,۰۰۰{" "}
                      <span className="text-xs font-normal text-[#9CA3AF]">
                        تومان
                      </span>
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-[#9CA3AF] pt-1">
                    <span>تفکیک اسپرد:</span>
                    <span className="text-[#F2EEE5] font-semibold">
                      ۲۵۰,۰۰۰ تومان
                    </span>
                  </div>
                </div>

                <div className="mt-5 pt-3">
                  <a
                    href={brandConfig.telegram.botUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="w-full flex items-center justify-center gap-2 rounded-[10px] border border-[#6B7485] bg-[#17191D] py-2.5 text-xs font-semibold text-[#F2EEE5] hover:border-[#C9A45A] hover:text-[#C9A45A] transition-colors"
                  >
                    <span>بررسی در ربات تلگرام</span>
                    <ChevronLeft className="h-4 w-4" />
                  </a>
                </div>
              </div>

              {/* Card 3: XAU/USD */}
              <div className="zar-card-interactive p-6">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-3">
                    <div className="h-10 w-10 rounded-xl bg-[#17191D] border border-[#2B303A] flex items-center justify-center">
                      <ZarorbitSymbolSvg sizePx={24} />
                    </div>
                    <div>
                      <h3 className="font-bold text-[#F2EEE5] text-base">
                        انس جهانی طلا (شاخص)
                      </h3>
                      <span className="bidi-isolate text-[11px] text-[#9CA3AF] font-mono">
                        XAU / USD
                      </span>
                    </div>
                  </div>
                  <span className="rounded-full bg-[#2B2416] px-2.5 py-0.5 text-[11px] font-medium text-[#C9A45A] border border-[#82611C]">
                    شاخص مرجع
                  </span>
                </div>

                <div className="space-y-3 pt-2 tabular-nums">
                  <div className="flex items-center justify-between text-sm py-2 border-b border-[#2B303A]">
                    <span className="text-[#9CA3AF]">مقدار مرجع نمایشی:</span>
                    <span className="bidi-isolate font-bold text-[#F2EEE5]">
                      $2,745.80
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-sm py-2 border-b border-[#2B303A]">
                    <span className="text-[#9CA3AF]">معادل وزنی هر انس:</span>
                    <span className="font-medium text-[#F2EEE5]">
                      ۳۱.۱۰۳۵ گرم (خالص ۲۴ عیار)
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-[#9CA3AF] pt-1">
                    <span>نقش در مدل:</span>
                    <span className="text-[#4CB3A5]">پارامتر مرجع داخلی</span>
                  </div>
                </div>

                <div className="mt-5 pt-3">
                  <div className="w-full text-center py-2.5 text-xs text-[#9CA3AF] bg-[#17191D] rounded-[10px] border border-[#2B303A]">
                    داده‌های بالا صرفاً نمایشی هستند
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- ARCHITECTURAL 1-0-1 ORBIT & GOVERNANCE ----------------- */}
        <section
          id="governance"
          className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto"
        >
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-center">
            <div className="lg:col-span-7 space-y-6 text-center lg:text-right">
              <div className="inline-flex items-center gap-2 rounded-full border border-[#286B63] bg-[#162A24] px-4 py-1.5 text-xs font-semibold text-[#4CB3A5]">
                <span className="bidi-isolate">
                  OPEN INTEROPERABILITY • PROGRESSIVE DAO
                </span>
              </div>

              <h2 className="text-3xl sm:text-4xl font-black text-[#F2EEE5] leading-tight">
                زیرساخت دفترکل دوطرفه و{" "}
                <span className="text-[#C9A45A]">
                  شفافیت حقوق مالکیت دارایی
                </span>
              </h2>

              <p className="text-sm sm:text-base text-[#9CA3AF] font-light leading-relaxed max-w-2xl mx-auto lg:mx-0">
                در{" "}
                <span className="bidi-isolate text-[#F2EEE5]">ZARORBIT</span>،
                پیش از هر تعامل باید مشخص باشد که کاربر دقیقاً با چه دارایی
                مواجه است: تعهد قراردادی طلا، دارایی شبکه‌ای (رمزارز) یا رکورد
                NFT — و چه حقوق، هزینه‌ها و محدودیت‌هایی بر آن حاکم است.
              </p>

              {/* Mandatory Brand Book §10.1 Checklist */}
              <div className="zar-card p-5 text-right space-y-3">
                <div className="flex items-center gap-2 text-xs font-bold text-[#C9A45A]">
                  <FileCheck2 className="h-4 w-4" />
                  <span>چک‌لیست شفافیت دارایی (Brand Book §10.1)</span>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs text-[#9CA3AF]">
                  <div className="rounded-lg bg-[#17191D] border border-[#2B303A] p-3">
                    <strong className="text-[#F2EEE5] block mb-1">
                      ۱. تفکیک نوع رکورد
                    </strong>
                    ثبت دیجیتال به خودی خود سند قانونی ملک یا طلای فیزیکی نیست
                    مگر شرط تحویل فعال باشد.
                  </div>
                  <div className="rounded-lg bg-[#17191D] border border-[#2B303A] p-3">
                    <strong className="text-[#F2EEE5] block mb-1">
                      ۲. پاسخگویی در مدل DAO
                    </strong>
                    توسعه DAO جایگزین تعهدات حقوقی، نگهداری و پاسخگویی اپراتور
                    نمی‌شود.
                  </div>
                </div>
              </div>
            </div>

            <div className="lg:col-span-5 flex justify-center">
              <ZarorbitOrbitalMotion className="w-full max-w-[420px]" />
            </div>
          </div>
        </section>

        {/* ----------------- RWA VALUATION CALCULATOR ----------------- */}
        <section
          id="calculator"
          className="py-20 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto"
        >
          <div className="zar-card p-8 sm:p-12 shadow-card">
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-center">
              <div className="lg:col-span-6 space-y-6">
                <div>
                  <span className="bidi-isolate text-xs font-bold text-[#C9A45A] uppercase tracking-wider">
                    RWA BENCHMARK CALCULATOR • 18K GOLD
                  </span>
                  <h2 className="text-2xl sm:text-3xl font-black text-[#F2EEE5] mt-1">
                    محاسبه‌گر برآورد ارزش (واحد طلای ۱۸ عیار)
                  </h2>
                  <p className="text-sm text-[#9CA3AF] font-light leading-relaxed mt-2">
                    وزن دلخواه را برای مشاهده برآورد ارزش بر پایه نرخ نمونه و
                    نحوه نمایش شفاف اسپرد و کارمزد وارد کنید.
                  </p>
                </div>

                {/* Clean Architectural Asset Card Preview */}
                <div className="rounded-2xl bg-[#17191D] border border-[#2B303A] p-6 space-y-4">
                  <div className="flex items-center justify-between text-[11px] text-[#9CA3AF] border-b border-[#2B303A] pb-3">
                    <span className="bidi-isolate font-mono text-[#C9A45A] font-bold">
                      XAU18-RWA • UNIT ESTIMATE
                    </span>
                    <span>عیار استاندارد ۱۸ (۷۵۰)</span>
                  </div>
                  <div className="flex items-baseline justify-between">
                    <span className="text-xs text-[#9CA3AF]">
                      مقدار انتخابی:
                    </span>
                    <div className="text-2xl sm:text-3xl font-black text-[#F2EEE5] tabular-nums">
                      {numericAmount.toLocaleString("fa-IR")}{" "}
                      <span className="text-sm font-semibold text-[#C9A45A]">
                        {unit === "gram" ? "گرم" : "مثقال"}
                      </span>
                    </div>
                  </div>
                  <div className="rounded-lg bg-[#1F2329] border border-[#2B303A] p-3 flex items-start gap-2.5 text-xs text-[#9CA3AF]">
                    <Info className="h-4 w-4 text-[#4CB3A5] shrink-0 mt-0.5" />
                    <span>
                      در مدل بازارساز، کارمزد تراکنش برابر صفر است و مابه‌التفاوت
                      خرید و فروش (اسپرد تقریبی{" "}
                      <strong className="text-[#F2EEE5] tabular-nums">
                        {illustrativeSpreadEst.toLocaleString("fa-IR")} تومان
                      </strong>
                      ) پیش از تایید نمایش داده می‌شود.
                    </span>
                  </div>
                </div>

                {/* Presets */}
                <div>
                  <span className="text-xs text-[#9CA3AF] block mb-2">
                    مقادیر نمونه پرکاربرد:
                  </span>
                  <div className="flex flex-wrap gap-2">
                    {[1, 5, 10, 20, 50, 100].map((val) => (
                      <button
                        key={val}
                        onClick={() => {
                          setUnit("gram");
                          setAmountInput(String(val));
                        }}
                        className={`px-3 py-1.5 rounded-[10px] text-xs font-medium border transition-colors ${
                          numericAmount === val && unit === "gram"
                            ? "bg-[#C9A45A] text-[#17191D] border-[#C9A45A] font-bold"
                            : "bg-[#17191D] text-[#F2EEE5] border-[#6B7485] hover:border-[#C9A45A]"
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
                        className={`px-3 py-1.5 rounded-[10px] text-xs font-medium border transition-colors ${
                          numericAmount === val && unit === "mesghal"
                            ? "bg-[#C9A45A] text-[#17191D] border-[#C9A45A] font-bold"
                            : "bg-[#17191D] text-[#F2EEE5] border-[#6B7485] hover:border-[#C9A45A]"
                        }`}
                      >
                        {val} مثقال
                      </button>
                    ))}
                  </div>
                </div>
              </div>

              {/* Calculator Input Panel */}
              <div className="lg:col-span-6 bg-[#17191D] rounded-2xl p-6 sm:p-8 border border-[#2B303A] space-y-6">
                <div className="flex rounded-[10px] bg-[#101215] p-1 border border-[#2B303A]">
                  <button
                    onClick={() => setUnit("gram")}
                    className={`flex-1 py-2 text-xs font-bold rounded-[8px] transition-colors ${
                      unit === "gram"
                        ? "bg-[#C9A45A] text-[#17191D]"
                        : "text-[#9CA3AF] hover:text-[#F2EEE5]"
                    }`}
                  >
                    گرم (عیار ۱۸ / ۷۵۰)
                  </button>
                  <button
                    onClick={() => setUnit("mesghal")}
                    className={`flex-1 py-2 text-xs font-bold rounded-[8px] transition-colors ${
                      unit === "mesghal"
                        ? "bg-[#C9A45A] text-[#17191D]"
                        : "text-[#9CA3AF] hover:text-[#F2EEE5]"
                    }`}
                  >
                    مثقال طلا (۴.۳۳ گرم)
                  </button>
                </div>

                <div>
                  <label
                    htmlFor="gold-calc-input"
                    className="block text-xs text-[#9CA3AF] mb-2"
                  >
                    وزن مورد نظر ({unit === "gram" ? "گرم" : "مثقال"}):
                  </label>
                  <div className="relative">
                    <input
                      id="gold-calc-input"
                      type="number"
                      step="0.01"
                      min="0.01"
                      value={amountInput}
                      onChange={(e) => setAmountInput(e.target.value)}
                      className="w-full rounded-[10px] bg-[#1F2329] border border-[#6B7485] pl-24 pr-4 py-3.5 text-lg font-bold text-[#F2EEE5] focus:border-[#C9A45A] focus:outline-none focus:ring-1 focus:ring-[#C9A45A] text-left tabular-nums"
                      dir="ltr"
                    />
                    <span className="bidi-isolate absolute left-3 top-1/2 -translate-y-1/2 text-xs text-[#9CA3AF] font-medium bg-[#282D35] px-2.5 py-1 rounded">
                      {unit === "gram" ? "Gram" : "Mesghal"}
                    </span>
                  </div>
                </div>

                <div className="rounded-xl bg-[#1F2329] border border-[#2B303A] p-5 space-y-3 tabular-nums">
                  <div className="flex items-center justify-between text-xs text-[#9CA3AF]">
                    <span>
                      نرخ مرجع ({unit === "gram" ? "هر گرم ۱۸ عیار" : "هر مثقال"}
                      ):
                    </span>
                    <span className="font-semibold text-[#F2EEE5]">
                      {(unit === "gram" ? gramRate : mesghalRate).toLocaleString(
                        "fa-IR"
                      )}{" "}
                      تومان
                    </span>
                  </div>
                  <div className="flex items-center justify-between text-xs text-[#9CA3AF]">
                    <span>کارمزد تراکنش در این فاز:</span>
                    <span className="font-semibold text-[#48B884]">
                      ۰ تومان (اسپرد قیمتی)
                    </span>
                  </div>
                  <div className="pt-3 border-t border-[#2B303A] flex items-center justify-between">
                    <span className="text-sm font-bold text-[#F2EEE5]">
                      برآورد ارزش (نمایشی):
                    </span>
                    <span className="text-2xl sm:text-3xl font-black text-[#C9A45A]">
                      {Math.round(totalCalculated).toLocaleString("fa-IR")}{" "}
                      <span className="text-xs font-normal text-[#9CA3AF]">
                        تومان
                      </span>
                    </span>
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  <a
                    href={brandConfig.telegram.botUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center justify-center gap-2 rounded-[10px] bg-[#C9A45A] py-3.5 text-xs font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
                  >
                    <Send className="h-4 w-4" />
                    <span>ثبت تعامل در ربات</span>
                  </a>
                  <Link
                    href="/login"
                    className="flex items-center justify-center gap-2 rounded-[10px] bg-[#286B63] py-3.5 text-xs font-bold text-white hover:bg-[#328076] transition-colors"
                  >
                    <Wallet className="h-4 w-4" />
                    <span>مشاهده کیف پول</span>
                  </Link>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- FINAL CTA SECTION ----------------- */}
        <section className="py-16 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
          <div className="zar-card p-10 sm:p-14 text-center space-y-6">
            <div className="flex justify-center">
              <div className="h-16 w-16 rounded-2xl bg-[#17191D] border border-[#2B303A] flex items-center justify-center">
                <ZarorbitSymbolSvg sizePx={44} />
              </div>
            </div>
            <div className="max-w-2xl mx-auto space-y-3">
              <h2 className="text-2xl sm:text-3xl font-black text-[#F2EEE5]">
                همراهی در شکل‌گیری زیست‌بوم{" "}
                <span className="bidi-isolate text-[#C9A45A]">ZARORBIT</span>
              </h2>
              <p className="text-sm text-[#9CA3AF] font-light leading-relaxed">
                از پورتال وب برای بررسی دفترکل دارایی‌ها استفاده کنید یا در
                ربات تلگرام ماژول آزمایشی طلای ۱۸ عیار را تجربه نمایید.
              </p>
            </div>
            <div className="flex flex-col sm:flex-row items-center justify-center gap-4 pt-2">
              <Link
                href="/login"
                className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-[10px] bg-[#C9A45A] px-7 py-3.5 text-sm font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
              >
                <Wallet className="h-4 w-4" />
                <span>ورود به پرتال وب</span>
              </Link>
              <a
                href={brandConfig.telegram.botUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-[10px] border border-[#6B7485] bg-[#17191D] px-7 py-3.5 text-sm font-semibold text-[#F2EEE5] hover:border-[#4CB3A5] hover:text-[#4CB3A5] transition-colors"
              >
                <Send className="h-4 w-4 text-[#4CB3A5]" />
                <span>
                  ربات رسمی{" "}
                  <span className="bidi-isolate">
                    @{brandConfig.telegram.botUsername}
                  </span>
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
