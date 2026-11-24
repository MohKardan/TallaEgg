"use client";

import React, { useState } from "react";
import { Calculator, Check, Copy } from "lucide-react";
import {
  MESGHAL_TO_18K_GRAM,
  convertGoldWeight,
  formatIntegerInput,
  type GoldUnit,
} from "@/lib/gold-math";

const WEIGHT_PRESETS: Array<{ label: string; amount: string; unit: GoldUnit }> = [
  { label: "۱ مثقال", amount: "1", unit: "mesghal" },
  { label: "۵ گرم", amount: "5", unit: "gram" },
  { label: "۱۰ گرم", amount: "10", unit: "gram" },
  { label: "۵۰ گرم", amount: "50", unit: "gram" },
];

export default function GoldCalculator() {
  const [unit, setUnit] = useState<GoldUnit>("gram");
  const [amountInput, setAmountInput] = useState<string>("10");
  const [mesghalPriceInput, setMesghalPriceInput] = useState<string>("");
  const [copied, setCopied] = useState(false);

  const result = convertGoldWeight(amountInput, unit, mesghalPriceInput);

  const handlePriceChange = (raw: string) => {
    setMesghalPriceInput(formatIntegerInput(raw));
  };

  const handleCopySummary = async () => {
    const gramText = result.grams18k.toLocaleString("fa-IR", {
      maximumFractionDigits: 3,
    });
    const mesghalText = result.mesghals.toLocaleString("fa-IR", {
      maximumFractionDigits: 4,
    });
    const summary =
      result.estimatedTotalToman > 0
        ? `محاسبه طلای ۱۸ عیار ZARORBIT: ${gramText} گرم (${mesghalText} مثقال) ≈ ${result.estimatedTotalToman.toLocaleString("fa-IR")} تومان (کارمزد ۰٪)`
        : `محاسبه وزن طلای ۱۸ عیار ZARORBIT: ${gramText} گرم = ${mesghalText} مثقال آبشده`;

    try {
      await navigator.clipboard.writeText(summary);
      setCopied(true);
      setTimeout(() => setCopied(false), 1800);
    } catch {
      // Clipboard API unavailable in restricted context
    }
  };

  return (
    <section id="calculator" className="py-20 sm:py-24">
      <div className="mx-auto max-w-[1200px] px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-start">
          {/* Right Explanation Column */}
          <div className="lg:col-span-5 space-y-5">
            <div className="inline-flex items-center gap-2 text-xs font-bold text-[#4CB3A5] bidi-isolate uppercase tracking-[0.1em]">
              <Calculator className="h-4 w-4" />
              <span>MELTED GOLD CONVERSION TOOL</span>
            </div>
            <h2 className="text-2xl sm:text-[32px] font-extrabold text-[#F2EEE5] leading-[1.35]">
              محاسبه‌گر تبدیل مثقال و گرم (طلای ۱۸ عیار)
            </h2>
            <p className="text-[15px] text-[#F2EEE5]/75 leading-[1.85]">
              برای جلوگیری از نمایش نرخ‌های غیرواقعی یا منقضی، هیچ قیمت ثابتی
              در وب‌سایت درج نمی‌شود. مظنه زنده هر مثقال را از ربات یا طلافروشی
              خود وارد کنید تا معادل گرم ۱۸ عیار و ارزش کل بلافاصله محاسبه شود.
            </p>

            <div className="rounded-[14px] bg-[#1F2329] border border-white/[0.08] divide-y divide-white/[0.06] text-sm">
              <div className="flex items-center justify-between px-4 py-3">
                <span className="text-[#9CA3AF]">
                  ضریب تبدیل مثقال ۱۷ به گرم ۱۸ عیار
                </span>
                <span className="bidi-isolate font-num font-bold text-[#C9A45A]">
                  {MESGHAL_TO_18K_GRAM} g
                </span>
              </div>
              <div className="flex items-center justify-between px-4 py-3">
                <span className="text-[#9CA3AF]">کارمزد تراکنش در سامانه</span>
                <span className="font-semibold text-[#48B884] tabular-nums">
                  ۰.۰۰٪ (بدون کارمزد جداگانه)
                </span>
              </div>
            </div>
          </div>

          {/* Left Interactive Panel */}
          <div className="lg:col-span-7 zar-panel p-6 sm:p-8 space-y-6">
            {/* Quick Preset Chips */}
            <div className="flex flex-wrap items-center justify-between gap-3">
              <span className="text-xs font-semibold text-[#9CA3AF]">
                انتخاب سریع مقدار:
              </span>
              <div className="flex flex-wrap items-center gap-2">
                {WEIGHT_PRESETS.map((preset) => {
                  const active =
                    unit === preset.unit && amountInput === preset.amount;
                  return (
                    <button
                      key={preset.label}
                      type="button"
                      onClick={() => {
                        setUnit(preset.unit);
                        setAmountInput(preset.amount);
                      }}
                      className={`rounded-full px-3 py-1.5 text-xs font-semibold border transition-colors ${
                        active
                          ? "bg-[#C9A45A]/15 border-[#C9A45A] text-[#C9A45A]"
                          : "bg-[#17191D] border-white/[0.08] text-[#F2EEE5]/75 hover:border-white/[0.2]"
                      }`}
                    >
                      {preset.label}
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Inputs */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <label
                  htmlFor="gold-amount-input"
                  className="block text-sm font-semibold text-[#F2EEE5]"
                >
                  مقدار طلا
                </label>
                <div className="flex rounded-[10px] overflow-hidden border border-[#525B6B] bg-[#17191D] focus-within:border-[#4CB3A5]">
                  <input
                    id="gold-amount-input"
                    type="text"
                    inputMode="decimal"
                    value={amountInput}
                    onChange={(e) => setAmountInput(e.target.value)}
                    className="w-full bg-transparent px-3.5 py-3 text-base font-semibold text-[#F2EEE5] tabular-nums focus:outline-none"
                    placeholder="مثلاً ۱۰"
                  />
                  <div
                    role="radiogroup"
                    aria-label="واحد وزن طلا"
                    className="flex border-r border-white/[0.08] shrink-0"
                  >
                    <button
                      type="button"
                      role="radio"
                      aria-checked={unit === "gram"}
                      onClick={() => setUnit("gram")}
                      className={`px-3.5 text-xs font-bold transition-colors ${
                        unit === "gram"
                          ? "bg-[#C9A45A] text-[#17191D]"
                          : "text-[#9CA3AF] hover:text-[#F2EEE5]"
                      }`}
                    >
                      گرم
                    </button>
                    <button
                      type="button"
                      role="radio"
                      aria-checked={unit === "mesghal"}
                      onClick={() => setUnit("mesghal")}
                      className={`px-3.5 text-xs font-bold transition-colors ${
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

              <div className="space-y-2">
                <label
                  htmlFor="mesghal-rate-input"
                  className="block text-sm font-semibold text-[#F2EEE5]"
                >
                  مظنه یک مثقال (تومان — اختیاری)
                </label>
                <input
                  id="mesghal-rate-input"
                  type="text"
                  inputMode="numeric"
                  dir="ltr"
                  value={mesghalPriceInput}
                  onChange={(e) => handlePriceChange(e.target.value)}
                  className="w-full rounded-[10px] border border-[#525B6B] bg-[#17191D] px-3.5 py-3 text-base font-num font-semibold text-[#F2EEE5] text-right focus:outline-none focus:border-[#4CB3A5]"
                  placeholder="مظنه فعلی بازار را وارد کنید"
                />
              </div>
            </div>

            {/* Live Accessible Output Grid */}
            <div
              aria-live="polite"
              className="grid grid-cols-1 sm:grid-cols-3 gap-3.5"
            >
              <div className="rounded-[12px] bg-[#17191D] border border-white/[0.07] p-4">
                <div className="text-xs text-[#9CA3AF] mb-1.5">
                  معادل گرم (۱۸ عیار)
                </div>
                <div className="text-xl font-extrabold text-[#F2EEE5] tabular-nums">
                  {result.grams18k.toLocaleString("fa-IR", {
                    maximumFractionDigits: 3,
                  })}{" "}
                  <span className="text-xs font-normal text-[#9CA3AF]">
                    گرم
                  </span>
                </div>
              </div>

              <div className="rounded-[12px] bg-[#17191D] border border-white/[0.07] p-4">
                <div className="text-xs text-[#9CA3AF] mb-1.5">
                  معادل مثقال آبشده
                </div>
                <div className="text-xl font-extrabold text-[#4CB3A5] tabular-nums">
                  {result.mesghals.toLocaleString("fa-IR", {
                    maximumFractionDigits: 4,
                  })}{" "}
                  <span className="text-xs font-normal text-[#9CA3AF]">
                    مثقال
                  </span>
                </div>
              </div>

              <div className="rounded-[12px] bg-[#17191D] border border-[#C9A45A]/35 p-4">
                <div className="text-xs text-[#9CA3AF] mb-1.5">
                  برآورد ارزش کل
                </div>
                <div className="text-xl font-extrabold text-[#C9A45A] tabular-nums">
                  {result.estimatedTotalToman > 0 ? (
                    <>
                      {result.estimatedTotalToman.toLocaleString("fa-IR")}{" "}
                      <span className="text-xs font-normal text-[#9CA3AF]">
                        تومان
                      </span>
                    </>
                  ) : (
                    <span className="text-sm font-normal text-[#9CA3AF]">
                      مظنه را وارد کنید
                    </span>
                  )}
                </div>
              </div>
            </div>

            {/* Bottom Rate Breakdown & Copy Action */}
            <div className="flex flex-wrap items-center justify-between gap-3 border-t border-white/[0.08] pt-4 text-xs sm:text-sm">
              <div className="text-[#9CA3AF]">
                {result.gramRateToman > 0 ? (
                  <span>
                    نرخ هر گرم ۱۸ عیار:{" "}
                    <strong className="text-[#F2EEE5] tabular-nums">
                      {Math.round(result.gramRateToman).toLocaleString("fa-IR")}{" "}
                      تومان
                    </strong>
                  </span>
                ) : (
                  <span>
                    نرخ را شما وارد می‌کنید؛ محاسبه صرفاً برآورد تبدیل است.
                  </span>
                )}
              </div>

              <button
                type="button"
                onClick={handleCopySummary}
                className="inline-flex items-center gap-1.5 rounded-[10px] border border-white/[0.1] bg-[#17191D] px-3.5 py-2 text-xs font-semibold text-[#F2EEE5] hover:border-[#C9A45A]/60 hover:text-[#C9A45A] transition-colors"
              >
                {copied ? (
                  <>
                    <Check className="h-3.5 w-3.5 text-[#48B884]" />
                    <span className="text-[#48B884]">کپی شد</span>
                  </>
                ) : (
                  <>
                    <Copy className="h-3.5 w-3.5" />
                    <span>کپی خلاصه محاسبه</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
