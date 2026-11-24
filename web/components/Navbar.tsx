"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Wallet, LogIn, LogOut, Send, Menu, X } from "lucide-react";
import BrandLogo from "@/components/BrandLogo";
import { brandConfig } from "@/config/brand.config";

interface UserProfile {
  firstName?: string;
  lastName?: string;
  username?: string;
  status?: string;
}

const NAV_LINKS = [
  { href: "/#platform", label: "دفترکل و امکانات" },
  { href: "/#calculator", label: "محاسبه‌گر طلا" },
  { href: "/#transparency", label: "شفافیت و مرزها" },
  { href: "/vision", label: "چشم‌انداز" },
];

export default function Navbar() {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  useEffect(() => {
    fetch("/api/auth/me")
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (data?.authenticated && data?.user) {
          setUser(data.user);
        }
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!mobileMenuOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setMobileMenuOpen(false);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [mobileMenuOpen]);

  const handleLogout = async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    setUser(null);
    window.location.href = "/";
  };

  return (
    <header className="sticky top-0 z-50 w-full border-b border-white/[0.08] bg-[#17191D]/90 backdrop-blur-md">
      <div className="mx-auto flex h-[72px] max-w-[1200px] items-center justify-between px-4 sm:px-6 lg:px-8">
        <div className="flex items-center gap-3">
          <BrandLogo variant="minimal" size="md" />
          <span className="hidden lg:inline-flex items-center gap-1.5 rounded-full bg-[#162A24] border border-[#286B63]/80 px-2.5 py-0.5 text-[11px] font-semibold text-[#48B884]">
            <span className="h-1.5 w-1.5 rounded-full bg-[#48B884]" />
            پایلوت طلا
          </span>
        </div>

        <nav
          aria-label="منوی اصلی"
          className="hidden md:flex items-center gap-1 text-sm font-medium text-[#F2EEE5]/75"
        >
          {NAV_LINKS.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className="rounded-[10px] px-3.5 py-2 hover:bg-white/[0.04] hover:text-[#F2EEE5] transition-colors"
            >
              {item.label}
            </Link>
          ))}
        </nav>

        <div className="flex items-center gap-2.5">
          <a
            href={brandConfig.telegram.botUrl}
            target="_blank"
            rel="noopener noreferrer"
            aria-label="شروع در ربات تلگرام"
            title={`ربات تلگرام @${brandConfig.telegram.botUsername}`}
            className="hidden sm:inline-flex h-10 w-10 items-center justify-center rounded-[10px] border border-white/[0.1] bg-[#1F2329] text-[#4CB3A5] hover:border-[#4CB3A5]/60 hover:bg-[#252A32] transition-colors"
          >
            <Send className="h-4 w-4" />
          </a>

          {!loading && user ? (
            <div className="flex items-center gap-2">
              <Link
                href="/wallet"
                className="inline-flex h-10 items-center gap-2 rounded-[10px] bg-[#C9A45A] px-4 text-sm font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
              >
                <Wallet className="h-4 w-4" />
                <span>کیف پول</span>
              </Link>
              <button
                type="button"
                onClick={handleLogout}
                aria-label="خروج از حساب کاربری"
                title="خروج از حساب کاربری"
                className="inline-flex h-10 w-10 items-center justify-center rounded-[10px] border border-white/[0.1] text-[#9CA3AF] hover:text-[#F07F83] hover:border-[#F07F83]/60 transition-colors"
              >
                <LogOut className="h-4 w-4" />
              </button>
            </div>
          ) : (
            <Link
              href="/login"
              className="inline-flex h-10 items-center gap-2 rounded-[10px] bg-[#C9A45A] px-4 text-sm font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
            >
              <LogIn className="h-4 w-4" />
              <span>ورود به پنل</span>
            </Link>
          )}

          <button
            type="button"
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
            aria-label={mobileMenuOpen ? "بستن منوی موبایل" : "باز کردن منوی موبایل"}
            aria-expanded={mobileMenuOpen}
            aria-controls="mobile-nav"
            className="md:hidden inline-flex h-10 w-10 items-center justify-center rounded-[10px] border border-[#6B7485] text-[#F2EEE5] hover:border-[#C9A45A]"
          >
            {mobileMenuOpen ? (
              <X className="h-5 w-5" aria-hidden="true" />
            ) : (
              <Menu className="h-5 w-5" aria-hidden="true" />
            )}
          </button>
        </div>
      </div>

      {mobileMenuOpen && (
        <div
          id="mobile-nav"
          className="md:hidden border-b border-white/[0.08] bg-[#1F2329] px-4 py-4 space-y-2"
        >
          {NAV_LINKS.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              onClick={() => setMobileMenuOpen(false)}
              className="block rounded-[10px] px-3 py-2.5 text-sm font-medium text-[#F2EEE5] hover:bg-white/[0.04] hover:text-[#C9A45A]"
            >
              {item.label}
            </Link>
          ))}
          <a
            href={brandConfig.telegram.botUrl}
            target="_blank"
            rel="noopener noreferrer"
            onClick={() => setMobileMenuOpen(false)}
            className="flex items-center gap-2 rounded-[10px] border border-white/[0.1] px-3 py-2.5 text-sm font-medium text-[#4CB3A5]"
          >
            <Send className="h-4 w-4" />
            <span>ربات تلگرام (@{brandConfig.telegram.botUsername})</span>
          </a>
        </div>
      )}
    </header>
  );
}
