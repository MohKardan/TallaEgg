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

/**
 * Pure-vector R1 (1–0–1 orbital Z) primary symbol master (128x128 viewBox).
 * Automatically switches to the optical small-scale master (< 28px) with thicker strokes
 * and an enlarged central aperture so the 0-ring remains legible at favicon sizes.
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
      aria-label="ZARORBIT 1-0-1 Symbol"
      className={className}
    >
      {isSmallOptical ? (
        <g transform="translate(3.84,3.84) scale(0.94)">
          <g transform="rotate(-56 64 64)">
            <ellipse
              cx="64"
              cy="64"
              rx="38.5"
              ry="15.5"
              fill="none"
              stroke={fill}
              strokeWidth="11.5"
            />
          </g>
          <path d="M 36 21 H 96 V 34.5 H 25 L 23 32 Z" fill={fill} />
          <path d="M 32 93.5 H 103 L 105 96 L 92 107 H 32 Z" fill={fill} />
        </g>
      ) : (
        <g>
          <g transform="rotate(-56 64 64)">
            <ellipse
              cx="64"
              cy="64"
              rx="38.0"
              ry="14.5"
              fill="none"
              stroke={fill}
              strokeWidth="10.5"
            />
          </g>
          <path d="M 37 24 H 94 V 36 H 28 L 26 34 Z" fill={fill} />
          <path d="M 34 92 H 100 L 102 94 L 91 104 H 34 Z" fill={fill} />
        </g>
      )}
    </svg>
  );
};

/**
 * Pure-vector Integrated [R1]ARORBIT horizontal logotype (321x88 viewBox).
 * The approved R1 (1–0–1) monogram replaces the opening 'Z' to eliminate double-Z repetition.
 */
export const ZarorbitIntegratedLockupSvg: React.FC<{
  heightPx?: number;
  tone?: "dark" | "light";
  className?: string;
}> = ({ heightPx = 36, tone = "dark", className = "" }) => {
  const widthPx = Math.round((heightPx * 321) / 88);
  const symbolColor = tone === "light" ? "#82611C" : "#C9A45A";
  const wordColor = tone === "light" ? "#17191D" : "#F2EEE5";

  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 321 88"
      width={widthPx}
      height={heightPx}
      role="img"
      aria-label="ZARORBIT"
      className={className}
    >
      <g transform="translate(1.05,7.20) scale(0.5750)">
        <g transform="rotate(-56 64 64)">
          <ellipse
            cx="64"
            cy="64"
            rx="38.0"
            ry="14.5"
            fill="none"
            stroke={symbolColor}
            strokeWidth="10.5"
          />
        </g>
        <path d="M 37 24 H 94 V 36 H 28 L 26 34 Z" fill={symbolColor} />
        <path d="M 34 92 H 100 L 102 94 L 91 104 H 34 Z" fill={symbolColor} />
      </g>
      <g
        transform="translate(63.20,22.00) scale(1.0000)"
        fill={wordColor}
        fillRule="evenodd"
      >
        <path
          transform="translate(0.00,0)"
          d="M 12.6 0 H 19.4 L 32 44 H 24.8 L 22.0 33.0 H 10.0 L 7.2 44 H 0 Z M 16 8.2 L 11.4 26.8 H 20.6 Z"
        />
        <path
          transform="translate(41.30,0)"
          d="M 0 0 H 16.5 A 12.1 12.1 0 0 1 16.5 24.2 H 17.5 L 28.8 44 H 21.0 L 10.2 24.2 H 6.5 V 44 H 0 Z M 6.5 6.2 V 18.0 H 16.0 A 5.9 5.9 0 0 0 16.0 6.2 Z"
        />
        <path
          transform="translate(78.80,0)"
          d="M 17 0 A 17 22 0 1 1 16.99 0 Z M 17 6.5 A 10.4 15.5 0 1 0 17.01 6.5 Z"
        />
        <path
          transform="translate(121.80,0)"
          d="M 0 0 H 16.5 A 12.1 12.1 0 0 1 16.5 24.2 H 17.5 L 28.8 44 H 21.0 L 10.2 24.2 H 6.5 V 44 H 0 Z M 6.5 6.2 V 18.0 H 16.0 A 5.9 5.9 0 0 0 16.0 6.2 Z"
        />
        <path
          transform="translate(159.90,0)"
          d="M 0 0 H 15.8 A 10.6 10.6 0 0 1 18.6 20.9 A 11.5 11.5 0 0 1 16.8 44 H 0 Z M 6.5 6.0 V 18.0 H 15.4 A 6.0 6.0 0 0 0 15.4 6.0 Z M 6.5 24.2 V 38.0 H 16.2 A 6.9 6.9 0 0 0 16.2 24.2 Z"
        />
        <path transform="translate(199.40,0)" d="M 0 0 H 6.8 V 44 H 0 Z" />
        <path
          transform="translate(213.90,0)"
          d="M 0 0 H 28 V 6.5 H 17.25 V 44 H 10.75 V 6.5 H 0 Z"
        />
      </g>
    </svg>
  );
};

/**
 * Clean Obsidian emblem container holding the pure-vector R1 symbol.
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

/**
 * Architectural Hero Card presenting the R1 (1–0–1) ecosystem geometry
 * and honest Brand Book v1.1 maturity boundaries across RWA, Digital, and DAO layers.
 */
export const BrandHeroShowcase: React.FC<{ className?: string }> = ({
  className = "",
}) => {
  return (
    <div
      className={`relative rounded-[20px] p-6 sm:p-8 bg-[#1F2329] border border-[#2B303A] shadow-card overflow-hidden ${className}`}
    >
      {/* Subtle architectural coordinate hairlines */}
      <div
        className="absolute inset-0 zar-grid-backdrop opacity-35 pointer-events-none"
        aria-hidden="true"
      />

      {/* Top Bar */}
      <div className="relative z-10 flex items-center justify-between border-b border-[#2B303A] pb-4">
        <span className="bidi-isolate text-[11px] font-bold tracking-[0.14em] text-[#C9A45A] uppercase">
          R1 // 1–0–1 ARCHITECTURE
        </span>
        <span className="inline-flex items-center gap-1.5 rounded-full bg-[#192535] border border-[#2D4D73] px-2.5 py-0.5 text-[11px] font-semibold text-[#6BA4E8]">
          Beta Focus
        </span>
      </div>

      {/* Central Vector Orbital Diagram */}
      <div className="relative z-10 my-6 flex items-center justify-center">
        <div className="relative w-56 h-56 rounded-2xl bg-[#17191D] border border-[#2B303A] flex items-center justify-center">
          <svg
            viewBox="0 0 224 224"
            width="224"
            height="224"
            className="absolute inset-0 pointer-events-none"
            aria-hidden="true"
          >
            {/* Primary restrained orbital track */}
            <ellipse
              cx="112"
              cy="112"
              rx="86"
              ry="34"
              transform="rotate(-24 112 112)"
              fill="none"
              stroke="#C9A45A"
              strokeWidth="1.2"
              strokeOpacity="0.45"
            />
            {/* Secondary interoperability teal dashed track */}
            <ellipse
              cx="112"
              cy="112"
              rx="66"
              ry="26"
              transform="rotate(28 112 112)"
              fill="none"
              stroke="#4CB3A5"
              strokeWidth="1.2"
              strokeDasharray="4 4"
              strokeOpacity="0.55"
            />
            {/* Two restrained participant network nodes */}
            <circle cx="38" cy="144" r="4" fill="#C9A45A" />
            <circle cx="170" cy="80" r="4" fill="#4CB3A5" />
          </svg>
          <ZarorbitSymbolSvg sizePx={96} className="relative z-10" />
        </div>
      </div>

      {/* Integrated Wordmark & Ecosystem Positioning */}
      <div className="relative z-10 text-center space-y-2 mb-5">
        <div className="flex justify-center">
          <ZarorbitIntegratedLockupSvg heightPx={32} />
        </div>
        <p className="bidi-isolate text-[11px] font-medium tracking-[0.06em] text-[#9CA3AF]">
          {brandConfig.slogan.en}
        </p>
      </div>

      {/* Honest Ecosystem Capability Layers */}
      <div className="relative z-10 space-y-2 border-t border-[#2B303A] pt-4 text-xs">
        <div className="flex items-center justify-between rounded-lg bg-[#17191D] px-3 py-2 border border-[#2B303A]">
          <span className="text-[#F2EEE5] font-medium">دارایی واقعی (RWA • طلای ۱۸ عیار)</span>
          <span className="text-[#6BA4E8] font-semibold">Beta</span>
        </div>
        <div className="flex items-center justify-between rounded-lg bg-[#17191D] px-3 py-2 border border-[#2B303A]">
          <span className="text-[#F2EEE5] font-medium">دارایی دیجیتال (رمزارز و NFT)</span>
          <span className="text-[#E2A93B] font-semibold">In Dev</span>
        </div>
        <div className="flex items-center justify-between rounded-lg bg-[#17191D] px-3 py-2 border border-[#2B303A]">
          <span className="text-[#F2EEE5] font-medium">حکمرانی جامعه (DAO) و AI/XR</span>
          <span className="text-[#9CA3AF] font-semibold">Planned</span>
        </div>
      </div>
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
          <ZarorbitIntegratedLockupSvg heightPx={lockupH} tone={tone} />
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
      <Link href="/" className="inline-flex items-center focus:outline-none focus-visible:ring-2 focus-visible:ring-[#C9A45A] rounded-lg">
        {content}
      </Link>
    );
  }

  return content;
}
