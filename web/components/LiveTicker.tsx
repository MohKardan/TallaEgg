"use client";

import React from "react";
import { TrendingUp, ShieldCheck, Zap, Activity } from "lucide-react";
import { brandConfig } from "@/config/brand.config";
import Image from "next/image";

export default function LiveTicker() {
  const tickerItems = [
    {
      label: "آبشده ۱۸ عیار (MAUA)",
      value: "۴,۳۵۰,۰۰۰ تومان",
      change: "+۰.۳۵٪",
      isUp: true,
    },
    {
      label: "انس جهانی (XAU)",
      value: "$2,745.80",
      change: "+۰.۱۲٪",
      isUp: true,
    },
    {
      label: "سکه بهار آزادی",
      value: "۴۸,۲۰۰,۰۰۰ تومان",
      change: "+۰.۴۵٪",
      isUp: true,
    },
    {
      label: "کارمزد معاملات",
      value: "۰.۰۰٪ (رایگان)",
      highlight: true,
    },
    {
      label: "وضعیت تسویه دفترکل",
      value: "آنلاین و لحظه‌ای",
      online: true,
    },
  ];

  return (
    <div className="w-full bg-[#05070a] border-b border-amber-500/15 py-1.5 px-4 text-xs overflow-hidden select-none">
      <div className="max-w-7xl mx-auto flex items-center justify-between gap-4">
        {/* Left Indicator */}
        <div className="flex items-center gap-2 shrink-0">
          <div className="relative w-4 h-4 rounded-full overflow-hidden border border-amber-500/40">
            <Image
              src={brandConfig.assets.iconNoOrbit}
              alt="Live"
              width={16}
              height={16}
              className="object-cover"
            />
          </div>
          <span className="text-[11px] font-bold text-amber-400 uppercase tracking-wider hidden sm:inline">
            نرخ زنده {brandConfig.name.en}
          </span>
          <span className="flex h-2 w-2 relative">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
            <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
          </span>
        </div>

        {/* Ticker Items */}
        <div className="flex items-center gap-6 overflow-x-auto no-scrollbar scroll-smooth text-[11px] text-slate-300">
          {tickerItems.map((item, idx) => (
            <div key={idx} className="flex items-center gap-1.5 shrink-0 whitespace-nowrap">
              <span className="text-slate-400">{item.label}:</span>
              <span
                className={`font-semibold ${
                  item.highlight
                    ? "text-amber-400"
                    : item.online
                    ? "text-emerald-400"
                    : "text-white"
                }`}
              >
                {item.value}
              </span>
              {item.change && (
                <span className="text-emerald-400 text-[10px] font-mono flex items-center">
                  <TrendingUp className="h-2.5 w-2.5 inline mr-0.5" />
                  {item.change}
                </span>
              )}
            </div>
          ))}
        </div>

        {/* Right Help / Direct Link */}
        <div className="hidden lg:flex items-center gap-2 text-[11px] text-slate-400 shrink-0">
          <ShieldCheck className="h-3.5 w-3.5 text-amber-400" />
          <span>تضمین عیار ۷۵۰ با فاکتور و انگ رسمی</span>
        </div>
      </div>
    </div>
  );
}
