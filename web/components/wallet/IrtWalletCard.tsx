import { CheckCircle } from "lucide-react";
import { formatNumber } from "@/lib/formatters";
import type { WalletItem } from "@/lib/api/wallet-client";

interface IrtWalletCardProps {
  wallet?: WalletItem;
}

export default function IrtWalletCard({ wallet }: IrtWalletCardProps) {
  const decimals = wallet?.decimalPlaces ?? 0;

  return (
    <div className="zar-panel-interactive p-6">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-3">
          <div className="h-10 w-10 rounded-xl bg-[#162A24] border border-[#286B63] flex items-center justify-center text-[#4CB3A5] font-bold text-xs bidi-isolate">
            IRT
          </div>
          <div>
            <h3 className="font-bold text-[#F2EEE5] text-base">
              کیف پول تومانی
            </h3>
            <span className="text-[11px] text-[#9CA3AF]">
              واحد تسویه داخلی (IRT)
            </span>
          </div>
        </div>
        <span className="text-xs font-semibold text-[#48B884] bg-[#162A24] px-2.5 py-0.5 rounded-full border border-[#286B63]">
          دفترکل نقدی
        </span>
      </div>

      <div className="space-y-3 pt-3">
        <div>
          <span className="text-xs text-[#9CA3AF] block mb-1">
            موجودی در دسترس:
          </span>
          <div className="text-2xl sm:text-3xl font-black text-[#F2EEE5] tracking-wide tabular-nums">
            {formatNumber(wallet?.availableBalance ?? 0, 0, decimals)}{" "}
            <span className="text-xs font-normal text-[#9CA3AF]">تومان</span>
          </div>
        </div>

        <div className="pt-3 border-t border-[#2B303A] grid grid-cols-2 gap-2 text-xs">
          <div>
            <span className="text-[#9CA3AF] block">موجودی کل:</span>
            <span className="font-semibold text-[#F2EEE5] tabular-nums">
              {formatNumber(wallet?.totalBalance ?? 0, 0, decimals)} تومان
            </span>
          </div>
          <div>
            <span className="text-[#9CA3AF] block">بلوکه در سفارشات:</span>
            <span className="font-semibold text-[#E2A93B] tabular-nums">
              {formatNumber(wallet?.lockedBalance ?? 0, 0, decimals)} تومان
            </span>
          </div>
        </div>
      </div>

      <div className="mt-6 pt-4 border-t border-[#2B303A] flex items-center justify-between text-xs">
        <span className="text-[#9CA3AF]">وضعیت ثبت:</span>
        <span className="text-[#48B884] font-medium flex items-center gap-1">
          <CheckCircle className="h-3.5 w-3.5" />
          تراز دوطرفه
        </span>
      </div>
    </div>
  );
}
