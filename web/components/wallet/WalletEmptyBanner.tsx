import { Send, Sparkles } from "lucide-react";
import { brandConfig } from "@/config/brand.config";

export default function WalletEmptyBanner() {
  return (
    <div className="zar-panel p-6 sm:p-8 flex flex-col md:flex-row items-center justify-between gap-6">
      <div className="flex items-start gap-4">
        <div className="h-12 w-12 rounded-xl bg-[#2B2416] border border-[#6B501D] flex items-center justify-center text-[#C9A45A] shrink-0">
          <Sparkles className="h-6 w-6" />
        </div>
        <div className="space-y-1">
          <h3 className="text-base font-bold text-[#F2EEE5]">
            حساب شما فعال است، اما هنوز مانده ثبت‌شده‌ای وجود ندارد
          </h3>
          <p className="text-xs text-[#9CA3AF] font-light leading-relaxed max-w-xl">
            در ماژول فعلی RWA، شارژ ریالی و ثبت معاملات آزمایشی طلا از طریق
            ربات رسمی تلگرام انجام می‌شود.
          </p>
        </div>
      </div>
      <a
        href={brandConfig.telegram.botUrl}
        target="_blank"
        rel="noopener noreferrer"
        className="shrink-0 inline-flex items-center gap-2 rounded-[10px] bg-[#C9A45A] px-5 py-3 text-xs font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
      >
        <Send className="h-4 w-4" />
        <span>شروع در ربات تلگرام</span>
      </a>
    </div>
  );
}
