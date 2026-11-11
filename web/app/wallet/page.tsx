"use client";

import Navbar from "@/components/Navbar";
import Footer from "@/components/Footer";
import { useEffect, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import {
  RefreshCw,
  AlertTriangle,
  CheckCircle,
  Send,
  Sparkles,
  Info,
} from "lucide-react";
import { formatNumber, formatJalaliDateTime } from "@/lib/formatters";
import { brandConfig } from "@/config/brand.config";
import { BrandEmblem } from "@/components/BrandLogo";

interface WalletItem {
  asset: string;
  balance: number;
  lockedBalance: number;
  availableBalance: number;
  updatedAt: string;
  isCreditLedger: boolean;
  persianLabel: string;
  unit: string;
}

interface UserSession {
  telegramId: number;
  username?: string;
  firstName?: string;
  lastName?: string;
  role: string;
  status: string;
}

export default function WalletPage() {
  const router = useRouter();

  const [sessionUser, setSessionUser] = useState<UserSession | null>(null);
  const [wallets, setWallets] = useState<WalletItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<string>("");

  const fetchWalletData = useCallback(async () => {
    try {
      const meRes = await fetch("/api/auth/me", { cache: "no-store" });
      if (!meRes.ok) {
        router.push("/login");
        return;
      }
      const meData = await meRes.json();
      if (!meData.authenticated || !meData.user) {
        router.push("/login");
        return;
      }
      setSessionUser(meData.user);

      if (meData.user.status !== "Approved") {
        setLoading(false);
        return;
      }

      const walletRes = await fetch("/api/wallet/balances", {
        cache: "no-store",
      });
      const walletData = await walletRes.json();

      if (walletRes.ok && walletData.success) {
        setWallets(walletData.wallets || []);
        setLastUpdated(walletData.summary?.timestamp || new Date().toISOString());
        setError(null);
      } else {
        setError(walletData.message || "خطا در بارگذاری اطلاعات کیف پول.");
      }
    } catch (err: any) {
      setError("خطا در برقراری ارتباط با سرویس: " + err.message);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [router]);

  useEffect(() => {
    fetchWalletData();
  }, [fetchWalletData]);

  const handleRefresh = () => {
    setRefreshing(true);
    fetchWalletData();
  };

  const irtWallet = wallets.find((w) => w.asset.toUpperCase() === "IRT");
  const goldWallets = wallets.filter(
    (w) => !w.isCreditLedger && w.asset.toUpperCase() !== "IRT"
  );
  const creditWallets = wallets.filter((w) => w.isCreditLedger);

  const hasNoBalance =
    wallets.length === 0 ||
    wallets.every(
      (w) =>
        (Number(w.balance) || 0) === 0 && (Number(w.lockedBalance) || 0) === 0
    );

  return (
    <>
      <Navbar />

      <main className="relative flex-1 py-12 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto w-full">
        {/* Header with user greeting & refresh button */}
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
                  {sessionUser.status === "Approved"
                    ? "حساب فعال (Beta)"
                    : sessionUser.status === "Pending"
                    ? "در انتظار تأیید"
                    : sessionUser.status}
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
              onClick={handleRefresh}
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

        {/* LOADING STATE */}
        {loading && (
          <div className="py-24 flex flex-col items-center justify-center space-y-4">
            <div className="h-10 w-10 rounded-full border-2 border-[#C9A45A] border-t-transparent animate-spin" />
            <p className="text-xs text-[#9CA3AF]">
              در حال دریافت اطلاعات دفترکل دارایی...
            </p>
          </div>
        )}

        {/* UNAPPROVED / PENDING STATE */}
        {!loading && sessionUser && sessionUser.status !== "Approved" && (
          <div className="my-8 zar-card p-8 sm:p-12 text-center max-w-2xl mx-auto shadow-card space-y-5">
            <div className="inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-[#2B2416] border border-[#6B501D] text-[#E2A93B]">
              <AlertTriangle className="h-8 w-8" />
            </div>
            <h2 className="text-2xl font-bold text-[#F2EEE5]">
              حساب کاربری شما هنوز تأیید نشده است
            </h2>
            <p className="text-sm text-[#9CA3AF] font-light leading-relaxed max-w-lg mx-auto">
              {sessionUser.status === "Pending"
                ? "حساب شما ایجاد شده است اما برای دسترسی به دفترکل دارایی، نیازمند تأیید هویت پایه توسط اپراتور سامانه است."
                : `وضعیت حساب شما در حال حاضر «${sessionUser.status}» است و امکان مشاهده یا انجام تراکنش وجود ندارد.`}
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
                onClick={handleRefresh}
                className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-[10px] border border-[#6B7485] bg-[#17191D] px-6 py-3.5 text-xs font-semibold text-[#F2EEE5] hover:border-[#C9A45A] transition-colors"
              >
                <RefreshCw className="h-4 w-4" />
                <span>بررسی مجدد وضعیت</span>
              </button>
            </div>
          </div>
        )}

        {/* ERROR STATE */}
        {!loading && error && sessionUser?.status === "Approved" && (
          <div className="mb-8 rounded-[14px] border border-[#6E3438] bg-[#2D1B1E] p-6 text-sm text-[#F07F83] flex items-start gap-4">
            <AlertTriangle className="h-6 w-6 shrink-0" />
            <div>
              <h4 className="font-bold text-[#F2EEE5] mb-1">
                خطا در بارگذاری کیف پول
              </h4>
              <p className="text-xs leading-relaxed">{error}</p>
            </div>
          </div>
        )}

        {/* APPROVED STATE */}
        {!loading && sessionUser?.status === "Approved" && (
          <div className="space-y-8">
            {hasNoBalance && (
              <div className="zar-card p-6 sm:p-8 flex flex-col md:flex-row items-center justify-between gap-6">
                <div className="flex items-start gap-4">
                  <div className="h-12 w-12 rounded-xl bg-[#2B2416] border border-[#6B501D] flex items-center justify-center text-[#C9A45A] shrink-0">
                    <Sparkles className="h-6 w-6" />
                  </div>
                  <div className="space-y-1">
                    <h3 className="text-base font-bold text-[#F2EEE5]">
                      حساب شما فعال است، اما هنوز مانده ثبت‌شده‌ای وجود ندارد
                    </h3>
                    <p className="text-xs text-[#9CA3AF] font-light leading-relaxed max-w-xl">
                      در ماژول فعلی RWA، شارژ ریالی و ثبت معاملات آزمایشی طلا
                      از طریق ربات رسمی تلگرام انجام می‌شود.
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
            )}

            {/* Primary Balances Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {/* IRT Card */}
              <div className="zar-card-interactive p-6">
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
                      {formatNumber(irtWallet?.availableBalance ?? 0)}{" "}
                      <span className="text-xs font-normal text-[#9CA3AF]">
                        تومان
                      </span>
                    </div>
                  </div>

                  <div className="pt-3 border-t border-[#2B303A] grid grid-cols-2 gap-2 text-xs">
                    <div>
                      <span className="text-[#9CA3AF] block">موجودی کل:</span>
                      <span className="font-semibold text-[#F2EEE5] tabular-nums">
                        {formatNumber(irtWallet?.balance ?? 0)} تومان
                      </span>
                    </div>
                    <div>
                      <span className="text-[#9CA3AF] block">
                        بلوکه در سفارشات:
                      </span>
                      <span className="font-semibold text-[#E2A93B] tabular-nums">
                        {formatNumber(irtWallet?.lockedBalance ?? 0)} تومان
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

              {/* RWA / Melted Gold Wallets */}
              {goldWallets.map((wallet) => (
                <div key={wallet.asset} className="zar-card-interactive p-6">
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-3">
                      <div className="h-10 w-10 rounded-xl bg-[#2B2416] border border-[#82611C] flex items-center justify-center text-[#C9A45A] font-bold text-xs bidi-isolate">
                        {wallet.asset.slice(0, 4)}
                      </div>
                      <div>
                        <h3 className="font-bold text-[#F2EEE5] text-base">
                          {wallet.persianLabel}
                        </h3>
                        <span className="bidi-isolate text-[11px] text-[#9CA3AF] font-mono">
                          {wallet.asset}
                        </span>
                      </div>
                    </div>
                    <span className="text-xs font-semibold text-[#C9A45A] bg-[#2B2416] px-2.5 py-0.5 rounded-full border border-[#82611C]">
                      RWA Focus
                    </span>
                  </div>

                  <div className="space-y-3 pt-3">
                    <div>
                      <span className="text-xs text-[#9CA3AF] block mb-1">
                        موجودی در دسترس:
                      </span>
                      <div className="text-2xl sm:text-3xl font-black text-[#C9A45A] tracking-wide tabular-nums">
                        {formatNumber(wallet.availableBalance, 0, 4)}{" "}
                        <span className="text-xs font-normal text-[#9CA3AF]">
                          {wallet.unit}
                        </span>
                      </div>
                    </div>

                    <div className="pt-3 border-t border-[#2B303A] grid grid-cols-2 gap-2 text-xs">
                      <div>
                        <span className="text-[#9CA3AF] block">موجودی کل:</span>
                        <span className="font-semibold text-[#F2EEE5] tabular-nums">
                          {formatNumber(wallet.balance, 0, 4)} {wallet.unit}
                        </span>
                      </div>
                      <div>
                        <span className="text-[#9CA3AF] block">
                          بلوکه در سفارشات:
                        </span>
                        <span className="font-semibold text-[#E2A93B] tabular-nums">
                          {formatNumber(wallet.lockedBalance, 0, 4)} {wallet.unit}
                        </span>
                      </div>
                    </div>
                  </div>

                  <div className="mt-6 pt-4 border-t border-[#2B303A] flex items-center justify-between text-xs">
                    <span className="text-[#9CA3AF]">ماهیت حقوقی:</span>
                    <span className="text-[#F2EEE5] font-medium">
                      {wallet.asset === "MAUA"
                        ? "تعهد دفترکل (تحویل مشروط به ضوابط)"
                        : "رکورد دفترکل داخلی"}
                    </span>
                  </div>
                </div>
              ))}

              {/* Cross-Asset Credit Ledger */}
              {creditWallets.map((wallet) => (
                <div key={wallet.asset} className="zar-card-interactive p-6">
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-3">
                      <div className="h-10 w-10 rounded-xl bg-[#192535] border border-[#2D4D73] flex items-center justify-center text-[#6BA4E8] font-bold text-xs bidi-isolate">
                        CR
                      </div>
                      <div>
                        <h3 className="font-bold text-[#F2EEE5] text-base">
                          {wallet.persianLabel}
                        </h3>
                        <span className="bidi-isolate text-[11px] text-[#9CA3AF] font-mono">
                          {wallet.asset}
                        </span>
                      </div>
                    </div>
                    <span className="text-xs font-semibold text-[#6BA4E8] bg-[#192535] px-2.5 py-0.5 rounded-full border border-[#2D4D73]">
                      اعتبار شناور
                    </span>
                  </div>

                  <div className="space-y-3 pt-3">
                    <div>
                      <span className="text-xs text-[#9CA3AF] block mb-1">
                        سقف اعتبار تخصیص‌یافته:
                      </span>
                      <div className="text-2xl sm:text-3xl font-black text-[#6BA4E8] tracking-wide tabular-nums">
                        {formatNumber(wallet.balance, 0, 4)}{" "}
                        <span className="text-xs font-normal text-[#9CA3AF]">
                          {wallet.unit}
                        </span>
                      </div>
                    </div>

                    <div className="pt-3 border-t border-[#2B303A] text-xs text-[#9CA3AF] leading-relaxed">
                      سقف اعتباری تعریف‌شده توسط اپراتور میز برای معاملات
                      بین‌دارایی مطابق قوانین حساب.
                    </div>
                  </div>

                  <div className="mt-6 pt-4 border-t border-[#2B303A] flex items-center justify-between text-xs">
                    <span className="text-[#9CA3AF]">مدل تسویه:</span>
                    <span className="bidi-isolate text-[#4CB3A5] font-medium">
                      Cross-Asset Ledger
                    </span>
                  </div>
                </div>
              ))}
            </div>

            {/* Ownership & Legal Boundary Notice (§10.1) */}
            <div className="rounded-[14px] border border-[#2B303A] bg-[#1F2329] p-5 flex items-start gap-3 text-xs text-[#9CA3AF] leading-relaxed">
              <Info className="h-5 w-5 text-[#4CB3A5] shrink-0 mt-0.5" />
              <div>
                <strong className="text-[#F2EEE5]">
                  یادآور شفافیت مالکیت:
                </strong>{" "}
                اعداد نمایش‌داده‌شده در این صفحه، مانده ثبت‌شده در دفترکل داخلی
                سامانه هستند. تحویل فیزیکی یا حقوق مرتبط با دارایی واقعی تابع
                شرایط عملیاتی، حداقل نصاب و ضوابط اعلام‌شده است و صرف رکورد
                دیجیتال معادل سند مستقل مالکیت فیزیکی نیست.
              </div>
            </div>

            {/* Quick Action & Telegram integration */}
            <div className="zar-card p-6 sm:p-8 flex flex-col sm:flex-row items-center justify-between gap-6">
              <div className="space-y-1">
                <h4 className="text-base font-bold text-[#F2EEE5]">
                  ثبت سفارش جدید در ماژول طلای ۱۸ عیار (RWA)
                </h4>
                <p className="text-xs text-[#9CA3AF] font-light max-w-xl leading-relaxed">
                  ثبت سفارش خرید و فروش از طریق ربات تلگرام{" "}
                  {brandConfig.name.fa} انجام می‌شود و تغییرات پس از تسویه
                  دفترکل در این صفحه نمایش داده خواهد شد.
                </p>
              </div>

              <a
                href={brandConfig.telegram.botUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="shrink-0 inline-flex items-center gap-2 rounded-[10px] bg-[#286B63] px-6 py-3.5 text-xs font-bold text-white hover:bg-[#328076] transition-colors"
              >
                <Send className="h-4 w-4" />
                <span>ورود به ربات تلگرام</span>
              </a>
            </div>
          </div>
        )}
      </main>

      <Footer />
    </>
  );
}
