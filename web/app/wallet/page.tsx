"use client";

import Navbar from "@/components/Navbar";
import Footer from "@/components/Footer";
import { useEffect, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import {
  Wallet,
  Coins,
  Shield,
  Clock,
  RefreshCw,
  AlertTriangle,
  CheckCircle,
  HelpCircle,
  Lock,
  ArrowUpRight,
  Send,
  Sparkles,
} from "lucide-react";
import { formatNumber, formatJalaliDateTime } from "@/lib/formatters";
import { brandConfig } from "@/config/brand.config";

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
      // 1. Verify user session via BFF route
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

      // Check if user is in an unapproved state
      if (meData.user.status !== "Approved") {
        setLoading(false);
        return;
      }

      // 2. Fetch wallet balances via BFF route (server resolves userId from session)
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

  // Group wallets
  const irtWallet = wallets.find((w) => w.asset.toUpperCase() === "IRT");
  const goldWallets = wallets.filter(
    (w) => !w.isCreditLedger && w.asset.toUpperCase() !== "IRT"
  );
  const creditWallets = wallets.filter((w) => w.isCreditLedger);

  // Check if approved but has absolutely no balance yet
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
        <div className="ambient-glow top-20 right-10 opacity-30" />
        <div className="ambient-glow bottom-20 left-10 opacity-30" />

        {/* Header with user greeting & refresh button */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-8 mb-8 border-b border-amber-500/15 gap-4">
          <div>
            <div className="flex items-center gap-3 mb-1">
              <h1 className="text-2xl sm:text-3xl font-extrabold text-white">
                کیف پول و مدیریت دارایی
              </h1>
              {sessionUser && (
                <span
                  className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold ${
                    sessionUser.status === "Approved"
                      ? "bg-emerald-500/15 text-emerald-400 border border-emerald-500/30"
                      : "bg-amber-500/15 text-amber-400 border border-amber-500/30"
                  }`}
                >
                  <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
                  {sessionUser.status === "Approved"
                    ? "حساب فعال"
                    : sessionUser.status === "Pending"
                    ? "در انتظار تأیید"
                    : sessionUser.status}
                </span>
              )}
            </div>
            <p className="text-xs text-slate-400 font-light">
              کاربر: {sessionUser?.firstName || ""} {sessionUser?.lastName || ""}{" "}
              {sessionUser?.username ? `(@${sessionUser.username})` : ""}
            </p>
          </div>

          <div className="flex items-center gap-3">
            {lastUpdated && (
              <span className="text-[11px] text-slate-400 font-light hidden md:inline">
                آخرین بروزرسانی: {formatJalaliDateTime(lastUpdated)}
              </span>
            )}
            <button
              onClick={handleRefresh}
              disabled={refreshing || loading}
              className="inline-flex items-center gap-2 rounded-xl border border-amber-500/30 bg-amber-500/10 px-4 py-2.5 text-xs font-medium text-amber-300 hover:bg-amber-500/20 active:scale-95 transition-all disabled:opacity-50"
            >
              <RefreshCw
                className={`h-4 w-4 ${refreshing ? "animate-spin" : ""}`}
              />
              <span>بروزرسانی موجودی</span>
            </button>
          </div>
        </div>

        {/* ---------------- LOADING STATE ---------------- */}
        {loading && (
          <div className="py-24 flex flex-col items-center justify-center space-y-4">
            <div className="h-10 w-10 rounded-full border-2 border-amber-500 border-t-transparent animate-spin" />
            <p className="text-xs text-slate-400 animate-pulse">
              در حال دریافت امن اطلاعات کیف پول از سرویس دفترکل...
            </p>
          </div>
        )}

        {/* ---------------- UNAPPROVED / PENDING STATE ---------------- */}
        {!loading && sessionUser && sessionUser.status !== "Approved" && (
          <div className="my-8 rounded-3xl border border-amber-500/30 bg-[#121622] p-8 sm:p-12 text-center max-w-2xl mx-auto shadow-goldGlowSubtle space-y-5">
            <div className="inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-amber-500/10 border border-amber-500/30 text-amber-400">
              <AlertTriangle className="h-8 w-8" />
            </div>
            <h2 className="text-2xl font-bold text-white">
              حساب کاربری شما هنوز تأیید نشده است
            </h2>
            <p className="text-sm text-slate-300 font-light leading-relaxed max-w-lg mx-auto">
              {sessionUser.status === "Pending"
                ? "حساب کاربری شما با موفقیت ایجاد شده است اما برای دسترسی به کیف پول و شروع معاملات طلا، نیازمند تأیید هویت توسط مدیریت سامانه می‌باشد."
                : `وضعیت حساب کاربری شما در حال حاضر «${sessionUser.status}» می‌باشد و امکان مشاهده یا انجام تراکنش وجود ندارد.`}
            </p>
            <div className="pt-4 flex flex-col sm:flex-row items-center justify-center gap-4">
              <a
                href={brandConfig.telegram.botUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-400 to-amber-600 px-6 py-3.5 text-xs font-bold text-slate-950 shadow-goldGlowSubtle hover:brightness-110 transition-all"
              >
                <Send className="h-4 w-4" />
                <span>پیگیری تأیید در ربات تلگرام</span>
              </a>
              <button
                onClick={handleRefresh}
                className="w-full sm:w-auto inline-flex items-center justify-center gap-2 rounded-xl border border-slate-700 bg-slate-800/80 px-6 py-3.5 text-xs font-semibold text-slate-300 hover:bg-slate-700 transition-colors"
              >
                <RefreshCw className="h-4 w-4" />
                <span>بررسی مجدد وضعیت</span>
              </button>
            </div>
          </div>
        )}

        {/* ---------------- ERROR STATE ---------------- */}
        {!loading && error && sessionUser?.status === "Approved" && (
          <div className="mb-8 rounded-2xl border border-rose-500/30 bg-rose-500/10 p-6 text-sm text-rose-300 flex items-start gap-4">
            <AlertTriangle className="h-6 w-6 text-rose-400 shrink-0" />
            <div>
              <h4 className="font-bold text-rose-200 mb-1">خطا در بارگذاری کیف پول</h4>
              <p className="text-xs leading-relaxed">{error}</p>
            </div>
          </div>
        )}

        {/* ---------------- APPROVED STATE WITH WALLET BALANCES ---------------- */}
        {!loading && sessionUser?.status === "Approved" && (
          <div className="space-y-10">
            {/* APPROVED BUT EMPTY / NO BALANCE YET STATE */}
            {hasNoBalance && (
              <div className="rounded-2xl border border-amber-500/20 bg-amber-500/5 p-6 sm:p-8 flex flex-col md:flex-row items-center justify-between gap-6">
                <div className="flex items-start gap-4">
                  <div className="h-12 w-12 rounded-xl bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400 shrink-0">
                    <Sparkles className="h-6 w-6" />
                  </div>
                  <div className="space-y-1">
                    <h3 className="text-base font-bold text-white">
                      حساب شما تأیید شده است، اما هنوز موجودی ندارید
                    </h3>
                    <p className="text-xs text-slate-300 font-light leading-relaxed max-w-xl">
                      برای شروع معاملات طلای آبشده، می‌توانید از طریق ربات تلگرام حساب ریالی خود را واریز نمایید یا با تحویل طلای فیزیکی به صرافی، کیف پول آبشده خود را شارژ کنید.
                    </p>
                  </div>
                </div>
                <a
                  href={brandConfig.telegram.botUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="shrink-0 inline-flex items-center gap-2 rounded-xl bg-amber-500 px-5 py-3 text-xs font-bold text-slate-950 hover:bg-amber-400 transition-colors"
                >
                  <Send className="h-4 w-4" />
                  <span>واریز ریالی در ربات تلگرام</span>
                </a>
              </div>
            )}

            {/* Primary Balances Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {/* Card 1: Toman Balance (IRT) */}
              <div className="glass-card-interactive rounded-2xl p-6 relative overflow-hidden border border-amber-500/25">
                <div className="flex items-center justify-between mb-4">
                  <div className="flex items-center gap-3">
                    <div className="h-10 w-10 rounded-xl bg-emerald-500/15 border border-emerald-500/30 flex items-center justify-center text-emerald-400 font-bold">
                      IRT
                    </div>
                    <div>
                      <h3 className="font-bold text-white text-base">کیف پول تومانی</h3>
                      <span className="text-[11px] text-slate-400">واحد پول رایج (IRT)</span>
                    </div>
                  </div>
                  <span className="text-xs font-semibold text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded-md border border-emerald-500/20">
                    تسویه نقدی
                  </span>
                </div>

                <div className="space-y-3 pt-3">
                  <div>
                    <span className="text-xs text-slate-400 block mb-1">
                      موجودی در دسترس (قابل معامله/برداشت):
                    </span>
                    <div className="text-2xl sm:text-3xl font-black text-white tracking-wide">
                      {formatNumber(irtWallet?.availableBalance ?? 0)}{" "}
                      <span className="text-xs font-normal text-slate-400">تومان</span>
                    </div>
                  </div>

                  <div className="pt-3 border-t border-slate-800 grid grid-cols-2 gap-2 text-xs">
                    <div>
                      <span className="text-slate-400 block">موجودی کل:</span>
                      <span className="font-semibold text-slate-200">
                        {formatNumber(irtWallet?.balance ?? 0)} تومان
                      </span>
                    </div>
                    <div>
                      <span className="text-slate-400 block">بلوکه در سفارشات:</span>
                      <span className="font-semibold text-amber-400">
                        {formatNumber(irtWallet?.lockedBalance ?? 0)} تومان
                      </span>
                    </div>
                  </div>
                </div>

                <div className="mt-6 pt-4 border-t border-slate-800/80 flex items-center justify-between text-xs">
                  <span className="text-slate-400">وضعیت دفترکل:</span>
                  <span className="text-emerald-400 font-medium flex items-center gap-1">
                    <CheckCircle className="h-3.5 w-3.5" />
                    تراز و بروز
                  </span>
                </div>
              </div>

              {/* Card 2: Precious Assets & Melted Gold */}
              {goldWallets.map((wallet) => (
                <div
                  key={wallet.asset}
                  className="glass-card-interactive rounded-2xl p-6 relative overflow-hidden border border-amber-500/30 bg-gradient-to-br from-[#161a26] to-[#0f121a]"
                >
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-3">
                      <div className="h-10 w-10 rounded-xl bg-amber-500/20 border border-amber-500/40 flex items-center justify-center text-amber-400 font-bold">
                        {wallet.asset.slice(0, 4)}
                      </div>
                      <div>
                        <h3 className="font-bold text-white text-base">
                          {wallet.persianLabel}
                        </h3>
                        <span className="text-[11px] text-slate-400">
                          {wallet.asset}
                        </span>
                      </div>
                    </div>
                    <span className="text-xs font-semibold text-amber-300 bg-amber-500/10 px-2 py-0.5 rounded-md border border-amber-500/30">
                      {wallet.asset === "MAUA" ? "شمش / آبشده" : "دارایی معتبر"}
                    </span>
                  </div>

                  <div className="space-y-3 pt-3">
                    <div>
                      <span className="text-xs text-slate-400 block mb-1">
                        موجودی در دسترس:
                      </span>
                      <div className="text-2xl sm:text-3xl font-black text-amber-400 tracking-wide">
                        {formatNumber(wallet.availableBalance, 0, 4)}{" "}
                        <span className="text-xs font-normal text-slate-300">{wallet.unit}</span>
                      </div>
                    </div>

                    <div className="pt-3 border-t border-slate-800 grid grid-cols-2 gap-2 text-xs">
                      <div>
                        <span className="text-slate-400 block">موجودی کل:</span>
                        <span className="font-semibold text-slate-200">
                          {formatNumber(wallet.balance, 0, 4)} {wallet.unit}
                        </span>
                      </div>
                      <div>
                        <span className="text-slate-400 block">بلوکه در سفارشات:</span>
                        <span className="font-semibold text-amber-300">
                          {formatNumber(wallet.lockedBalance, 0, 4)} {wallet.unit}
                        </span>
                      </div>
                    </div>
                  </div>

                  <div className="mt-6 pt-4 border-t border-slate-800/80 flex items-center justify-between text-xs">
                    <span className="text-slate-400">قابلیت تحویل:</span>
                    <span className="text-amber-400 font-medium">
                      {wallet.asset === "MAUA" ? "شمش فیزیکی با انگ" : "تحویل طبق ضوابط"}
                    </span>
                  </div>
                </div>
              ))}

              {/* Card 3: Credit Line (CREDIT_MAUA) */}
              {creditWallets.map((wallet) => (
                <div
                  key={wallet.asset}
                  className="glass-card-interactive rounded-2xl p-6 relative overflow-hidden border border-sky-500/25 bg-[#0e1320]"
                >
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-3">
                      <div className="h-10 w-10 rounded-xl bg-sky-500/15 border border-sky-500/30 flex items-center justify-center text-sky-400 font-bold">
                        CR
                      </div>
                      <div>
                        <h3 className="font-bold text-white text-base">
                          {wallet.persianLabel}
                        </h3>
                        <span className="text-[11px] text-slate-400">
                          {wallet.asset} (اعتبار شناور)
                        </span>
                      </div>
                    </div>
                    <span className="text-xs font-semibold text-sky-400 bg-sky-500/10 px-2 py-0.5 rounded-md border border-sky-500/20">
                      اعتبار معامله
                    </span>
                  </div>

                  <div className="space-y-3 pt-3">
                    <div>
                      <span className="text-xs text-slate-400 block mb-1">
                        سقف اعتبار فعال طلایی:
                      </span>
                      <div className="text-2xl sm:text-3xl font-black text-sky-300 tracking-wide">
                        {formatNumber(wallet.balance, 0, 4)}{" "}
                        <span className="text-xs font-normal text-slate-400">{wallet.unit}</span>
                      </div>
                    </div>

                    <div className="pt-3 border-t border-slate-800 text-xs text-slate-300 leading-relaxed">
                      💡 این سقف اعتباری توسط میز بازارساز تعریف شده و به شما اجازه می‌دهد فراتر از مانده نقد، موقعیت معاملاتی طلا اخذ نمایید.
                    </div>
                  </div>

                  <div className="mt-6 pt-4 border-t border-slate-800/80 flex items-center justify-between text-xs">
                    <span className="text-slate-400">مدل تسویه:</span>
                    <span className="text-sky-400 font-medium">بین‌دارایی (Cross-Asset)</span>
                  </div>
                </div>
              ))}
            </div>

            {/* Quick Actions & Bot integration prompt */}
            <div className="rounded-2xl border border-slate-800 bg-[#0c0f18] p-6 sm:p-8 flex flex-col sm:flex-row items-center justify-between gap-6">
              <div className="space-y-1">
                <h4 className="text-base font-bold text-white">
                  ثبت سفارش خرید یا فروش لحظه‌ای طلا
                </h4>
                <p className="text-xs text-slate-400 font-light max-w-xl leading-relaxed">
                  سفارش‌گذاری و اجرای مستقیم معاملات هم‌اکنون از طریق ربات تلگرام {brandConfig.name.fa} انجام می‌شود. پس از هر معامله، تغییرات موجودی در این صفحه به صورت آنی منعکس خواهد شد.
                </p>
              </div>

              <a
                href={brandConfig.telegram.botUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="shrink-0 inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-amber-400 to-amber-600 px-6 py-3.5 text-xs font-bold text-slate-950 shadow-goldGlowSubtle hover:brightness-110 transition-all"
              >
                <Send className="h-4 w-4" />
                <span>ورود به ربات معامله‌گری</span>
              </a>
            </div>
          </div>
        )}
      </main>

      <Footer />
    </>
  );
}
