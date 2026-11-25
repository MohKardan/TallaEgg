"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AlertTriangle, Info, Send } from "lucide-react";
import Navbar from "@/components/Navbar";
import Footer from "@/components/Footer";
import WalletHeader from "@/components/wallet/WalletHeader";
import IrtWalletCard from "@/components/wallet/IrtWalletCard";
import AssetWalletCard from "@/components/wallet/AssetWalletCard";
import CreditWalletCard from "@/components/wallet/CreditWalletCard";
import WalletEmptyBanner from "@/components/wallet/WalletEmptyBanner";
import UnapprovedAccountCard from "@/components/wallet/UnapprovedAccountCard";
import { brandConfig } from "@/config/brand.config";
import type { WalletItem } from "@/lib/api/wallet-client";
import {
  invalidateSessionCache,
  setSessionCache,
  useSession,
} from "@/lib/auth/use-session";

export default function WalletPage() {
  const router = useRouter();
  const { user: sessionUser, loading: sessionLoading, refresh: refreshSession } =
    useSession();

  const [wallets, setWallets] = useState<WalletItem[]>([]);
  const [walletsLoading, setWalletsLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<string>("");

  const fetchBalances = useCallback(async () => {
    try {
      const walletRes = await fetch("/api/wallet/balances", {
        cache: "no-store",
      });

      if (walletRes.status === 401) {
        invalidateSessionCache();
        router.push("/login");
        return;
      }

      const walletData = await walletRes.json().catch(() => null);

      if (walletRes.status === 403 && walletData?.code === "ACCOUNT_NOT_ACTIVE") {
        if (sessionUser) {
          setSessionCache({
            ...sessionUser,
            status: walletData.status || "Suspended",
          });
        }
        setWallets([]);
        setError(null);
        return;
      }

      if (walletRes.ok && walletData?.success) {
        setWallets(Array.isArray(walletData.wallets) ? walletData.wallets : []);
        setLastUpdated(
          walletData.summary?.timestamp || new Date().toISOString()
        );
        setError(null);
      } else {
        setError(
          walletData?.message || "خطا در بارگذاری اطلاعات کیف پول."
        );
      }
    } catch {
      setError(
        "خطا در برقراری ارتباط با سرویس کیف پول. لطفاً اتصال اینترنت خود را بررسی کرده و دوباره تلاش کنید."
      );
    } finally {
      setWalletsLoading(false);
      setRefreshing(false);
    }
  }, [router, sessionUser]);

  useEffect(() => {
    if (sessionLoading) return;

    if (!sessionUser) {
      router.push("/login");
      return;
    }

    if (sessionUser.status !== "Approved") {
      setWalletsLoading(false);
      return;
    }

    void fetchBalances();
  }, [sessionLoading, sessionUser, fetchBalances, router]);

  const handleRefresh = async () => {
    setRefreshing(true);
    const freshUser = await refreshSession();
    if (!freshUser) {
      setRefreshing(false);
      router.push("/login");
      return;
    }
    if (freshUser.status !== "Approved") {
      setWalletsLoading(false);
      setRefreshing(false);
      return;
    }
    await fetchBalances();
  };

  const loading = sessionLoading || (sessionUser?.status === "Approved" && walletsLoading);

  const irtWallet = wallets.find((w) => w.asset.toUpperCase() === "IRT");
  const assetWallets = wallets.filter(
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

      <main
        id="main-content"
        className="relative flex-1 py-12 px-4 sm:px-6 lg:px-8 max-w-[1200px] mx-auto w-full"
      >
        <WalletHeader
          sessionUser={sessionUser}
          lastUpdated={lastUpdated}
          refreshing={refreshing}
          loading={loading}
          onRefresh={() => void handleRefresh()}
        />

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
          <UnapprovedAccountCard
            status={sessionUser.status}
            onRefresh={() => void handleRefresh()}
          />
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
            {hasNoBalance && <WalletEmptyBanner />}

            {/* Primary Balances Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              <IrtWalletCard wallet={irtWallet} />

              {assetWallets.map((wallet) => (
                <AssetWalletCard key={wallet.asset} wallet={wallet} />
              ))}

              {creditWallets.map((wallet) => (
                <CreditWalletCard key={wallet.asset} wallet={wallet} />
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
            <div className="zar-panel p-6 sm:p-8 flex flex-col sm:flex-row items-center justify-between gap-6">
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
