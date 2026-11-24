import React from "react";
import {
  ArrowLeftRight,
  BookOpenCheck,
  Building2,
  Clock,
  CheckCircle2,
} from "lucide-react";

const WORKFLOW_STEPS = [
  { num: "۱", title: "تایید مشتری", desc: "ورود با کد معرف طلافروشی" },
  { num: "۲", title: "اعلام مظنه", desc: "نرخ خرید و فروش با کنترل بازه" },
  { num: "۳", title: "ثبت فوری ۰٪", desc: "تایید معامله روی ربات تلگرام" },
  { num: "۴", title: "دفترکل و تسویه", desc: "مانده طلایی/ریالی با تاریخ شمسی" },
];

export default function FeatureBento() {
  return (
    <section
      id="platform"
      className="py-20 sm:py-24 bg-[#101215] border-y border-white/[0.08]"
    >
      <div className="mx-auto max-w-[1200px] px-4 sm:px-6 lg:px-8">
        <div className="flex flex-col md:flex-row md:items-end justify-between mb-12 gap-4">
          <div className="space-y-2 max-w-2xl">
            <div className="text-xs font-bold text-[#C9A45A] bidi-isolate uppercase tracking-[0.1em]">
              ACTIVE TRADING INFRASTRUCTURE
            </div>
            <h2 className="text-2xl sm:text-[32px] font-extrabold text-[#F2EEE5] leading-[1.35]">
              امکانات عملیاتی امروز برای طلافروشی و مشتریان
            </h2>
          </div>
          <p className="text-sm sm:text-[15px] text-[#9CA3AF] max-w-md leading-[1.8]">
            جایگزینی سفارش‌گیری تلفنی و دفترچه‌های دستی با جریان معاملاتی دقیق،
            قابل ممیزی و همگام با رویه واقعی بازار طلا.
          </p>
        </div>

        {/* Asymmetric Bento Row 1 (7 + 5) */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
          {/* Flagship Wide Bento Card: Dealer Book & Cross-Asset Credit */}
          <div className="lg:col-span-7 zar-panel border-[#C9A45A]/25 p-6 sm:p-7 flex flex-col justify-between gap-6">
            <div className="space-y-3">
              <div className="flex items-center justify-between gap-3">
                <div className="flex h-11 w-11 items-center justify-center rounded-[12px] bg-[#162A24] border border-[#286B63] text-[#4CB3A5]">
                  <ArrowLeftRight className="h-5 w-5" />
                </div>
                <span className="inline-flex items-center gap-1.5 rounded-full bg-[#162A24] border border-[#286B63] px-3 py-1 text-xs font-semibold text-[#48B884]">
                  <CheckCircle2 className="h-3.5 w-3.5" />
                  <span>فعال در پایلوت</span>
                </span>
              </div>

              <h3 className="text-xl font-bold text-[#F2EEE5]">
                دفترکل بازارگردان و اعتبار متقابل بین‌دارایی
              </h3>
              <p className="text-[15px] text-[#F2EEE5]/75 leading-[1.85]">
                طلافروشی برای مشتریان شناخته‌شده خود سقف اعتبار ریالی و طلایی
                تعریف می‌کند؛ بدون نیاز به درگاه پرداخت اجباری در لحظه ثبت
                معامله، تمام بدهکار/بستانکارها به‌صورت خودکار در دفترکل متقابل
                ثبت می‌شوند.
              </p>
            </div>

            {/* 4-Step Operational Workflow Anatomy */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5 pt-2 border-t border-white/[0.08]">
              {WORKFLOW_STEPS.map((step) => (
                <div
                  key={step.num}
                  className="rounded-[12px] bg-[#17191D] border border-white/[0.07] p-3 space-y-1"
                >
                  <span className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-[#C9A45A]/15 text-xs font-bold text-[#C9A45A]">
                    {step.num}
                  </span>
                  <div className="text-xs sm:text-sm font-bold text-[#F2EEE5]">
                    {step.title}
                  </div>
                  <div className="text-xs text-[#9CA3AF] leading-relaxed">
                    {step.desc}
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Secondary Highlight Card: Instant Dealer Quote */}
          <div className="lg:col-span-5 zar-panel-interactive p-6 sm:p-7 flex flex-col justify-between gap-6">
            <div className="space-y-3">
              <div className="flex items-center justify-between gap-3">
                <div className="flex h-11 w-11 items-center justify-center rounded-[12px] bg-[#2B2416] border border-[#82611C] text-[#C9A45A]">
                  <Clock className="h-5 w-5" />
                </div>
                <span className="rounded-full bg-[#162A24] border border-[#286B63] px-3 py-1 text-xs font-semibold text-[#48B884]">
                  فعال
                </span>
              </div>

              <h3 className="text-xl font-bold text-[#F2EEE5]">
                اعلام مظنه لحظه‌ای بدون اختلاف تلفنی
              </h3>
              <p className="text-[15px] text-[#F2EEE5]/75 leading-[1.85]">
                مدیر طلافروشی نرخ خرید و فروش را با کنترل بازه معقولیت منتشر
                می‌کند و مشتری مستقیماً روی مظنه معتبر و زمان‌دار معامله را
                ثبت می‌کند.
              </p>
            </div>

            <div className="rounded-[12px] bg-[#17191D] border border-white/[0.07] p-4 space-y-2 text-sm">
              <div className="flex items-center justify-between text-xs text-[#9CA3AF]">
                <span>ساختار درآمدی شفاف</span>
                <span className="font-bold text-[#C9A45A]">اسپرد روشن</span>
              </div>
              <p className="text-xs sm:text-sm text-[#F2EEE5]/80 leading-relaxed">
                کارمزد جداگانه سامانه <strong className="text-[#48B884]">۰.۰۰٪</strong>{" "}
                است و مابه‌التفاوت خرید و فروش پیش از تایید نهایی برای مشتری
                شفاف نمایش داده می‌شود.
              </p>
            </div>
          </div>
        </div>

        {/* Bottom Balanced Pair (6 + 6) */}
        <div className="mt-5 grid grid-cols-1 md:grid-cols-2 gap-5">
          <div className="zar-panel-interactive p-6 sm:p-7 flex flex-col justify-between gap-5">
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex h-10 w-10 items-center justify-center rounded-[10px] bg-[#252A32] border border-white/[0.1] text-[#F2EEE5]">
                  <BookOpenCheck className="h-5 w-5" />
                </div>
                <span className="text-xs font-medium text-[#4CB3A5]">
                  تاریخ شمسی (+۰۳:۳۰)
                </span>
              </div>
              <h3 className="text-lg font-bold text-[#F2EEE5]">
                تاریخچه ممیزی و پورتال کیف پول وب
              </h3>
              <p className="text-sm sm:text-[15px] text-[#F2EEE5]/75 leading-[1.85]">
                هر معامله، اعتبار و تسویه با شناسه یکتا در دفترکل ثبت می‌شود.
                مشتری و مدیر در هر لحظه می‌توانند مانده طلایی، ریالی و سابقه
                تراکنش‌ها را در ربات و پورتال وب بررسی کنند.
              </p>
            </div>
          </div>

          <div className="zar-panel-interactive p-6 sm:p-7 flex flex-col justify-between gap-5">
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex h-10 w-10 items-center justify-center rounded-[10px] bg-[#2B2416] border border-[#82611C] text-[#C9A45A]">
                  <Building2 className="h-5 w-5" />
                </div>
                <span className="rounded-full bg-[#192535] border border-[#2D4D73] px-3 py-0.5 text-xs font-semibold text-[#6BA4E8]">
                  آماده پایلوت
                </span>
              </div>
              <h3 className="text-lg font-bold text-[#F2EEE5]">
                معماری اختصاصی و حفظ مالکیت مشتری طلافروش
              </h3>
              <p className="text-sm sm:text-[15px] text-[#F2EEE5]/75 leading-[1.85]">
                مشتری با کد معرف و تایید مستقیم طلافروشی وارد می‌شود؛ کنترل کامل
                سقف اعتبار، نرخ مظنه و شرایط تحویل فیزیکی تماماً در اختیار
                طلافروشی باقی می‌ماند.
              </p>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
