"use client";

import Navbar from "@/components/Navbar";
import Footer from "@/components/Footer";
import { useEffect, useState, useRef } from "react";
import { useRouter } from "next/navigation";
import { Send, Shield, AlertCircle, CheckCircle2, WifiOff } from "lucide-react";
import { brandConfig } from "@/config/brand.config";
import { ZarorbitIntegratedLockupSvg } from "@/components/BrandLogo";

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
          setStatusMessage(
            "ورود با موفقیت انجام شد. در حال انتقال به کیف پول..."
          );
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

      script.onerror = () => {
        setWidgetFailed(true);
      };

      widgetContainerRef.current.appendChild(script);

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
        <div
          className="absolute inset-0 zar-grid-backdrop opacity-25 pointer-events-none"
          aria-hidden="true"
        />

        <div className="relative w-full max-w-md mx-auto">
          <div className="zar-card p-8 sm:p-10 shadow-card">
            {/* Header */}
            <div className="text-center space-y-3 mb-8">
              <div className="flex justify-center mb-2">
                <ZarorbitIntegratedLockupSvg heightPx={40} />
              </div>
              <h1 className="text-xl font-extrabold text-[#F2EEE5]">
                ورود به پورتال {brandConfig.name.fa}
              </h1>
              <p className="bidi-isolate text-xs text-[#C9A45A] font-medium">
                {brandConfig.slogan.en}
              </p>
              <p className="text-xs text-[#9CA3AF] font-light leading-relaxed">
                ورود امن و مستقیم از طریق حساب کاربری تلگرام بدون نیاز به رمز
                عبور جداگانه
              </p>
            </div>

            {/* Status & Error Alerts */}
            {statusMessage && (
              <div className="mb-6 rounded-[10px] bg-[#162A24] border border-[#286B63] p-4 text-xs text-[#48B884] flex items-start gap-3">
                <CheckCircle2 className="h-5 w-5 shrink-0" />
                <span>{statusMessage}</span>
              </div>
            )}

            {error && (
              <div className="mb-6 rounded-[10px] bg-[#2D1B1E] border border-[#6E3438] p-4 text-xs text-[#F07F83] space-y-2">
                <div className="flex items-start gap-3">
                  <AlertCircle className="h-5 w-5 shrink-0" />
                  <span className="font-medium leading-relaxed">{error}</span>
                </div>

                {userStatus === "Pending" && (
                  <div className="mt-2 pt-2 border-t border-[#6E3438] text-[11px] text-[#F2EEE5]">
                    حساب شما هنوز به مرحله تأیید نرسیده است. برای پیگیری
                    تأیید پایه می‌توانید به ربات رسمی پیام دهید.
                  </div>
                )}
              </div>
            )}

            {/* Telegram Login Widget Container */}
            <div className="my-8 flex flex-col items-center justify-center space-y-4">
              <span className="text-xs text-[#9CA3AF] mb-1">
                روی دکمه زیر کلیک کرده و با حساب تلگرام خود وارد شوید:
              </span>

              <div
                ref={widgetContainerRef}
                className="flex items-center justify-center min-h-[48px]"
              />

              {widgetFailed && (
                <div className="w-full rounded-[10px] bg-[#2B2416] border border-[#6B501D] p-4 text-xs text-[#E2A93B] space-y-2 text-right">
                  <div className="flex items-start gap-2.5">
                    <WifiOff className="h-4 w-4 shrink-0 mt-0.5" />
                    <p className="leading-relaxed font-light text-[#F2EEE5]">
                      ویجت تلگرام بارگذاری نشد؟ اتصال اینترنت به دامنه تلگرام
                      را بررسی کنید یا از طریق ربات رسمی وارد شوید.
                    </p>
                  </div>
                </div>
              )}

              {loading && (
                <div className="flex items-center gap-2 text-xs text-[#C9A45A] pt-2">
                  <div className="h-3.5 w-3.5 rounded-full border-2 border-[#C9A45A] border-t-transparent animate-spin" />
                  <span>در حال اعتبارسنجی اطلاعات نشست...</span>
                </div>
              )}
            </div>

            {/* Security & Privacy Notes */}
            <div className="border-t border-[#2B303A] pt-6 space-y-3 text-xs text-[#9CA3AF]">
              <div className="flex items-center gap-2 font-medium text-[#F2EEE5]">
                <Shield className="h-4 w-4 text-[#4CB3A5]" />
                <span>حریم خصوصی و نشست امن {brandConfig.name.en}:</span>
              </div>
              <ul className="space-y-1.5 pr-6 list-disc list-outside text-[11px] text-[#9CA3AF] font-light">
                <li>
                  ورود تنها برای حساب‌های فعال و تأییدشده در ربات مجاز است.
                </li>
                <li>
                  نشست کاربری به‌صورت کوکی امن ({" "}
                  <span className="bidi-isolate font-mono">httpOnly</span> ) با
                  امضای رمزنگاری‌شده نگهداری می‌شود.
                </li>
                <li>اطلاعات غیرضروری در مرورگر ذخیره یا افشا نمی‌گردد.</li>
              </ul>
            </div>

            {/* Link to Bot for Registration */}
            <div className="mt-8 pt-6 border-t border-[#2B303A] text-center">
              <p className="text-xs text-[#9CA3AF] mb-3">
                هنوز حساب آزمایشی خود را ثبت نکرده‌اید؟
              </p>
              <a
                href={`https://t.me/${botUsername}`}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center justify-center gap-2 w-full rounded-[10px] border border-[#6B7485] bg-[#17191D] py-3 text-xs font-semibold text-[#F2EEE5] hover:border-[#4CB3A5] hover:text-[#4CB3A5] transition-colors"
              >
                <Send className="h-4 w-4 text-[#4CB3A5]" />
                <span>
                  ثبت‌نام در ربات{" "}
                  <span className="bidi-isolate">@{botUsername}</span>
                </span>
              </a>
            </div>
          </div>
        </div>
      </main>

      <Footer />
    </>
  );
}
