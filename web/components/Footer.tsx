import Link from "next/link";
import { ShieldCheck, Scale, Send, Compass } from "lucide-react";
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
              زیرساخت معاملاتی و دفترکل دقیق برای معاملات شفاف طلای آبشده بین
              طلافروشی‌ها و مشتریان تاییدشده آن‌هاست؛ با معماری ماژولار و نقشه
              راه تدریجی برای توسعه به سمت زیست‌بوم دارایی‌های واقعی و دیجیتال.
            </p>
            <div className="flex items-center gap-4 text-xs pt-2 flex-wrap">
              <span className="flex items-center gap-1.5 text-[#48B884]">
                <ShieldCheck className="h-4 w-4" />
                شفافیت طرف حساب و تسویه
              </span>
              <span className="flex items-center gap-1.5 text-[#C9A45A]">
                <Scale className="h-4 w-4" />
                کارمزد ۰٪ (اسپرد شفاف)
              </span>
              <Link
                href="/vision"
                className="flex items-center gap-1.5 text-[#4CB3A5] hover:underline"
              >
                <Compass className="h-4 w-4" />
                چشم‌انداز اکوسیستم
              </Link>
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
                  href="/#platform"
                  className="hover:text-[#C9A45A] transition-colors"
                >
                  زیرساخت معاملات طلا
                </Link>
              </li>
              <li>
                <Link
                  href="/#transparency"
                  className="hover:text-[#C9A45A] transition-colors"
                >
                  شفافیت و دفترکل
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
                  href="/vision"
                  className="hover:text-[#C9A45A] transition-colors"
                >
                  چشم‌انداز و نقشه راه
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
              کانال‌های رسمی تلگرام
            </h4>
            <p className="text-xs text-[#9CA3AF] mb-3 leading-relaxed">
              دسترسی به سامانه معاملاتی و اطلاعیه‌های رسمی{" "}
              <span className="bidi-isolate text-[#F2EEE5]">
                {brandConfig.name.en}
              </span>
              :
            </p>
            <div className="flex flex-col gap-2.5">
              <a
                href={brandConfig.telegram.botUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center justify-center gap-2 rounded-[10px] bg-[#286B63] px-4 py-2.5 text-xs font-semibold text-white hover:bg-[#328076] transition-colors"
              >
                <Send className="h-4 w-4" />
                <span className="bidi-isolate">
                  @{brandConfig.telegram.botUsername}
                </span>
              </a>
              <a
                href={brandConfig.telegram.channelUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center justify-center gap-2 rounded-[10px] border border-[#6B7485] px-4 py-2 text-xs font-medium text-[#F2EEE5] hover:border-[#C9A45A] hover:text-[#C9A45A] transition-colors"
              >
                <span className="bidi-isolate">
                  @{brandConfig.telegram.channelUsername}
                </span>
              </a>
            </div>
          </div>
        </div>

        <div className="mt-12 pt-8 border-t border-[#2B303A] flex flex-col sm:flex-row items-center justify-between text-xs text-[#9CA3AF] gap-4">
          <p className="flex items-center gap-2 flex-wrap">
            <span>{brandConfig.name.ecosystemFa}</span>
            <span className="text-[#2B303A]">•</span>
            <span className="bidi-isolate text-[#C9A45A]">
              {brandConfig.domain.primary}
            </span>
            <span className="text-[#2B303A]">/</span>
            <span className="bidi-isolate">{brandConfig.domain.secondary}</span>
          </p>
          <p className="bidi-isolate text-[11px] text-[#9CA3AF]">
            {brandConfig.slogan.en}
          </p>
        </div>
      </div>
    </footer>
  );
}
