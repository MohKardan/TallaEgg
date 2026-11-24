import Link from "next/link";
import { BookOpenCheck, Scale, Send, ShieldCheck, Wallet } from "lucide-react";
import Navbar from "@/components/Navbar";
import Footer from "@/components/Footer";
import HeroLedgerPreview from "@/components/hero/HeroLedgerPreview";
import FeatureBento from "@/components/sections/FeatureBento";
import GoldCalculator from "@/components/calculator/GoldCalculator";
import TransparencySection from "@/components/sections/TransparencySection";
import { brandConfig } from "@/config/brand.config";

export default function HomePage() {
  return (
    <>
      <Navbar />

      <main id="main-content" className="relative flex-1">
        {/* ----------------- HERO SECTION (PRODUCT-FIRST, STRICT CTA DISCIPLINE) ----------------- */}
        <section className="relative pt-12 pb-16 sm:pt-16 sm:pb-20 lg:pt-20 lg:pb-24">
          <div className="mx-auto max-w-[1200px] px-4 sm:px-6 lg:px-8">
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 lg:gap-12 items-center">
              {/* Right Column: Clear Value Proposition + Single Primary Action */}
              <div className="lg:col-span-7 space-y-6 text-center lg:text-right">
                <div className="inline-flex items-center gap-2 rounded-full border border-[#286B63]/80 bg-[#162A24] px-3.5 py-1.5 text-xs font-semibold text-[#48B884]">
                  <span className="h-1.5 w-1.5 rounded-full bg-[#48B884]" />
                  <span>پایلوت فعال • زیرساخت معاملات طلای آبشده</span>
                </div>

                <h1 className="text-[36px] sm:text-[44px] lg:text-[48px] font-black tracking-tight leading-[1.3] text-[#F2EEE5]">
                  زیرساخت شفاف{" "}
                  <span className="text-[#C9A45A] block mt-1.5">
                    معاملات طلا و دفترکل متقابل.
                  </span>
                </h1>

                <p className="text-base sm:text-[17px] text-[#F2EEE5]/75 max-w-xl leading-[1.85] mx-auto lg:mx-0">
                  <strong className="text-[#F2EEE5] font-semibold">
                    {brandConfig.name.fa}
                  </strong>{" "}
                  سامانه معاملاتی و دفترکل دقیق برای طلافروشی‌ها و مشتریان
                  تاییدشده آن‌هاست: اعلام مظنه لحظه‌ای، ثبت فوری معامله در تلگرام
                  و کارمزد{" "}
                  <strong className="text-[#C9A45A] font-semibold tabular-nums">
                    ۰.۰۰٪
                  </strong>{" "}
                  بر پایه اسپرد شفاف.
                </p>

                {/* Strict 2-Action Hierarchy (1 Primary Gold + 1 Quiet Secondary) */}
                <div className="flex flex-col sm:flex-row items-center justify-center lg:justify-start gap-3.5 pt-2">
                  <a
                    href={brandConfig.telegram.botUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="w-full sm:w-auto inline-flex h-12 items-center justify-center gap-2.5 rounded-[10px] bg-[#C9A45A] px-7 text-sm font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
                  >
                    <Send className="h-4 w-4" />
                    <span>شروع در ربات تلگرام</span>
                  </a>
                  <Link
                    href="/login"
                    className="w-full sm:w-auto inline-flex h-12 items-center justify-center gap-2.5 rounded-[10px] border border-white/[0.14] bg-[#1F2329] px-6 text-sm font-semibold text-[#F2EEE5] hover:border-[#4CB3A5]/60 hover:text-[#4CB3A5] transition-colors"
                  >
                    <Wallet className="h-4 w-4 text-[#4CB3A5]" />
                    <span>ورود به پورتال دفترکل</span>
                  </Link>
                </div>
              </div>

              {/* Left Column: Interactive Sample Dealer-Book & Wallet Preview */}
              <div className="lg:col-span-5">
                <HeroLedgerPreview />
              </div>
            </div>

            {/* Inline Operational Trust Strip (hairline dividers, zero redundant boxes) */}
            <div className="mt-14 pt-6 border-t border-white/[0.08] grid grid-cols-1 sm:grid-cols-3 gap-4 sm:gap-6 text-sm text-[#F2EEE5]/75">
              <div className="flex items-center gap-2.5 justify-center lg:justify-start">
                <ShieldCheck className="h-4 w-4 text-[#48B884] shrink-0" />
                <span>طرف حساب مستقیم و تسویه شفاف با طلافروشی</span>
              </div>
              <div className="flex items-center gap-2.5 justify-center lg:justify-start">
                <Scale className="h-4 w-4 text-[#C9A45A] shrink-0" />
                <span>کارمزد تراکنش ۰.۰۰٪ • درآمد فقط از اسپرد روشن</span>
              </div>
              <div className="flex items-center gap-2.5 justify-center lg:justify-start">
                <BookOpenCheck className="h-4 w-4 text-[#4CB3A5] shrink-0" />
                <span>ثبت آنی با شناسه یکتا و تاریخ شمسی دقیق</span>
              </div>
            </div>
          </div>
        </section>

        {/* ----------------- ASYMMETRIC INSTITUTIONAL BENTO ----------------- */}
        <FeatureBento />

        {/* ----------------- INTERACTIVE GOLD WEIGHT & VALUE CALCULATOR ----------------- */}
        <GoldCalculator />

        {/* ----------------- TRANSPARENCY, 3D S1 CORE & VISION GATEWAY ----------------- */}
        <TransparencySection />
      </main>

      <Footer />
    </>
  );
}
