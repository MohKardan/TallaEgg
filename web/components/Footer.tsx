import Link from "next/link";
import { Shield, Sparkles, Send, Lock } from "lucide-react";
import BrandLogo from "@/components/BrandLogo";
import { brandConfig } from "@/config/brand.config";

export default function Footer() {
  return (
    <footer className="border-t border-amber-500/10 bg-[#06080c] py-14 text-slate-400">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-10">
          {/* Brand info */}
          <div className="md:col-span-2 space-y-4">
            <BrandLogo variant="full" size="md" />
            <p className="text-sm leading-relaxed text-slate-400 max-w-md font-light">
              {brandConfig.name.fa} ({brandConfig.name.en}) — پلتفرم نوین و مدار یکپارچه معاملات آنلاین طلای فیزیکی و دیجیتال بر پایه مدل بازارساز اختصاصی. بدون کارمزد با کمترین اسپرد قیمت، تسویه آنی دفترکل حساب‌ها و دسترسی ۲۴ ساعته از طریق پورتال وب و ربات تلگرام.
            </p>
            <div className="flex items-center gap-4 text-xs text-slate-400 pt-2 flex-wrap">
              <span className="flex items-center gap-1.5 text-emerald-400/90">
                <Shield className="h-4 w-4" />
                دفترکل امن و تراز
              </span>
              <span className="flex items-center gap-1.5 text-amber-400/90">
                <Sparkles className="h-4 w-4" />
                کارمزد صفر درصد
              </span>
              <span className="flex items-center gap-1.5 text-sky-400/90">
                <Lock className="h-4 w-4" />
                احراز هویت دومرحله‌ای تلگرام
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
              ثبت سفارش، احراز هویت و پشتیبانی برخط ۲۴ ساعته از طریق ربات رسمی {brandConfig.name.fa}:
            </p>
            <a
              href={brandConfig.telegram.botUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-2 rounded-lg bg-amber-500/10 border border-amber-500/30 px-4 py-2.5 text-xs font-medium text-amber-300 hover:bg-amber-500/20 transition-all"
            >
              <Send className="h-4 w-4" />
              <span>ورود به ربات @{brandConfig.telegram.botUsername}</span>
            </a>
          </div>
        </div>

        <div className="mt-12 pt-8 border-t border-slate-800/80 flex flex-col sm:flex-row items-center justify-between text-xs text-slate-400 gap-4">
          <p>© تمامی حقوق برای {brandConfig.name.legalFa} ({brandConfig.domain.primary}) محفوظ است.</p>
          <p className="text-[11px] text-slate-400">
            {brandConfig.slogan.en} — زیرساخت معاملاتی نوین طلای آبشده ۱۸ عیار.
          </p>
        </div>
      </div>
    </footer>
  );
}
