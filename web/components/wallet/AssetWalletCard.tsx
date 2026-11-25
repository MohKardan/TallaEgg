import { formatNumber } from "@/lib/formatters";
import type { AssetCategory, WalletItem } from "@/lib/api/wallet-client";

interface CategoryStyle {
  badgeLabel: string;
  badgeClassName: string;
  iconClassName: string;
  amountClassName: string;
}

const CATEGORY_STYLES: Record<AssetCategory, CategoryStyle> = {
  gold: {
    badgeLabel: "طلای آبشده / RWA",
    badgeClassName: "text-[#C9A45A] bg-[#2B2416] border-[#82611C]",
    iconClassName: "bg-[#2B2416] border-[#82611C] text-[#C9A45A]",
    amountClassName: "text-[#C9A45A]",
  },
  crypto: {
    badgeLabel: "دارایی دیجیتال",
    badgeClassName: "text-[#6BA4E8] bg-[#192535] border-[#2D4D73]",
    iconClassName: "bg-[#192535] border-[#2D4D73] text-[#6BA4E8]",
    amountClassName: "text-[#6BA4E8]",
  },
  fiat: {
    badgeLabel: "ارز",
    badgeClassName: "text-[#48B884] bg-[#162A24] border-[#286B63]",
    iconClassName: "bg-[#162A24] border-[#286B63] text-[#4CB3A5]",
    amountClassName: "text-[#48B884]",
  },
  credit: {
    badgeLabel: "اعتبار شناور",
    badgeClassName: "text-[#6BA4E8] bg-[#192535] border-[#2D4D73]",
    iconClassName: "bg-[#192535] border-[#2D4D73] text-[#6BA4E8]",
    amountClassName: "text-[#6BA4E8]",
  },
  other: {
    badgeLabel: "دارایی دفترکل",
    badgeClassName: "text-[#F2EEE5] bg-[#252A32] border-[#6B7485]",
    iconClassName: "bg-[#252A32] border-[#6B7485] text-[#F2EEE5]",
    amountClassName: "text-[#F2EEE5]",
  },
};

interface AssetWalletCardProps {
  wallet: WalletItem;
}

export default function AssetWalletCard({ wallet }: AssetWalletCardProps) {
  const style = CATEGORY_STYLES[wallet.category] ?? CATEGORY_STYLES.other;
  const maxDecimals = wallet.decimalPlaces ?? 4;

  return (
    <div className="zar-panel-interactive p-6">
      <div className="flex items-center justify-between mb-4 gap-2">
        <div className="flex items-center gap-3 min-w-0">
          <div
            className={`h-10 w-10 rounded-xl border flex items-center justify-center font-bold text-xs bidi-isolate shrink-0 ${style.iconClassName}`}
          >
            {wallet.asset.slice(0, 4)}
          </div>
          <div className="min-w-0">
            <h3 className="font-bold text-[#F2EEE5] text-base truncate">
              {wallet.persianLabel}
            </h3>
            <span className="bidi-isolate text-[11px] text-[#9CA3AF] font-mono">
              {wallet.asset}
            </span>
          </div>
        </div>
        <span
          className={`shrink-0 text-xs font-semibold px-2.5 py-0.5 rounded-full border ${style.badgeClassName}`}
        >
          {style.badgeLabel}
        </span>
      </div>

      <div className="space-y-3 pt-3">
        <div>
          <span className="text-xs text-[#9CA3AF] block mb-1">
            موجودی در دسترس:
          </span>
          <div
            className={`text-2xl sm:text-3xl font-black tracking-wide tabular-nums ${style.amountClassName}`}
          >
            {formatNumber(wallet.availableBalance, 0, maxDecimals)}{" "}
            <span className="text-xs font-normal text-[#9CA3AF]">
              {wallet.unit}
            </span>
          </div>
        </div>

        <div className="pt-3 border-t border-[#2B303A] grid grid-cols-2 gap-2 text-xs">
          <div>
            <span className="text-[#9CA3AF] block">موجودی کل:</span>
            <span className="font-semibold text-[#F2EEE5] tabular-nums">
              {formatNumber(wallet.totalBalance, 0, maxDecimals)} {wallet.unit}
            </span>
          </div>
          <div>
            <span className="text-[#9CA3AF] block">بلوکه در سفارشات:</span>
            <span className="font-semibold text-[#E2A93B] tabular-nums">
              {formatNumber(wallet.lockedBalance, 0, maxDecimals)} {wallet.unit}
            </span>
          </div>
        </div>
      </div>

      <div className="mt-6 pt-4 border-t border-[#2B303A] flex items-center justify-between text-xs">
        <span className="text-[#9CA3AF]">ماهیت حقوقی:</span>
        <span className="text-[#F2EEE5] font-medium">
          {wallet.asset.toUpperCase() === "MAUA"
            ? "تعهد دفترکل (تحویل مشروط به ضوابط)"
            : "رکورد دفترکل داخلی"}
        </span>
      </div>
    </div>
  );
}
