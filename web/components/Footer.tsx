import Link from "next/link";
import { Shield, Sparkles, Send, Lock } from "lucide-react";

export default function Footer() {
  return (
    <footer className="border-t border-amber-500/10 bg-[#06080c] py-14 text-slate-400">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-10">
          {/* Brand info */}
          <div className="md:col-span-2 space-y-4">
            <div className="flex items-center gap-3">
              <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-amber-500/10 border border-amber-500/30">
                <span className="font-black text-amber-400 text-sm">TE</span>
              </div>
              <span className="text-lg font-bold text-white tracking-wide">
                طلااگ <span className="text-xs text-amber-400">TallaEgg</span>
              </span>
            </div>
            <p className="text-sm leading-relaxed text-slate-400 max-w-md font-light">
              پلتفرم مدرن معاملات طلای آبشده ۱۸ عیار بر پایه مدل بازارساز اختصاصی (Dealer Model). بدون کارمزد با کمترین اسپرد قیمت، تسویه آنی دفترکل حساب‌ها و دسترسی دائمی از طریق ربات تلگرام و پورتال وب.
            </p>
            <div className="flex items-center gap-4 text-xs text-slate-400 pt-2">
              <span className="flex items-center gap-1.5 text-emerald-400/90">
                <Shield className="h-4 w-4" />
                دفترکل امن و تراز
              </span>
              <span className="flex items-center gap-1.5 text-amber-400/90">
                <Sparkles className="h-4 w-4" />
                معاملات با کارمزد صفر
              </span>
              <span className="flex items-center gap-1.5 text-sky-400/90">
                <Lock className="h-4 w-4" />
                احراز هویت دو مرحله‌ای
              </span>
            </div>
          </div>

          {/* Quick links */}
          <div>
            <h4 className="text-sm font-semibold text-white mb-4">دسترسی سریع</h4>
            <ul className="space-y-2.5 text-sm">
              <li>
                <Link href="/#rates" className="hover:text-amber-400 transition-colors">
                  مظنه لحظه‌ای آبشده
                </Link>
              </li>
              <li>
                <Link href="/#calculator" className="hover:text-amber-400 transition-colors">
                  محاسبه‌گر ارزش وزنی
                </Link>
              </li>
              <li>
                <Link href="/wallet" className="hover:text-amber-400 transition-colors">
                  کیف پول و موجودی
                </Link>
              </li>
              <li>
                <Link href="/login" className="hover:text-amber-400 transition-colors">
                  ورود به حساب کاربری
                </Link>
              </li>
            </ul>
          </div>

          {/* Contact / Telegram */}
          <div>
            <h4 className="text-sm font-semibold text-white mb-4">ارتباط و پشتیبانی</h4>
            <p className="text-xs text-slate-400 mb-3 leading-relaxed">
              ثبت سفارش، احراز هویت و پشتیبانی برخط ۲۴ ساعته از طریق ربات رسمی طلااگ:
            </p>
            <a
              href="https://t.me/TallaEgg_Bot"
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-2 rounded-lg bg-amber-500/10 border border-amber-500/30 px-4 py-2.5 text-xs font-medium text-amber-300 hover:bg-amber-500/20 transition-all"
            >
              <Send className="h-4 w-4" />
              <span>ورود به ربات @TallaEgg_Bot</span>
            </a>
          </div>
        </div>

        <div className="mt-12 pt-8 border-t border-slate-800/80 flex flex-col sm:flex-row items-center justify-between text-xs text-slate-400 gap-4">
          <p>© تمامی حقوق برای پلتفرم معاملاتی طلااگ (TallaEgg) محفوظ است.</p>
          <p className="text-[11px] text-slate-400">
            توسعه داده‌شده با استانداردهای نوین معماری نرم‌افزار و امنیت تراکنش‌های مالی.
          </p>
        </div>
      </div>
    </footer>
  );
}
