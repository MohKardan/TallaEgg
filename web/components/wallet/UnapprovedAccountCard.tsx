import { AlertTriangle, RefreshCw, Send } from "lucide-react";
import { brandConfig } from "@/config/brand.config";

const STATUS_LABELS: Record<string, string> = {
  Pending: "در انتظار تأیید",
  Rejected: "رد شده",
  Suspended: "معلق",
  Blocked: "مسدود",
};

interface UnapprovedAccountCardProps {
  status: string;
  onRefresh: () => void;
}

export default function UnapprovedAccountCard({
  status,
  onRefresh,
}: UnapprovedAccountCardProps) {
  const statusLabel = STATUS_LABELS[status] || "غیرفعال";

  return (
    <div className="my-8 zar-panel p-8 sm:p-12 text-center max-w-2xl mx-auto space-y-5">
      <div className="inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-[#2B2416] border border-[#6B501D] text-[#E2A93B]">
        <AlertTriangle className="h-8 w-8" />
      </div>
      <h2 className="text-2xl font-bold text-[#F2EEE5]">
        حساب کاربری شما هنوز تأیید نشده است
      </h2>
      <p className="text-sm text-[#9CA3AF] font-light leading-relaxed max-w-lg mx-auto">
        {status === "Pending"
          ? "حساب شما ایجاد شده است اما برای دسترسی به دفترکل دارایی، نیازمند تأیید هویت پایه توسط اپراتور سامانه است."
          : `وضعیت حساب شما در حال حاضر «${statusLabel}» است و امکان مشاهده یا انجام تراکنش وجود ندارد.`}
      </p>
      <div className="pt-4 flex flex-col sm:flex-row items-center justify-center gap-4">
        <a
          href={brandConfig.telegram.botUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-[10px] bg-[#C9A45A] px-6 py-3.5 text-xs font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
        >
          <Send className="h-4 w-4" />
          <span>پیگیری در ربات تلگرام</span>
        </a>
        <button
          type="button"
          onClick={onRefresh}
          className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-[10px] border border-[#6B7485] bg-[#17191D] px-6 py-3.5 text-xs font-semibold text-[#F2EEE5] hover:border-[#C9A45A] transition-colors"
        >
          <RefreshCw className="h-4 w-4" />
          <span>بررسی مجدد وضعیت</span>
        </button>
      </div>
    </div>
  );
}
