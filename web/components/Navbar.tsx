"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Wallet, LogIn, LogOut, Send, ShieldCheck } from "lucide-react";

interface UserProfile {
  firstName?: string;
  lastName?: string;
  username?: string;
  status?: string;
}

export default function Navbar() {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);

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
    <header className="sticky top-0 z-50 w-full border-b border-amber-500/10 bg-[#080a0f]/80 backdrop-blur-xl">
      <div className="mx-auto flex h-20 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
        {/* Brand Logo */}
        <Link href="/" className="flex items-center gap-3 group">
          <div className="relative flex h-11 w-11 items-center justify-center rounded-xl bg-gradient-to-br from-amber-400 via-amber-600 to-yellow-800 p-[1px] shadow-goldGlowSubtle group-hover:shadow-goldGlow transition-all">
            <div className="flex h-full w-full items-center justify-center rounded-[11px] bg-[#0d1017]">
              <span className="text-xl font-black text-amber-400 tracking-tighter">TE</span>
            </div>
          </div>
          <div className="flex flex-col">
            <span className="text-xl font-bold tracking-tight text-white group-hover:text-amber-400 transition-colors">
              طلااگ <span className="text-xs font-medium text-amber-500/80 mr-1">TallaEgg</span>
            </span>
            <span className="text-[11px] text-slate-400 -mt-1 font-light">
              معاملات آنی طلای آبشده
            </span>
          </div>
        </Link>

        {/* Center Nav Links */}
        <nav className="hidden md:flex items-center gap-8 text-sm font-medium text-slate-300">
          <Link href="/#rates" className="hover:text-amber-400 transition-colors">
            مظنه لحظه‌ای
          </Link>
          <Link href="/#calculator" className="hover:text-amber-400 transition-colors">
            ماشین‌حساب طلا
          </Link>
          <Link href="/#features" className="hover:text-amber-400 transition-colors">
            مزایای معامله
          </Link>
          <Link href="/#guide" className="hover:text-amber-400 transition-colors">
            راهنمای شروع
          </Link>
        </nav>

        {/* Action Buttons */}
        <div className="flex items-center gap-3">
          <a
            href="https://t.me/TallaEgg_Bot"
            target="_blank"
            rel="noopener noreferrer"
            className="hidden sm:inline-flex items-center gap-2 rounded-lg border border-amber-500/30 px-3.5 py-2 text-xs font-medium text-amber-300 hover:bg-amber-500/10 transition-colors"
          >
            <Send className="h-3.5 w-3.5" />
            <span>ربات تلگرام</span>
          </a>

          {!loading && user ? (
            <div className="flex items-center gap-2">
              <Link
                href="/wallet"
                className="inline-flex items-center gap-2 rounded-lg bg-gradient-to-r from-amber-500 to-amber-600 px-4 py-2 text-xs font-semibold text-slate-950 shadow-goldGlowSubtle hover:brightness-110 transition-all"
              >
                <Wallet className="h-4 w-4" />
                <span>کیف پول</span>
              </Link>
              <button
                onClick={handleLogout}
                title="خروج از حساب کاربری"
                className="inline-flex items-center justify-center h-8 w-8 rounded-lg border border-slate-700/60 text-slate-400 hover:text-rose-400 hover:border-rose-500/30 transition-colors"
              >
                <LogOut className="h-4 w-4" />
              </button>
            </div>
          ) : (
            <Link
              href="/login"
              className="inline-flex items-center gap-2 rounded-lg bg-gradient-to-r from-amber-500 to-amber-600 px-4 py-2 text-xs font-semibold text-slate-950 shadow-goldGlowSubtle hover:brightness-110 transition-all"
            >
              <LogIn className="h-4 w-4" />
              <span>ورود با تلگرام</span>
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}
