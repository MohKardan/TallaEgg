import React from "react";
import Link from "next/link";
import Image from "next/image";
import { brandConfig } from "@/config/brand.config";

interface BrandLogoProps {
  variant?: "full" | "icon" | "minimal";
  size?: "sm" | "md" | "lg";
  className?: string;
  withLink?: boolean;
}

/**
 * Geometric Vector Emblem for Zarorbit:
 * 1. Isometric Bullion 'Z' cut in 24K pure gold
 * 2. Orbital Binary Rings representing high-speed liquidity and blockchain/ledger precision
 * 3. Deep Obsidian (#080A0F) backdrop with metallic reflections
 */
export const BrandEmblem: React.FC<{ sizePx?: number; className?: string }> = ({
  sizePx = 44,
  className = "",
}) => {
  return (
    <div
      style={{ width: sizePx, height: sizePx }}
      className={`relative rounded-xl overflow-hidden shadow-goldGlowSubtle border border-amber-500/30 flex items-center justify-center shrink-0 bg-[#090C12] ${className}`}
    >
      <Image
        src="/images/zarorbit-logo.jpg"
        alt="Zarorbit Official Logo"
        width={sizePx}
        height={sizePx}
        className="object-cover w-full h-full"
        priority
      />
    </div>
  );
};

export default function BrandLogo({
  variant = "full",
  size = "md",
  className = "",
  withLink = true,
}: BrandLogoProps) {
  const sizeMap = {
    sm: { px: 36, title: "text-base", sub: "text-[10px]" },
    md: { px: 46, title: "text-xl", sub: "text-[11px]" },
    lg: { px: 58, title: "text-2xl", sub: "text-xs" },
  };

  const { px, title, sub } = sizeMap[size];

  const content = (
    <div className={`flex items-center gap-3 group cursor-pointer ${className}`}>
      <BrandEmblem sizePx={px} />

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
