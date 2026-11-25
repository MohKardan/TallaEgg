import { formatNumber } from "@/lib/formatters";
import type { WalletItem } from "@/lib/api/wallet-client";

interface CreditWalletCardProps {
  wallet: WalletItem;
}

export default function CreditWalletCard({ wallet }: CreditWalletCardProps) {
  const maxDecimals = wallet.decimalPlaces ?? 4;

  return (
    <div className="zar-panel-interactive p-6">
      <div className="flex items-center justify-between mb-4 gap-2">
        <div className="flex items-center gap-3 min-w-0">
          <div className="h-10 w-10 rounded-xl bg-[#192535] border border-[#2D4D73] flex items-center justify-center text-[#6BA4E8] font-bold text-xs bidi-isolate shrink-0">
            CR
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
        <span className="shrink-0 text-xs font-semibold text-[#6BA4E8] bg-[#192535] px-2.5 py-0.5 rounded-full border border-[#2D4D73]">
          اعتبار شناور
        </span>
      </div>

      <div className="space-y-3 pt-3">
        <div>
          <span className="text-xs text-[#9CA3AF] block mb-1">
            سقف اعتبار تخصیص‌یافته:
          </span>
          <div className="text-2xl sm:text-3xl font-black text-[#6BA4E8] tracking-wide tabular-nums">
            {formatNumber(wallet.balance, 0, maxDecimals)}{" "}
            <span className="text-xs font-normal text-[#9CA3AF]">
              {wallet.unit}
            </span>
          </div>
        </div>

        <div className="pt-3 border-t border-[#2B303A] text-xs text-[#9CA3AF] leading-relaxed">
          سقف اعتباری تعریف‌شده توسط اپراتور میز برای معاملات بین‌دارایی مطابق
          قوانین حساب.
        </div>
      </div>

      <div className="mt-6 pt-4 border-t border-[#2B303A] flex items-center justify-between text-xs">
        <span className="text-[#9CA3AF]">مدل تسویه:</span>
        <span className="bidi-isolate text-[#4CB3A5] font-medium">
          Cross-Asset Ledger
        </span>
      </div>
    </div>
  );
}
