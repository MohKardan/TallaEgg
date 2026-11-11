import React from "react";
import Link from "next/link";
import Image from "next/image";
import { brandConfig } from "@/config/brand.config";

interface BrandLogoProps {
  variant?: "full" | "icon" | "minimal" | "showcase";
  size?: "sm" | "md" | "lg" | "xl";
  className?: string;
  withLink?: boolean;
  useLargeLogo?: boolean;
}

/**
 * BrandEmblem:
 * - Small / Icon surfaces (sizePx < 80 or default): uses the brushed gold 'Z' without outer ring (zarorbit-icon.jpg)
 * - Large / Showcase surfaces (useLargeLogo or sizePx >= 80): uses the circular gold 'Z' medallion logo (zarorbit-logo.jpg)
 */
export const BrandEmblem: React.FC<{
  sizePx?: number;
  className?: string;
  useLargeLogo?: boolean;
  priority?: boolean;
}> = ({ sizePx = 42, className = "", useLargeLogo, priority = false }) => {
  const isLarge = useLargeLogo ?? sizePx >= 80;
  const imageSrc = isLarge
    ? brandConfig.assets.logoLargeOrbit
    : brandConfig.assets.iconNoOrbit;

  return (
    <div
      style={{ width: sizePx, height: sizePx }}
      className={`relative ${
        isLarge ? "rounded-full" : "rounded-xl"
      } overflow-hidden shadow-goldGlowSubtle border border-amber-500/35 flex items-center justify-center shrink-0 bg-[#14171c] transition-transform duration-300 group-hover:scale-105 ${className}`}
    >
      <Image
        src={imageSrc}
        alt={isLarge ? "Zarorbit Official Logo" : "Zarorbit Icon Emblem"}
        width={sizePx}
        height={sizePx}
        className="object-cover w-full h-full scale-[1.06]"
        priority={priority}
      />
    </div>
  );
};

/**
 * BrandHeroShowcase:
 * High-impact luxury obsidian card presentation for the official Zarorbit circular Z logo.
 */
export const BrandHeroShowcase: React.FC<{ className?: string }> = ({
  className = "",
}) => {
  return (
    <div
      className={`relative rounded-3xl p-6 sm:p-8 bg-gradient-to-b from-[#131824]/90 via-[#0c1018]/90 to-[#07090e]/95 border border-amber-500/30 backdrop-blur-2xl shadow-goldGlow overflow-hidden group ${className}`}
    >
      {/* Background ambient radial lights */}
      <div className="absolute -top-24 -right-24 w-56 h-56 bg-amber-500/20 rounded-full blur-3xl pointer-events-none group-hover:bg-amber-500/30 transition-all duration-700" />
      <div className="absolute -bottom-24 -left-24 w-56 h-56 bg-amber-600/15 rounded-full blur-3xl pointer-events-none" />

      {/* Main Official Logo Medallion Container */}
      <div className="relative mx-auto w-56 h-56 sm:w-64 sm:h-64 flex items-center justify-center">
        {/* Outer rotating dashed tech halo */}
        <div
          className="absolute inset-0 rounded-full border border-dashed border-amber-500/25 animate-[spin_28s_linear_infinite]"
          aria-hidden="true"
        />
        <div className="relative w-48 h-48 sm:w-56 sm:h-56 rounded-full overflow-hidden border-2 border-amber-500/45 shadow-2xl bg-[#181b20]">
          <Image
            src={brandConfig.assets.logoLargeOrbit}
            alt="Zarorbit Physical & Digital Gold Official Logo"
            fill
            sizes="(max-width: 640px) 192px, 224px"
            className="object-cover w-full h-full scale-[1.08] transition-transform duration-700 group-hover:scale-[1.12]"
            priority
          />
          <div className="absolute inset-0 bg-gradient-to-tr from-transparent via-amber-400/5 to-white/10 pointer-events-none" />
        </div>
      </div>

      {/* Brand Wordmark inside showcase */}
      <div className="mt-5 text-center space-y-1">
        <div className="text-lg sm:text-xl font-black tracking-[0.28em] gold-gradient-text uppercase">
          {brandConfig.name.en}
        </div>
        <div className="text-[11px] font-medium tracking-[0.18em] text-amber-400/80 uppercase">
          {brandConfig.slogan.en}
        </div>
      </div>

      {/* Floating Meta Pills */}
      <div className="mt-5 flex flex-wrap items-center justify-between gap-3 text-xs border-t border-amber-500/20 pt-4">
        <div className="flex items-center gap-2">
          <span className="h-2 w-2 rounded-full bg-emerald-400 animate-pulse" />
          <span className="text-slate-300 font-medium">شبکه فعال بازارساز</span>
        </div>
        <div className="text-amber-400/90 font-mono tracking-wider font-semibold">
          999.9 PURE GOLD
        </div>
      </div>
    </div>
  );
};

export default function BrandLogo({
  variant = "full",
  size = "md",
  className = "",
  withLink = true,
  useLargeLogo = false,
}: BrandLogoProps) {
  const sizeMap = {
    sm: { px: 36, title: "text-base", sub: "text-[10px]" },
    md: { px: 44, title: "text-xl", sub: "text-[11px]" },
    lg: { px: 56, title: "text-2xl", sub: "text-xs" },
    xl: { px: 76, title: "text-3xl", sub: "text-sm" },
  };

  const { px, title, sub } = sizeMap[size];

  const content = (
    <div className={`flex items-center gap-3 group cursor-pointer ${className}`}>
      <BrandEmblem sizePx={px} useLargeLogo={useLargeLogo} />

      {variant !== "icon" && (
        <div className="flex flex-col select-none">
          <div className="flex items-center gap-2">
            <span
              className={`${title} font-extrabold tracking-tight text-white group-hover:text-amber-400 transition-colors`}
            >
              {brandConfig.name.fa}
            </span>
            <span className="text-[11px] font-bold tracking-wider text-amber-400/90 uppercase border-r border-amber-500/30 pr-2 mr-0.5">
              {brandConfig.name.en}
            </span>
          </div>
          {variant === "full" && (
            <span
              className={`${sub} text-slate-400 font-light -mt-0.5 group-hover:text-amber-300/80 transition-colors tracking-wide`}
            >
              {brandConfig.slogan.en}
            </span>
          )}
        </div>
      )}
    </div>
  );

  if (withLink) {
    return <Link href="/">{content}</Link>;
  }

  return content;
}
