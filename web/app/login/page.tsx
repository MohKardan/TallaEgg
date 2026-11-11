"use client";

import Navbar from "@/components/Navbar";
import Footer from "@/components/Footer";
import { useEffect, useState, useRef } from "react";
import { useRouter } from "next/navigation";
import { Send, Shield, AlertCircle, CheckCircle2, Lock, WifiOff } from "lucide-react";
import { brandConfig } from "@/config/brand.config";
import { BrandEmblem } from "@/components/BrandLogo";

declare global {
  interface Window {
    onTelegramAuth?: (user: any) => void;
  }
}

export default function LoginPage() {
  const router = useRouter();
  const widgetContainerRef = useRef<HTMLDivElement>(null);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [userStatus, setUserStatus] = useState<string | null>(null);
  const [widgetFailed, setWidgetFailed] = useState(false);

  const botUsername =
    process.env.NEXT_PUBLIC_TELEGRAM_BOT_USERNAME ||
    brandConfig.telegram.botUsername;

  // Check if already authenticated
  useEffect(() => {
    fetch("/api/auth/me")
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data?.authenticated) {
          router.push("/wallet");
        }
      })
      .catch(() => {});
  }, [router]);

  // Set up Telegram widget callback
  useEffect(() => {
    window.onTelegramAuth = async (user: any) => {
      setLoading(true);
      setError(null);
      setStatusMessage(null);
      setUserStatus(null);

      try {
        const res = await fetch("/api/auth/telegram", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(user),
        });

        const data = await res.json();

        if (res.ok && data.success) {
          setStatusMessage("ورود با موفقیت انجام شد. در حال انتقال به کیف پول...");
          setTimeout(() => {
            router.push("/wallet");
          }, 800);
        } else {
          setError(data.message || "خطا در احراز هویت تلگرام.");
          if (data.status) {
            setUserStatus(data.status);
          }
        }
      } catch (err: any) {
        setError("خطا در برقراری ارتباط با سرور: " + err.message);
      } finally {
        setLoading(false);
      }
    };

    // Dynamically inject Telegram widget script into container
    if (widgetContainerRef.current) {
      widgetContainerRef.current.innerHTML = "";
      const script = document.createElement("script");
      script.src = "https://telegram.org/js/telegram-widget.js?22";
      script.setAttribute("data-telegram-login", botUsername);
      script.setAttribute("data-size", "large");
      script.setAttribute("data-radius", "12");
      script.setAttribute("data-onauth", "onTelegramAuth(user)");
      script.setAttribute("data-request-access", "write");
      script.async = true;

      // Handle Telegram CDN load errors
      script.onerror = () => {
        setWidgetFailed(true);
      };

      widgetContainerRef.current.appendChild(script);

      // Watchdog timeout: if widget iframe does not appear within 6 seconds
      const watchdogTimer = setTimeout(() => {
        if (
          widgetContainerRef.current &&
          !widgetContainerRef.current.querySelector("iframe")
        ) {
          setWidgetFailed(true);
        }
      }, 6000);

      return () => {
        clearTimeout(watchdogTimer);
        delete window.onTelegramAuth;
      };
    }

    return () => {
      delete window.onTelegramAuth;
    };
  }, [botUsername, router]);

  return (
    <>
      <Navbar />

      <main className="relative flex-1 flex items-center justify-center py-16 px-4 sm:px-6 lg:px-8">
        <div className="ambient-glow top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 opacity-40" />

        <div className="relative w-full max-w-md mx-auto">
          <div className="glass-card rounded-3xl p-8 sm:p-10 border border-amber-500/25 shadow-goldGlow">
            {/* Header */}
            <div className="text-center space-y-3 mb-8">
              <div className="flex justify-center mb-3">
                <BrandEmblem sizePx={88} useLargeLogo priority />
              </div>
              <h1 className="text-2xl font-extrabold text-white">
                ورود به پورتال {brandConfig.name.fa}
              </h1>
              <p className="text-xs text-amber-400/90 font-medium tracking-wide">
                {brandConfig.slogan.en}
              </p>
              <p className="text-xs text-slate-400 font-light leading-relaxed">
                ورود امن و مستقیم از طریق حساب کاربری تلگرام بدون نیاز به رمز عبور
              </p>
            </div>

            {/* Status & Error Alerts */}
            {statusMessage && (
              <div className="mb-6 rounded-xl bg-emerald-500/10 border border-emerald-500/30 p-4 text-xs text-emerald-300 flex items-start gap-3">
                <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-400" />
                <span>{statusMessage}</span>
              </div>
            )}

            {error && (
              <div className="mb-6 rounded-xl bg-rose-500/10 border border-rose-500/30 p-4 text-xs text-rose-300 space-y-2">
                <div className="flex items-start gap-3">
                  <AlertCircle className="h-5 w-5 shrink-0 text-rose-400" />
                  <span className="font-medium leading-relaxed">{error}</span>
                </div>

                {userStatus === "Pending" && (
                  <div className="mt-2 pt-2 border-t border-rose-500/20 text-[11px] text-slate-300">
                    💡 حساب شما هنوز به مرحله تأیید مدیر نرسیده است. برای پیگیری تأیید هویت می‌توانید به پشتیبانی در ربات تلگرام پیام دهید.
                  </div>
                )}
              </div>
            )}

            {/* Telegram Login Widget Container */}
            <div className="my-8 flex flex-col items-center justify-center space-y-4">
              <span className="text-xs text-slate-400 mb-1">
                روی دکمه زیر کلیک کرده و با حساب تلگرام خود وارد شوید:
              </span>

              <div
                ref={widgetContainerRef}
                className="flex items-center justify-center min-h-[48px]"
              >
                {/* Telegram Script injected here */}
              </div>

              {/* Script failure fallback for filtered networks */}
              {widgetFailed && (
                <div className="w-full rounded-xl bg-amber-500/10 border border-amber-500/30 p-4 text-xs text-amber-300 space-y-2 text-right">
                  <div className="flex items-start gap-2.5">
                    <WifiOff className="h-4 w-4 shrink-0 text-amber-400 mt-0.5" />
                    <p className="leading-relaxed font-light">
                      دکمه تلگرام بارگذاری نشد؟ به دلیل محدودیت‌های شبکه اینترنت کشور روی دامنه تلگرام، لطفاً اتصال اینترنت خود یا ابزار گذر از تحریم را بررسی کنید، یا مستقیماً از طریق ربات تلگرام وارد شوید.
                    </p>
                  </div>
                </div>
              )}

              {loading && (
                <div className="flex items-center gap-2 text-xs text-amber-400 pt-2 animate-pulse">
                  <div className="h-3 w-3 rounded-full border-2 border-amber-400 border-t-transparent animate-spin" />
                  <span>در حال اعتبارسنجی اطلاعات نشست...</span>
                </div>
              )}
            </div>

            {/* Steps & Guidance */}
            <div className="border-t border-slate-800 pt-6 space-y-3 text-xs text-slate-400">
              <div className="flex items-center gap-2 font-medium text-slate-300">
                <Shield className="h-4 w-4 text-amber-400" />
                <span>فرآیند ورود امن به {brandConfig.name.fa}:</span>
              </div>
              <ul className="space-y-1.5 pr-6 list-disc list-outside text-[11px] text-slate-400 font-light">
                <li>ورود تنها برای حساب‌های فعال و تأییدشده در ربات مجاز است.</li>
                <li>نشست کاربری به شکل کوکی فوق‌امنیتی (httpOnly) ذخیره می‌شود.</li>
                <li>شناسه کاربری شما هرگز در مرورگر ذخیره یا افشا نمی‌گردد.</li>
              </ul>
            </div>

            {/* Link to Bot for Registration */}
            <div className="mt-8 pt-6 border-t border-slate-800 text-center">
              <p className="text-xs text-slate-400 mb-3">هنوز در سامانه ثبت‌نام نکرده‌اید؟</p>
              <a
                href={`https://t.me/${botUsername}`}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center justify-center gap-2 w-full rounded-xl border border-amber-500/30 bg-amber-500/10 py-3 text-xs font-semibold text-amber-300 hover:bg-amber-500/20 transition-colors"
              >
                <Send className="h-4 w-4" />
                <span>ثبت‌نام از طریق ربات @{botUsername}</span>
              </a>
            </div>
          </div>
        </div>
      </main>

      <Footer />
    </>
  );
}
