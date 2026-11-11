import Link from "next/link";
import { ShieldCheck, Layers, Send, Users } from "lucide-react";
import BrandLogo from "@/components/BrandLogo";
import { brandConfig } from "@/config/brand.config";

export default function Footer() {
  return (
    <footer className="border-t border-[#2B303A] bg-[#101215] py-14 text-[#9CA3AF]">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-10">
          {/* Brand info */}
          <div className="md:col-span-2 space-y-4">
            <BrandLogo variant="full" size="md" />
            <p className="text-sm leading-relaxed text-[#9CA3AF] max-w-lg font-light">
              <span className="text-[#F2EEE5] font-semibold">
                {brandConfig.name.en} ({brandConfig.name.fa})
              </span>{" "}
              یک زیست‌بوم روبه‌توسعه برای اتصال شفاف دارایی‌های دنیای واقعی
              (با نقطه شروع طلای ۱۸ عیار) و دارایی‌های دیجیتال شامل رمزارزها و
              توکن‌های غیرمثلی (NFT) است که با رویکرد جامعه‌محور به سوی
              حکمرانی تدریجی DAO حرکت می‌کند.
            </p>
            <div className="flex items-center gap-4 text-xs pt-2 flex-wrap">
              <span className="flex items-center gap-1.5 text-[#48B884]">
                <ShieldCheck className="h-4 w-4" />
                تفکیک صریح دارایی و حقوق
              </span>
              <span className="flex items-center gap-1.5 text-[#C9A45A]">
                <Layers className="h-4 w-4" />
                RWA + Crypto &amp; NFTs
              </span>
              <span className="flex items-center gap-1.5 text-[#4CB3A5]">
                <Users className="h-4 w-4" />
                مسیر تدریجی DAO
              </span>
            </div>
          </div>

          {/* Quick links */}
          <div>
            <h4 className="text-sm font-bold text-[#F2EEE5] mb-4">
              بخش‌های پلتفرم
            </h4>
            <ul className="space-y-2.5 text-sm">
              <li>
                <Link
                  href="/#ecosystem"
                  className="hover:text-[#C9A45A] transition-colors"
                >
                  ساختار اکوسیستم
                </Link>
              </li>
              <li>
                <Link
                  href="/#rates"
                  className="hover:text-[#C9A45A] transition-colors"
                >
                  نرخ مرجع طلای آبشده
                </Link>
              </li>
              <li>
                <Link
                  href="/wallet"
                  className="hover:text-[#C9A45A] transition-colors"
                >
                  کیف پول دفترکل
                </Link>
              </li>
              <li>
                <Link
                  href="/login"
                  className="hover:text-[#C9A45A] transition-colors"
                >
                  ورود با تلگرام
                </Link>
              </li>
            </ul>
          </div>

          {/* Telegram integration */}
          <div>
            <h4 className="text-sm font-bold text-[#F2EEE5] mb-4">
              دسترسی و مشارکت
            </h4>
            <p className="text-xs text-[#9CA3AF] mb-3 leading-relaxed">
              احراز هویت پایه و دسترسی آزمایشی به ماژول دارایی طلا از طریق
              ربات رسمی {brandConfig.name.en} انجام می‌شود:
            </p>
            <a
              href={brandConfig.telegram.botUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-2 rounded-[10px] bg-[#286B63] px-4 py-2.5 text-xs font-semibold text-white hover:bg-[#328076] transition-colors"
            >
              <Send className="h-4 w-4" />
              <span className="bidi-isolate">@{brandConfig.telegram.botUsername}</span>
            </a>
          </div>
        </div>

        <div className="mt-12 pt-8 border-t border-[#2B303A] flex flex-col sm:flex-row items-center justify-between text-xs text-[#9CA3AF] gap-4">
          <p>
            {brandConfig.name.ecosystemFa} (
            <span className="bidi-isolate">{brandConfig.domain.primary}</span>)
          </p>
          <p className="bidi-isolate text-[11px] text-[#9CA3AF]">
            {brandConfig.slogan.en}
          </p>
        </div>
      </div>
    </footer>
  );
}
