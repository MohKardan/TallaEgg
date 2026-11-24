import React from "react";
import Link from "next/link";
import { ChevronLeft, Compass, FileCheck2 } from "lucide-react";
import { brandConfig } from "@/config/brand.config";
import LazyOrbitalCore from "@/components/sections/LazyOrbitalCore";

const TRANSPARENCY_PRINCIPLES = [
  {
    title: "طرف حساب مستقیم",
    text: "معامله میان مشتری تاییدشده و طلافروشی (بازارگردان) ثبت می‌شود و شرایط تسویه فیزیکی و ریالی مستقیماً میان دو طرف تعریف می‌گردد.",
  },
  {
    title: "ساختار درآمد روشن",
    text: "کارمزد معاملاتی فعلی ۰.۰۰٪ است و مابه‌التفاوت قیمت خرید و فروش (اسپرد) پیش از تایید معامله به‌وضوح نمایش داده می‌شود.",
  },
  {
    title: "ثبت قابل ممیزی",
    text: "هر معامله، اعتبار و تسویه با شناسه یکتا در دفترکل ثبت می‌شود و تاریخچه آن در ربات تلگرام و پورتال کیف پول قابل بررسی است.",
  },
  {
    title: "تفکیک امروز از فردا",
    text: "قابلیت‌های فعلی روی طلای آبشده متمرکز است و توسعه دارایی‌های واقعی (RWA)، دیجیتال و حکمرانی جمعی به‌صورت مرحله‌ای در صفحه چشم‌انداز پیگیری می‌شود.",
  },
];

export default function TransparencySection() {
  return (
    <section
      id="transparency"
      className="py-20 sm:py-24 bg-[#101215] border-t border-white/[0.08]"
    >
      <div className="mx-auto max-w-[1200px] px-4 sm:px-6 lg:px-8 space-y-14">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 lg:gap-12 items-center">
          <div className="lg:col-span-6 space-y-6">
            <div className="text-xs font-bold text-[#C9A45A] bidi-isolate uppercase tracking-[0.1em]">
              TRANSPARENCY &amp; OPERATIONAL BOUNDARIES
            </div>
            <h2 className="text-2xl sm:text-[32px] font-extrabold text-[#F2EEE5] leading-[1.35]">
              شفافیت معاملاتی و مرزهای حقوقی
            </h2>
            <p className="text-[15px] text-[#F2EEE5]/75 leading-[1.85]">
              در{" "}
              <span className="text-[#F2EEE5] font-semibold">
                {brandConfig.name.fa}
              </span>
              ، اعتماد بر پایه بیان دقیق واقعیت فنی و حقوقی ساخته می‌شود، نه
              شعارهای تبلیغاتی. پیش از هر معامله، چهار اصل زیر برای طرفین روشن
              است:
            </p>

            <div className="zar-panel p-5 sm:p-6 space-y-4">
              <div className="flex items-center gap-2 text-sm text-[#C9A45A] font-bold">
                <FileCheck2 className="h-4 w-4 shrink-0" />
                <span>اصول شفافیت حساب و دارایی</span>
              </div>
              <ul className="space-y-3.5 text-sm text-[#F2EEE5]/75 leading-[1.8]">
                {TRANSPARENCY_PRINCIPLES.map((item) => (
                  <li key={item.title} className="flex items-start gap-2.5">
                    <span
                      aria-hidden="true"
                      className="mt-2 h-1.5 w-1.5 rounded-full bg-[#48B884] shrink-0"
                    />
                    <span>
                      <strong className="text-[#F2EEE5] font-semibold">
                        {item.title}:
                      </strong>{" "}
                      {item.text}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          </div>

          <div className="lg:col-span-6">
            <LazyOrbitalCore />
          </div>
        </div>

        {/* Restrained Editorial Gateway to /vision (replaces loud redundant banner) */}
        <div className="border-t border-white/[0.08] pt-10 flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div className="space-y-2 max-w-2xl">
            <div className="inline-flex items-center gap-2 text-xs font-bold text-[#4CB3A5]">
              <Compass className="h-4 w-4" />
              <span>چشم‌انداز بلندمدت اکوسیستم</span>
            </div>
            <p className="text-sm sm:text-[15px] text-[#F2EEE5]/75 leading-[1.85]">
              معماری ماژولار{" "}
              <span className="bidi-isolate font-semibold text-[#F2EEE5]">
                ZARORBIT
              </span>{" "}
              پس از تثبیت معاملات طلا، به‌صورت گام‌به‌گام به سمت سایر دارایی‌های
              واقعی (RWA)، دارایی‌های دیجیتال و حکمرانی جمعی (DAO) گسترش می‌یابد.
            </p>
          </div>
          <Link
            href="/vision"
            className="inline-flex items-center gap-1.5 text-sm font-bold text-[#C9A45A] hover:text-[#D4B16A] transition-colors shrink-0 self-start md:self-center"
          >
            <span>مطالعه نقشه راه صادقانه و بلوغ ماژول‌ها</span>
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </div>
      </div>
    </section>
  );
}
