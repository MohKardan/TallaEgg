"use client";

import React from "react";
import { ShieldCheck } from "lucide-react";
import { brandConfig } from "@/config/brand.config";
import { ZarorbitSymbolSvg } from "@/components/BrandLogo";

export default function LiveTicker() {
  const tickerItems = [
    {
      label: "نقطه شروع RWA (طلای ۱۸ عیار)",
      value: "۴,۳۵۰,۰۰۰ تومان",
      status: "نمونه مرجع",
      tone: "gold" as const,
    },
    {
      label: "دارایی‌های دیجیتال (Crypto & NFT)",
      value: "در حال توسعه",
      tone: "warning" as const,
    },
    {
      label: "مدل کارمزد ماژول آزمایشی",
      value: "۰.۰۰٪ (اسپرد شفاف)",
      tone: "teal" as const,
    },
    {
      label: "مسیر حکمرانی",
      value: "جامعه‌محور / Progressive DAO",
      tone: "neutral" as const,
    },
  ];

  return (
    <div className="w-full bg-[#101215] border-b border-[#2B303A] py-1.5 px-4 text-xs overflow-hidden select-none">
      <div className="max-w-7xl mx-auto flex items-center justify-between gap-4">
        {/* Left Ecosystem Identifier */}
        <div className="flex items-center gap-2 shrink-0">
          <ZarorbitSymbolSvg sizePx={16} />
          <span className="bidi-isolate text-[11px] font-bold text-[#C9A45A] tracking-wider hidden sm:inline">
            {brandConfig.name.en} ECOSYSTEM
          </span>
          <span className="inline-flex items-center rounded-full bg-[#192535] border border-[#2D4D73] px-2 py-0.5 text-[10px] font-semibold text-[#6BA4E8]">
            Beta Focus
          </span>
        </div>

        {/* Ticker Items */}
        <div className="flex items-center gap-6 overflow-x-auto no-scrollbar scroll-smooth text-[11px] text-[#9CA3AF]">
          {tickerItems.map((item, idx) => (
            <div
              key={idx}
              className="flex items-center gap-1.5 shrink-0 whitespace-nowrap"
            >
              <span>{item.label}:</span>
              <span
                className={`font-semibold tabular-nums ${
                  item.tone === "gold"
                    ? "text-[#C9A45A]"
                    : item.tone === "teal"
                    ? "text-[#4CB3A5]"
                    : item.tone === "warning"
                    ? "text-[#E2A93B]"
                    : "text-[#F2EEE5]"
                }`}
              >
                {item.value}
              </span>
              {item.status && (
                <span className="text-[10px] text-[#9CA3AF]">
                  ({item.status})
                </span>
              )}
            </div>
          ))}
        </div>

        {/* Right Transparency Note */}
        <div className="hidden lg:flex items-center gap-1.5 text-[11px] text-[#9CA3AF] shrink-0">
          <ShieldCheck className="h-3.5 w-3.5 text-[#4CB3A5]" />
          <span>تفکیک صریح وضعیت فعلی و نقشه راه</span>
        </div>
      </div>
    </div>
  );
}
