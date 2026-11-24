import React from "react";
import Link from "next/link";
import { brandConfig } from "@/config/brand.config";

interface BrandLogoProps {
  variant?: "full" | "icon" | "minimal";
  size?: "sm" | "md" | "lg" | "xl";
  tone?: "dark" | "light";
  className?: string;
  withLink?: boolean;
}

// Single-source-of-truth S1 (Zero-Interrupt Orbital Z) pure closed paths (fill-rule="nonzero")
const S1_PRIMARY_PATH =
  "M 12 12 L 116 12 L 116 23.82 L 93.1 46.72 L 81.28 34.9 L 87.78 28.4 L 12 28.4 Z M 116 116 L 12 116 L 12 104.18 L 34.9 81.28 L 46.72 93.1 L 40.22 99.6 L 116 99.6 Z M 39.7 64 A 24.3 24.3 0 1 1 88.3 64 A 24.3 24.3 0 1 1 39.7 64 Z M 52.7 64 A 11.3 11.3 0 1 0 75.3 64 A 11.3 11.3 0 1 0 52.7 64 Z";

const S1_FAVICON_PATH =
  "M 10 10 L 118 10 L 118 22.72 L 94.26 46.46 L 81.54 33.74 L 87.28 28 L 10 28 Z M 118 118 L 10 118 L 10 105.28 L 33.74 81.54 L 46.46 94.26 L 40.72 100 L 118 100 Z M 38.8 64 A 25.2 25.2 0 1 1 89.2 64 A 25.2 25.2 0 1 1 38.8 64 Z M 53.4 64 A 10.6 10.6 0 1 0 74.6 64 A 10.6 10.6 0 1 0 53.4 64 Z";

// Genuine Manrope Bold (SIL OFL-1.1) wordmark outlines with optical kerning (must use fill-rule="nonzero")
const WORDMARK_MANROPE_PATH =
  "M33.92 66L0 66L0 64.99L21.69 28.91L1.53 28.91L1.53 22L34.44 22L34.44 23.04L12.86 59.06L33.92 59.06 M49.99 66L42.41 66L56.29 22L67.07 22L80.94 66L73.37 66L60.78 26.52L62.43 26.52L49.99 66M73.24 56.47L50.14 56.47L50.14 49.59L73.24 49.59 M97.61 66L90.24 66L90.24 22L108.42 22Q109.07 22 110.08 22.05Q111.08 22.09 111.94 22.24Q115.76 22.83 118.25 24.78Q120.74 26.74 121.95 29.72Q123.15 32.69 123.15 36.33Q123.15 41.71 120.43 45.57Q117.71 49.44 112.09 50.36L108.94 50.63L97.61 50.63L97.61 66M123.86 66L115.51 66L106.84 48.09L114.32 46.44L123.86 66M97.61 28.91L97.61 43.76L108.12 43.76Q108.73 43.76 109.49 43.69Q110.26 43.63 110.90 43.45Q112.73 42.99 113.79 41.83Q114.84 40.67 115.28 39.20Q115.73 37.74 115.73 36.33Q115.73 34.92 115.28 33.44Q114.84 31.96 113.79 30.80Q112.73 29.64 110.90 29.18Q110.26 29 109.49 28.95Q108.73 28.91 108.12 28.91 M152.89 66.92Q146.29 66.92 141.58 64.03Q136.88 61.14 134.35 55.98Q131.83 50.81 131.83 44Q131.83 37.19 134.35 32.02Q136.88 26.86 141.58 23.97Q146.29 21.08 152.89 21.08Q159.49 21.08 164.21 23.97Q168.93 26.86 171.45 32.02Q173.97 37.19 173.97 44Q173.97 50.81 171.45 55.98Q168.93 61.14 164.21 64.03Q159.49 66.92 152.89 66.92M152.89 59.98Q157.32 60.01 160.27 58.02Q163.21 56.04 164.70 52.43Q166.18 48.83 166.18 44Q166.18 39.17 164.70 35.61Q163.21 32.05 160.27 30.07Q157.32 28.08 152.89 28.02Q148.46 27.99 145.52 29.98Q142.59 31.96 141.11 35.57Q139.63 39.17 139.63 44Q139.63 48.83 141.09 52.39Q142.56 55.95 145.51 57.93Q148.46 59.92 152.89 59.98 M191.34 66L183.98 66L183.98 22L202.16 22Q202.80 22 203.81 22.05Q204.82 22.09 205.68 22.24Q209.49 22.83 211.98 24.78Q214.47 26.74 215.68 29.72Q216.89 32.69 216.89 36.33Q216.89 41.71 214.17 45.57Q211.45 49.44 205.83 50.36L202.68 50.63L191.34 50.63L191.34 66M217.59 66L209.25 66L200.57 48.09L208.06 46.44L217.59 66M191.34 28.91L191.34 43.76L201.86 43.76Q202.47 43.76 203.23 43.69Q203.99 43.63 204.64 43.45Q206.47 42.99 207.52 41.83Q208.58 40.67 209.02 39.20Q209.46 37.74 209.46 36.33Q209.46 34.92 209.02 33.44Q208.58 31.96 207.52 30.80Q206.47 29.64 204.64 29.18Q203.99 29 203.23 28.95Q202.47 28.91 201.86 28.91 M247.83 66L228.61 66L228.61 22L246.06 22Q250.52 22 253.38 23.79Q256.24 25.58 257.63 28.36Q259.02 31.14 259.02 34.10Q259.02 37.74 257.29 40.27Q255.56 42.81 252.60 43.69L252.60 42.17Q256.76 43.11 258.91 46.11Q261.06 49.10 261.06 52.92Q261.06 56.83 259.57 59.77Q258.07 62.70 255.12 64.35Q252.17 66 247.83 66M236.07 46.44L236.07 59.06L246.92 59.06Q248.84 59.06 250.37 58.28Q251.90 57.51 252.77 56.08Q253.64 54.66 253.64 52.71Q253.64 50.94 252.88 49.53Q252.11 48.13 250.64 47.28Q249.18 46.44 247.13 46.44L236.07 46.44M236.07 28.88L236.07 39.57L245.97 39.57Q247.56 39.57 248.81 38.96Q250.06 38.35 250.80 37.16Q251.53 35.96 251.53 34.22Q251.53 31.93 250.06 30.40Q248.60 28.88 245.97 28.88 M279.15 66L271.79 66L271.79 22L279.15 22 M310.15 66L302.79 66L302.79 28.91L288.55 28.91L288.55 22L324.39 22L324.39 28.91L310.15 28.91";

/**
 * Pure-vector Zero-Interrupt Orbital Z (S1) primary symbol master (128x128 viewBox).
 * Automatically uses the optical small-scale master (< 28px) with bolder bars and ring wall so the mark remains sharp down to 16px favicon sizes.
 */
export const ZarorbitSymbolSvg: React.FC<{
  sizePx?: number;
  tone?: "dark" | "light" | "mono-dark" | "mono-light";
  className?: string;
}> = ({ sizePx = 40, tone = "dark", className = "" }) => {
  const isSmallOptical = sizePx < 28;
  const fill =
    tone === "light"
      ? "#82611C"
      : tone === "mono-dark"
      ? "#F2EEE5"
      : tone === "mono-light"
      ? "#17191D"
      : "#C9A45A";

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 128 128"
      width={sizePx}
      height={sizePx}
      role="img"
      aria-label="ZARORBIT Symbol"
      className={className}
    >
      <path
        d={isSmallOptical ? S1_FAVICON_PATH : S1_PRIMARY_PATH}
        fill={fill}
        fillRule="nonzero"
      />
    </svg>
  );
};

/**
 * Pure-vector separated [Symbol] ZARORBIT horizontal lockup (417x88 viewBox).
 * Uses the owner-approved Zero-Interrupt Orbital Z (S1) paired with genuine Manrope Bold outlines.
 */
export const ZarorbitHorizontalLockupSvg: React.FC<{
  heightPx?: number;
  tone?: "dark" | "light";
  className?: string;
}> = ({ heightPx = 36, tone = "dark", className = "" }) => {
  const widthPx = Math.round((heightPx * 417) / 88);
  const symbolColor = tone === "light" ? "#82611C" : "#C9A45A";
  const wordColor = tone === "light" ? "#17191D" : "#F2EEE5";

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 417 88"
      width={widthPx}
      height={heightPx}
      role="img"
      aria-label="ZARORBIT"
      className={className}
    >
      <g transform="translate(4,12) scale(0.5)">
        <path d={S1_PRIMARY_PATH} fill={symbolColor} fillRule="nonzero" />
      </g>
      <g transform="translate(82,0)" fill={wordColor} fillRule="nonzero">
        <path d={WORDMARK_MANROPE_PATH} />
      </g>
    </svg>
  );
};

/** Backwards-compatible export alias for existing pages importing ZarorbitIntegratedLockupSvg. */
export const ZarorbitIntegratedLockupSvg = ZarorbitHorizontalLockupSvg;

/**
 * Clean Obsidian emblem container holding the pure-vector S1 symbol.
 */
export const BrandEmblem: React.FC<{
  sizePx?: number;
  className?: string;
}> = ({ sizePx = 42, className = "" }) => {
  const iconPx = Math.round(sizePx * 0.72);
  return (
    <div
      style={{ width: sizePx, height: sizePx }}
      className={`rounded-xl bg-[#17191D] border border-[#2B303A] flex items-center justify-center shrink-0 transition-colors duration-200 hover:border-[#C9A45A] ${className}`}
    >
      <ZarorbitSymbolSvg sizePx={iconPx} />
    </div>
  );
};

export default function BrandLogo({
  variant = "full",
  size = "md",
  tone = "dark",
  className = "",
  withLink = true,
}: BrandLogoProps) {
  const sizeMap = {
    sm: { lockupH: 26, symbolPx: 26, faText: "text-xs" },
    md: { lockupH: 32, symbolPx: 34, faText: "text-xs" },
    lg: { lockupH: 40, symbolPx: 44, faText: "text-sm" },
    xl: { lockupH: 48, symbolPx: 56, faText: "text-base" },
  };

  const { lockupH, symbolPx, faText } = sizeMap[size];
  const textPrimary = tone === "light" ? "text-[#17191D]" : "text-[#F2EEE5]";
  const textMuted = tone === "light" ? "text-[#5B626E]" : "text-[#9CA3AF]";
  const borderCol = tone === "light" ? "border-[#D0C8B8]" : "border-[#2B303A]";

  const content = (
    <div className={`inline-flex items-center gap-3 select-none ${className}`}>
      {variant === "icon" ? (
        <ZarorbitSymbolSvg
          sizePx={symbolPx}
          tone={tone === "light" ? "light" : "dark"}
        />
      ) : (
        <>
          <ZarorbitHorizontalLockupSvg heightPx={lockupH} tone={tone} />
          {variant === "full" && (
            <div
              className={`hidden sm:flex flex-col border-r ${borderCol} pr-3 mr-0.5`}
            >
              <span className={`${faText} font-bold ${textPrimary}`}>
                {brandConfig.name.fa}
              </span>
              <span className={`text-[10px] ${textMuted}`}>
                {brandConfig.slogan.categoryFa}
              </span>
            </div>
          )}
        </>
      )}
    </div>
  );

  if (withLink) {
    return (
      <Link
        href="/"
        className="inline-flex items-center focus:outline-none focus-visible:ring-2 focus-visible:ring-[#C9A45A] rounded-lg"
      >
        {content}
      </Link>
    );
  }

  return content;
}
