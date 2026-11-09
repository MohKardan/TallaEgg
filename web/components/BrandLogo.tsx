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
 * - Small / Icon surfaces (sizePx < 80 or default): uses the Falcon with 'Z' coin and NO orbits (zarorbit-icon.jpg)
 * - Large / Showcase surfaces (useLargeLogo or sizePx >= 80): uses the full orbital binary rings logo (zarorbit-logo.jpg)
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
      className={`relative rounded-xl overflow-hidden shadow-goldGlowSubtle border border-amber-500/30 flex items-center justify-center shrink-0 bg-[#090C12] transition-transform duration-300 group-hover:scale-105 ${className}`}
    >
      <Image
        src={imageSrc}
        alt={isLarge ? "Zarorbit Flagship Orbital Logo" : "Zarorbit Icon Emblem"}
        width={sizePx}
        height={sizePx}
        className="object-cover w-full h-full"
        priority={priority}
      />
    </div>
  );
};

/**
 * BrandHeroShowcase:
 * High-impact luxury 3D card presentation for the large Zarorbit logo with orbital rings.
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

      {/* Main Large Orbital Logo Container */}
      <div className="relative mx-auto w-64 h-64 sm:w-72 sm:h-72 rounded-2xl overflow-hidden border border-amber-500/40 shadow-2xl bg-[#090c12]">
        <Image
          src={brandConfig.assets.logoLargeOrbit}
          alt="Zarorbit Physical & Digital Gold Official Emblem"
          fill
          sizes="(max-width: 640px) 256px, 288px"
          className="object-cover w-full h-full transition-transform duration-700 group-hover:scale-105"
          priority
        />
        {/* Subtle glass reflection overlay */}
        <div className="absolute inset-0 bg-gradient-to-tr from-transparent via-amber-400/5 to-white/10 pointer-events-none" />
      </div>

      {/* Floating Meta Pills */}
      <div className="mt-6 flex flex-wrap items-center justify-between gap-3 text-xs border-t border-amber-500/20 pt-4">
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
