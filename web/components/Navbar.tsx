"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Wallet, LogIn, LogOut, Send, Menu, X } from "lucide-react";
import BrandLogo from "@/components/BrandLogo";
import LiveTicker from "@/components/LiveTicker";
import { brandConfig } from "@/config/brand.config";

interface UserProfile {
  firstName?: string;
  lastName?: string;
  username?: string;
  status?: string;
}

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

  const handleLogout = async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    setUser(null);
    window.location.href = "/";
  };

  return (
    <>
      <LiveTicker />
      <header className="sticky top-0 z-50 w-full border-b border-[#2B303A] bg-[#17191D]/95 backdrop-blur-md">
        <div className="mx-auto flex h-20 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
          <BrandLogo variant="full" size="md" />

          <nav className="hidden md:flex items-center gap-7 text-sm font-medium text-[#9CA3AF]">
            <Link
              href="/#ecosystem"
              className="hover:text-[#F2EEE5] transition-colors"
            >
              اکوسیستم دارایی‌ها
            </Link>
            <Link
              href="/#rates"
              className="hover:text-[#F2EEE5] transition-colors"
            >
              نرخ مرجع طلا (RWA)
            </Link>
            <Link
              href="/#calculator"
              className="hover:text-[#F2EEE5] transition-colors"
            >
              محاسبه‌گر ارزش
            </Link>
            <Link
              href="/#governance"
              className="hover:text-[#F2EEE5] transition-colors"
            >
              شفافیت و DAO
            </Link>
          </nav>

          <div className="flex items-center gap-3">
            <a
              href={brandConfig.telegram.botUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="hidden sm:inline-flex items-center gap-2 rounded-[10px] border border-[#6B7485] px-3.5 py-2 text-xs font-medium text-[#F2EEE5] hover:border-[#4CB3A5] hover:text-[#4CB3A5] transition-colors"
            >
              <Send className="h-3.5 w-3.5 text-[#4CB3A5]" />
              <span>ربات تلگرام</span>
            </a>

            {!loading && user ? (
              <div className="flex items-center gap-2">
                <Link
                  href="/wallet"
                  className="inline-flex items-center gap-2 rounded-[10px] bg-[#C9A45A] px-4 py-2 text-xs font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
                >
                  <Wallet className="h-4 w-4" />
                  <span>کیف پول</span>
                </Link>
                <button
                  onClick={handleLogout}
                  aria-label="خروج از حساب کاربری"
                  title="خروج از حساب کاربری"
                  className="inline-flex items-center justify-center h-9 w-9 rounded-[10px] border border-[#6B7485] text-[#9CA3AF] hover:text-[#F07F83] hover:border-[#F07F83] transition-colors"
                >
                  <LogOut className="h-4 w-4" />
                </button>
              </div>
            ) : (
              <Link
                href="/login"
                className="inline-flex items-center gap-2 rounded-[10px] bg-[#C9A45A] px-4 py-2 text-xs font-bold text-[#17191D] hover:bg-[#D4B16A] transition-colors"
              >
                <LogIn className="h-4 w-4" />
                <span>ورود به پنل</span>
              </Link>
            )}

            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              aria-label="باز کردن منوی موبایل"
              className="md:hidden inline-flex items-center justify-center h-9 w-9 rounded-[10px] border border-[#6B7485] text-[#F2EEE5] hover:border-[#C9A45A]"
            >
              {mobileMenuOpen ? (
                <X className="h-5 w-5" />
              ) : (
                <Menu className="h-5 w-5" />
              )}
            </button>
          </div>
        </div>

        {mobileMenuOpen && (
          <div className="md:hidden border-b border-[#2B303A] bg-[#1F2329] px-4 py-4 space-y-3">
            <Link
              href="/#ecosystem"
              onClick={() => setMobileMenuOpen(false)}
              className="block text-sm text-[#F2EEE5] hover:text-[#C9A45A] py-1"
            >
              اکوسیستم دارایی‌ها
            </Link>
            <Link
              href="/#rates"
              onClick={() => setMobileMenuOpen(false)}
              className="block text-sm text-[#F2EEE5] hover:text-[#C9A45A] py-1"
            >
              نرخ مرجع طلا (RWA)
            </Link>
            <Link
              href="/#calculator"
              onClick={() => setMobileMenuOpen(false)}
              className="block text-sm text-[#F2EEE5] hover:text-[#C9A45A] py-1"
            >
              محاسبه‌گر ارزش
            </Link>
            <Link
              href="/#governance"
              onClick={() => setMobileMenuOpen(false)}
              className="block text-sm text-[#F2EEE5] hover:text-[#C9A45A] py-1"
            >
              شفافیت و DAO
            </Link>
          </div>
        )}
      </header>
    </>
  );
}
