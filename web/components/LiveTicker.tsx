"use client";

import React from "react";
import Link from "next/link";
import { ShieldCheck } from "lucide-react";
import { brandConfig } from "@/config/brand.config";
import { ZarorbitSymbolSvg } from "@/components/BrandLogo";

export default function LiveTicker() {
  const statusItems = [
    {
      label: "کانال معاملاتی فعال",
      value: `@${brandConfig.telegram.botUsername}`,
      tone: "gold" as const,
      ltr: true,
    },
    {
      label: "کارمزد معاملاتی",
      value: "۰.۰۰٪ (درآمد از اسپرد شفاف)",
      tone: "teal" as const,
      ltr: false,
    },
    {
      label: "تسویه و اعتبار",
      value: "دفترکل دقیق بازارگردان (Dealer Book)",
      tone: "neutral" as const,
      ltr: false,
    },
    {
      label: "چشم‌انداز آتی",
      value: "زیست‌بوم دارایی‌های واقعی و دیجیتال",
      tone: "neutral" as const,
      ltr: false,
      href: "/vision",
    },
  ];

  return (
    <div className="w-full bg-[#101215] border-b border-[#2B303A] py-1.5 px-4 text-xs overflow-hidden select-none">
      <div className="max-w-7xl mx-auto flex items-center justify-between gap-4">
        {/* Platform Identifier */}
        <div className="flex items-center gap-2 shrink-0">
          <ZarorbitSymbolSvg sizePx={16} />
          <span className="bidi-isolate text-[11px] font-bold text-[#C9A45A] tracking-wider hidden sm:inline">
            {brandConfig.name.en}
          </span>
          <span className="inline-flex items-center rounded-full bg-[#162A24] border border-[#286B63] px-2 py-0.5 text-[10px] font-semibold text-[#48B884]">
            پایلوت طلا
          </span>
        </div>

        {/* Truthful Operational Status Items (no hardcoded fake prices) */}
        <div className="flex items-center gap-6 overflow-x-auto no-scrollbar scroll-smooth text-[11px] text-[#9CA3AF]">
          {statusItems.map((item, idx) => (
            <div
              key={idx}
              className="flex items-center gap-1.5 shrink-0 whitespace-nowrap"
            >
              <span>{item.label}:</span>
              {item.href ? (
                <Link
                  href={item.href}
                  className="font-semibold text-[#C9A45A] hover:underline"
                >
                  {item.value}
                </Link>
              ) : (
                <span
                  className={`font-semibold ${item.ltr ? "bidi-isolate" : ""} ${
                    item.tone === "gold"
                      ? "text-[#C9A45A]"
                      : item.tone === "teal"
                      ? "text-[#4CB3A5]"
                      : "text-[#F2EEE5]"
                  }`}
                >
                  {item.value}
                </span>
              )}
            </div>
          ))}
        </div>

        {/* Right Transparency Note */}
        <div className="hidden lg:flex items-center gap-1.5 text-[11px] text-[#9CA3AF] shrink-0">
          <ShieldCheck className="h-3.5 w-3.5 text-[#4CB3A5]" />
          <span>مظنه زنده از طریق طلافروشی در ربات</span>
        </div>
      </div>
    </div>
  );
}
