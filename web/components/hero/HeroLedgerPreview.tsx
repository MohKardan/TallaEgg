"use client";

import React, { useState } from "react";
import Link from "next/link";
import {
  ArrowDownLeft,
  ArrowUpRight,
  BookOpenCheck,
  ChevronDown,
  ChevronLeft,
  ShieldCheck,
  Wallet,
} from "lucide-react";
import { ZarorbitSymbolSvg } from "@/components/BrandLogo";

interface SampleLedgerRow {
  id: string;
  type: "buy" | "sell";
  typeFa: string;
  weightLabel: string;
  mesghalLabel: string;
  dateFa: string;
  statusFa: string;
  counterpartyFa: string;
  commissionFa: string;
}

// Explicitly illustrative sample data (zero fake market prices, strictly labeled as sample UI)
const SAMPLE_ROWS: SampleLedgerRow[] = [
  {
    id: "TRD-1405-0841",
    type: "buy",
    typeFa: "خرید آبشده ۱۸ عیار",
    weightLabel: "+۱۲٫۵۰۰ گرم",
    mesghalLabel: "۲٫۸۸۵۶ مثقال",
    dateFa: "امروز • ۱۴:۱۲",
    statusFa: "ثبت‌شده در دفترکل",
    counterpartyFa: "دفترکل بازارگردان طلافروشی تاییدشده",
    commissionFa: "۰.۰۰٪ (اسپرد شفاف)",
  },
  {
    id: "TRD-1405-0837",
    type: "sell",
    typeFa: "فروش آبشده ۱۸ عیار",
    weightLabel: "−۴٫۳۳۲ گرم",
    mesghalLabel: "۱٫۰۰۰۰ مثقال",
    dateFa: "دیروز • ۱۷:۴۰",
    statusFa: "ثبت‌شده در دفترکل",
    counterpartyFa: "دفترکل بازارگردان طلافروشی تاییدشده",
    commissionFa: "۰.۰۰٪ (اسپرد شفاف)",
  },
  {
    id: "TRD-1405-0829",
    type: "buy",
    typeFa: "خرید آبشده ۱۸ عیار",
    weightLabel: "+۸٫۶۶۴ گرم",
    mesghalLabel: "۲٫۰۰۰۰ مثقال",
    dateFa: "۳ روز پیش • ۱۱:۰۵",
    statusFa: "تسویه‌شده",
    counterpartyFa: "دفترکل بازارگردان طلافروشی تاییدشده",
    commissionFa: "۰.۰۰٪ (اسپرد شفاف)",
  },
];

export default function HeroLedgerPreview() {
  const [activeTab, setActiveTab] = useState<"balances" | "history">("balances");
  const [expandedId, setExpandedId] = useState<string | null>(SAMPLE_ROWS[0].id);

  return (
    <section
      className="zar-panel relative overflow-hidden p-5 sm:p-6"
      aria-label="پیش‌نمایش نمونه دفترکل و کیف پول ZARORBIT"
    >
      {/* Subtle architectural hairline grid */}
      <div
        className="pointer-events-none absolute inset-0 zar-grid-backdrop opacity-35"
        aria-hidden="true"
      />

      {/* Panel Header with Explicit Honesty Watermark */}
      <div className="relative z-10 flex items-center justify-between gap-3 border-b border-white/[0.08] pb-4">
        <div className="flex items-center gap-2.5">
          <div className="flex h-9 w-9 items-center justify-center rounded-[10px] bg-[#17191D] border border-white/[0.08]">
            <ZarorbitSymbolSvg sizePx={22} />
          </div>
          <div>
            <div className="text-sm font-bold text-[#F2EEE5]">
              دفترکل معاملاتی و کیف پول
            </div>
            <div className="text-xs text-[#9CA3AF]">
              ساختار واقعی پنل مشتری و طلافروشی
            </div>
          </div>
        </div>
        <span className="inline-flex items-center gap-1.5 rounded-full border border-[#C9A45A]/40 bg-[#2B2416] px-2.5 py-1 text-[11px] font-semibold text-[#C9A45A] shrink-0">
          نمای نمونه
        </span>
      </div>

      {/* Interactive View Switcher (Toggle Buttons with aria-pressed + non-color indicator) */}
      <div
        role="group"
        aria-label="بخش‌های پیش‌نمایش دفترکل"
        className="relative z-10 mt-4 grid grid-cols-2 gap-1.5 rounded-[12px] bg-[#17191D] p-1 border border-white/[0.07]"
      >
        <button
          type="button"
          aria-pressed={activeTab === "balances"}
          onClick={() => setActiveTab("balances")}
          className={`flex items-center justify-center gap-2 rounded-[9px] py-2 text-xs sm:text-sm font-semibold transition-colors border ${
            activeTab === "balances"
              ? "bg-[#252A32] border-[#C9A45A]/50 text-[#F2EEE5] shadow-sm"
              : "border-transparent text-[#9CA3AF] hover:text-[#F2EEE5]"
          }`}
        >
          <Wallet className="h-4 w-4 text-[#C9A45A]" aria-hidden="true" />
          <span>مانده و اعتبار</span>
        </button>
        <button
          type="button"
          aria-pressed={activeTab === "history"}
          onClick={() => setActiveTab("history")}
          className={`flex items-center justify-center gap-2 rounded-[9px] py-2 text-xs sm:text-sm font-semibold transition-colors border ${
            activeTab === "history"
              ? "bg-[#252A32] border-[#4CB3A5]/50 text-[#F2EEE5] shadow-sm"
              : "border-transparent text-[#9CA3AF] hover:text-[#F2EEE5]"
          }`}
        >
          <BookOpenCheck className="h-4 w-4 text-[#4CB3A5]" aria-hidden="true" />
          <span>تاریخچه دفترکل</span>
        </button>
      </div>

      {/* Tab Panel Content */}
      <div className="relative z-10 mt-4 min-h-[264px]">
        {activeTab === "balances" ? (
          <div className="space-y-3">
            {/* Primary Gold Position Card */}
            <div className="rounded-[12px] bg-[#17191D] border border-[#C9A45A]/25 p-4">
              <div className="flex items-center justify-between text-xs text-[#9CA3AF]">
                <span>مانده طلای آبشده (۱۸ عیار • نمونه)</span>
                <span className="bidi-isolate font-num text-[#C9A45A] font-semibold">
                  MAUA / 18K
                </span>
              </div>
              <div className="mt-2 flex items-baseline justify-between gap-2">
                <div className="text-2xl font-extrabold text-[#F2EEE5] tabular-nums">
                  ۱۶٫۸۳۲{" "}
                  <span className="text-sm font-medium text-[#9CA3AF]">گرم</span>
                </div>
                <div className="text-sm font-semibold text-[#4CB3A5] tabular-nums">
                  ≈ ۳٫۸۸۵۷ مثقال
                </div>
              </div>
            </div>

            {/* Secondary Operational Ledger Rows */}
            <div className="divide-y divide-white/[0.06] rounded-[12px] bg-[#17191D] border border-white/[0.07]">
              <div className="flex items-center justify-between px-4 py-3 text-sm">
                <span className="text-[#9CA3AF]">اعتبار متقابل بین‌دارایی</span>
                <span className="font-semibold text-[#F2EEE5]">
                  تعیین‌شده توسط طلافروشی
                </span>
              </div>
              <div className="flex items-center justify-between px-4 py-3 text-sm">
                <span className="text-[#9CA3AF]">کارمزد معاملاتی سامانه</span>
                <span className="font-bold text-[#48B884] tabular-nums">
                  ۰.۰۰٪ (اسپرد شفاف)
                </span>
              </div>
              <div className="flex items-center justify-between px-4 py-3 text-sm">
                <span className="text-[#9CA3AF]">مظنه زنده خرید و فروش</span>
                <span className="font-medium text-[#C9A45A]">
                  مستقیم در ربات تلگرام
                </span>
              </div>
            </div>
          </div>
        ) : (
          <div className="space-y-2">
            {SAMPLE_ROWS.map((row) => {
              const isOpen = expandedId === row.id;
              const isBuy = row.type === "buy";
              return (
                <div
                  key={row.id}
                  className={`rounded-[12px] border transition-colors ${
                    isOpen
                      ? "bg-[#17191D] border-[#C9A45A]/35"
                      : "bg-[#17191D]/80 border-white/[0.07] hover:border-white/[0.15]"
                  }`}
                >
                  <button
                    type="button"
                    onClick={() => setExpandedId(isOpen ? null : row.id)}
                    aria-expanded={isOpen}
                    aria-controls={`ledger-row-${row.id}`}
                    className="w-full flex items-center justify-between gap-3 px-3.5 py-3 text-right"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      <span
                        className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] border ${
                          isBuy
                            ? "bg-[#162A24] border-[#286B63] text-[#4CB3A5]"
                            : "bg-[#2B2416] border-[#82611C] text-[#C9A45A]"
                        }`}
                      >
                        {isBuy ? (
                          <ArrowDownLeft className="h-4 w-4" aria-hidden="true" />
                        ) : (
                          <ArrowUpRight className="h-4 w-4" aria-hidden="true" />
                        )}
                      </span>
                      <div className="min-w-0">
                        <div className="text-sm font-semibold text-[#F2EEE5] truncate">
                          {row.typeFa}
                        </div>
                        <div className="text-xs text-[#9CA3AF]">
                          {row.dateFa}
                        </div>
                      </div>
                    </div>

                    <div className="flex items-center gap-2 shrink-0">
                      <div className="text-left">
                        <div
                          className={`text-sm font-bold tabular-nums ${
                            isBuy ? "text-[#4CB3A5]" : "text-[#C9A45A]"
                          }`}
                        >
                          {row.weightLabel}
                        </div>
                        <div className="text-[11px] text-[#9CA3AF] tabular-nums">
                          {row.mesghalLabel}
                        </div>
                      </div>
                      <ChevronDown
                        aria-hidden="true"
                        className={`h-4 w-4 text-[#9CA3AF] transition-transform ${
                          isOpen ? "rotate-180 text-[#C9A45A]" : ""
                        }`}
                      />
                    </div>
                  </button>

                  {isOpen && (
                    <div
                      id={`ledger-row-${row.id}`}
                      className="border-t border-white/[0.06] px-3.5 py-2.5 text-xs text-[#9CA3AF] space-y-1.5"
                    >
                      <div className="flex items-center justify-between">
                        <span>شناسه ممیزی:</span>
                        <span className="bidi-isolate font-num text-[#F2EEE5]">
                          {row.id}
                        </span>
                      </div>
                      <div className="flex items-center justify-between">
                        <span>کارمزد ثبت‌شده:</span>
                        <span className="text-[#48B884] font-medium">
                          {row.commissionFa}
                        </span>
                      </div>
                      <div className="flex items-center justify-between">
                        <span>طرف حساب:</span>
                        <span className="text-[#F2EEE5]">
                          {row.counterpartyFa}
                        </span>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Honest Disclaimer Footer + Direct Portal Entry */}
      <div className="relative z-10 mt-4 pt-3.5 border-t border-white/[0.08] flex flex-wrap items-center justify-between gap-3 text-xs">
        <span className="inline-flex items-center gap-1.5 text-[#F2EEE5]/80">
          <ShieldCheck className="h-4 w-4 text-[#48B884] shrink-0" aria-hidden="true" />
          <span>اعداد بالا صرفاً نمونه نمایشی هستند (بدون قیمت ساختگی)</span>
        </span>
        <Link
          href="/login"
          className="inline-flex items-center gap-1 font-semibold text-[#C9A45A] hover:text-[#D4B16A] transition-colors"
        >
          <span>مشاهده حساب واقعی</span>
          <ChevronLeft className="h-3.5 w-3.5" aria-hidden="true" />
        </Link>
      </div>
    </section>
  );
}
