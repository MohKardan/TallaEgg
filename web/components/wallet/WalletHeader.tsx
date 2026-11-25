import { RefreshCw } from "lucide-react";
import { BrandEmblem } from "@/components/BrandLogo";
import { formatJalaliDateTime } from "@/lib/formatters";
import type { PublicSessionUser } from "@/lib/auth/use-session";

const STATUS_LABELS: Record<string, string> = {
  Approved: "حساب فعال (پایلوت)",
  Pending: "در انتظار تأیید",
  Rejected: "رد شده",
  Suspended: "معلق",
  Blocked: "مسدود",
};

interface WalletHeaderProps {
  sessionUser: PublicSessionUser | null;
  lastUpdated: string;
  refreshing: boolean;
  loading: boolean;
  onRefresh: () => void;
}

export default function WalletHeader({
  sessionUser,
  lastUpdated,
  refreshing,
  loading,
  onRefresh,
}: WalletHeaderProps) {
  const statusLabel = sessionUser
    ? STATUS_LABELS[sessionUser.status] || "غیرفعال"
    : "";

  return (
    <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-8 mb-8 border-b border-[#2B303A] gap-4">
      <div>
        <div className="flex items-center gap-3 mb-1.5 flex-wrap">
          <BrandEmblem sizePx={40} />
          <h1 className="text-2xl sm:text-3xl font-extrabold text-[#F2EEE5]">
            کیف پول دفترکل و دارایی‌ها
          </h1>
          {sessionUser && (
            <span
              className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold border ${
                sessionUser.status === "Approved"
                  ? "bg-[#162A24] text-[#48B884] border-[#286B63]"
                  : "bg-[#2B2416] text-[#E2A93B] border-[#6B501D]"
              }`}
            >
              <span className="h-1.5 w-1.5 rounded-full bg-current" />
              {statusLabel}
            </span>
          )}
        </div>
        <p className="text-xs text-[#9CA3AF] font-light">
          عضو: {sessionUser?.firstName || ""} {sessionUser?.lastName || ""}{" "}
          {sessionUser?.username ? (
            <span className="bidi-isolate font-mono">
              (@{sessionUser.username})
            </span>
          ) : (
            ""
          )}
        </p>
      </div>

      <div className="flex items-center gap-3">
        {lastUpdated && (
          <span className="text-[11px] text-[#9CA3AF] font-light hidden md:inline">
            آخرین همگام‌سازی: {formatJalaliDateTime(lastUpdated)}
          </span>
        )}
        <button
          type="button"
          onClick={onRefresh}
          disabled={refreshing || loading}
          className="inline-flex items-center gap-2 rounded-[10px] border border-[#6B7485] bg-[#1F2329] px-4 py-2.5 text-xs font-semibold text-[#F2EEE5] hover:border-[#C9A45A] hover:text-[#C9A45A] active:scale-[0.98] transition-all disabled:opacity-50"
        >
          <RefreshCw
            className={`h-4 w-4 ${refreshing ? "animate-spin" : ""}`}
          />
          <span>بروزرسانی موجودی</span>
        </button>
      </div>
    </div>
  );
}
