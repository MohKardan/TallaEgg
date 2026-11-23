import type { Metadata } from "next";
import Link from "next/link";
import Navbar from "@/components/Navbar";
import Footer from "@/components/Footer";
import { brandConfig } from "@/config/brand.config";
import { ZarorbitSymbolSvg } from "@/components/BrandLogo";
import {
  Coins,
  Layers,
  Gem,
  Users,
  Cpu,
  ShieldCheck,
  ChevronRight,
  Send,
} from "lucide-react";

export const metadata: Metadata = {
  title: "چشم‌انداز و نقشه راه اکوسیستم",
  description:
    "نقشه راه مرحله‌ای ZARORBIT از زیرساخت معاملات طلا به سوی زیست‌بوم دارایی‌های واقعی (RWA)، دارایی‌های دیجیتال و حکمرانی جامعه‌محور (DAO).",
};

const roadmapLayers = [
  {
    title: "لایه اول: معاملات طلای آبشده و دفترکل بازارگردان",
    subtitle: "نقطه شروع تجاری و عملیاتی امروز",
    status: "Active Pilot",
    statusStyle: "bg-[#162A24] border-[#286B63] text-[#48B884]",
    icon: Coins,
    iconWrap: "bg-[#2B2416] border-[#82611C] text-[#C9A45A]",
    description:
      "زیرساخت معاملات نقدی طلای ۱۸ عیار برای طلافروشان و مشتریان تاییدشده آن‌ها روی تلگرام و پورتال وب با دفترکل دقیق، اعتبار متقابل و کارمزد ۰.۰۰٪.",
    boundaries: [
      "طرف حساب و تسویه فیزیکی مستقیم توسط طلافروشی انجام می‌شود.",
      "درگاه پرداخت اجباری یا ادعای خزانه‌داری متمرکز وجود ندارد.",
    ],
  },
  {
    title: "لایه دوم: توسعه دارایی‌های دنیای واقعی (RWA)",
    subtitle: "فلزات گرانبها و دارایی‌های فیزیکی با سند شفاف",
    status: "Planned",
    statusStyle: "bg-[#192535] border-[#2D4D73] text-[#6BA4E8]",
    icon: ShieldCheck,
    iconWrap: "bg-[#162A24] border-[#286B63] text-[#4CB3A5]",
    description:
      "پس از تثبیت قراردادهای پایلوت طلا و تناسب محصول با بازار، زیرساخت دفترکل می‌تواند به سایر فلزات گرانبها، سکه و دارایی‌های واقعی با تعریف صریح متصدی و شرایط تحویل گسترش یابد.",
    boundaries: [
      "هر دارایی جدید تنها با مشخص‌بودن کامل طرف حساب و ضوابط حقوقی اضافه می‌شود.",
      "پیش از توافق واقعی با شرکای بازار، قابلیت‌های نمایشی فعال نمی‌شوند.",
    ],
  },
  {
    title: "لایه سوم: دارایی‌های دیجیتال (رمزارزها و NFT)",
    subtitle: "توسعه زیرساخت چنددارایی در فازهای بعدی",
    status: "In Development / Planned",
    statusStyle: "bg-[#2B2416] border-[#6B501D] text-[#E2A93B]",
    icon: Layers,
    iconWrap: "bg-[#282D35] border-[#3A414D] text-[#F2EEE5]",
    description:
      "طراحی معماری برای پشتیبانی از دارایی‌های دیجیتال مجاز و رکوردهای غیرمثلی (NFT) با تفکیک روشن مالکیت دیجیتال، مالکیت فکری و تعهدات فیزیکی.",
    boundaries: [
      "هیچ وعده سود یا تضمین بازدهی برای دارایی‌های دیجیتال ارائه نمی‌شود.",
      "در حوزه NFT، مالکیت توکن به‌معنای انتقال خودکار کپی‌رایت یا دارایی فیزیکی نیست.",
    ],
  },
  {
    title: "لایه چهارم: مشارکت جمعی و مسیر تدریجی DAO",
    subtitle: "حکمرانی پاسخگو و مرحله‌ای جامعه",
    status: "Planned",
    statusStyle: "bg-[#17191D] border-[#3A414D] text-[#9CA3AF]",
    icon: Users,
    iconWrap: "bg-[#162A24] border-[#286B63] text-[#4CB3A5]",
    description:
      "حرکت گام‌به‌گام از کارگروه‌های مشورتی و بازخورد شفاف کاربران به سوی سازوکارهای پیشنهادی و رأی‌گیری ساختاریافته، بدون ادعای غیرمتمرکزبودن کامل پیش از بلوغ فنی و حقوقی.",
    boundaries: [
      "حکمرانی DAO در حال شکل‌گیری است و امروز ادعای اجرای آن‌چین کامل مطرح نمی‌شود.",
      "تفکیک شفاف میان رأی‌گیری مشورتی و تصمیمات الزام‌آور.",
    ],
  },
  {
    title: "لایه پنجم: تعامل‌پذیری باز و افق AI / XR",
    subtitle: "چشم‌انداز پژوهشی بلندمدت",
    status: "Conceptual",
    statusStyle: "bg-[#17191D] border-[#82611C] border-dashed text-[#C9A45A]",
    icon: Cpu,
    iconWrap: "bg-[#2B2416] border-[#82611C] text-[#C9A45A]",
    description:
      "بررسی استانداردهای تعامل‌پذیری باز، ابزارهای تحلیل هوشمند (AI) برای کمک‌تصمیم و تجربه‌های تعاملی نسل آینده (XR) در افق بلندمدت اکوسیستم.",
    boundaries: [
      "این لایه صرفاً در سطح چشم‌انداز مفهومی است و تعهد زمانی کوتاه‌مدت ندارد.",
    ],
  },
];

export default function VisionPage() {
  return (
    <>
      <Navbar />

      <main className="relative flex-1 py-16 px-4 sm:px-6 lg:px-8 max-w-7xl mx-auto">
        {/* Back link & Header */}
        <div className="mb-12 space-y-5">
          <Link
            href="/"
            className="inline-flex items-center gap-1.5 text-xs font-medium text-[#9CA3AF] hover:text-[#C9A45A] transition-colors"
          >
            <ChevronRight className="h-4 w-4" />
            <span>بازگشت به صفحه اصلی</span>
          </Link>

          <div className="flex flex-col md:flex-row md:items-end justify-between gap-6 border-b border-[#2B303A] pb-8">
            <div className="space-y-3 max-w-3xl">
              <div className="inline-flex items-center gap-2 rounded-full border border-[#2B303A] bg-[#1F2329] px-3.5 py-1 text-xs font-semibold text-[#C9A45A]">
                <ZarorbitSymbolSvg sizePx={16} />
                <span className="bidi-isolate">
                  ZARORBIT ECOSYSTEM VISION &amp; ROADMAP
                </span>
              </div>
              <h1 className="text-3xl sm:text-4xl font-black text-[#F2EEE5] leading-snug">
                چشم‌انداز بلندمدت و نقشه راه مرحله‌ای{" "}
                <span className="text-[#C9A45A]">{brandConfig.name.fa}</span>
              </h1>
              <p className="text-sm sm:text-base text-[#9CA3AF] leading-relaxed">
                ما میان{" "}
                <strong className="text-[#F2EEE5]">
                  محصول عملیاتی امروز (زیرساخت معاملات طلا)
                </strong>{" "}
                و{" "}
                <strong className="text-[#F2EEE5]">
                  چشم‌انداز بلندمدت زیست‌بوم چنددارایی
                </strong>{" "}
                مرز روشن می‌گذاریم. هر لایه زیر تنها پس از بلوغ لایه قبلی و
                تقاضای واقعی بازار وارد فاز اجرا می‌شود.
              </p>
            </div>

            <div className="rounded-xl bg-[#1F2329] border border-[#2B303A] p-4 text-xs text-[#9CA3AF] shrink-0 space-y-1.5">
              <div className="font-bold text-[#F2EEE5]">راهنمای برچسب بلوغ:</div>
              <div className="flex items-center gap-2">
                <span className="h-2 w-2 rounded-full bg-[#48B884]" />
                <span>Active Pilot: در حال اجرا با بازار واقعی</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="h-2 w-2 rounded-full bg-[#E2A93B]" />
                <span>In Development: طراحی و پیاده‌سازی زیرساخت</span>
              </div>
              <div className="flex items-center gap-2">
                <span className="h-2 w-2 rounded-full bg-[#6BA4E8]" />
                <span>Planned / Conceptual: افق میان‌مدت و بلندمدت</span>
              </div>
            </div>
          </div>
        </div>

        {/* Roadmap Layers */}
        <div className="space-y-6">
          {roadmapLayers.map((layer, index) => {
            const Icon = layer.icon;
            return (
              <section
                key={index}
                className="rounded-[20px] bg-[#1F2329] border border-[#2B303A] p-6 sm:p-8"
              >
                <div className="flex flex-col md:flex-row md:items-start justify-between gap-4 mb-4">
                  <div className="flex items-start gap-4">
                    <div
                      className={`h-11 w-11 rounded-xl border flex items-center justify-center shrink-0 ${layer.iconWrap}`}
                    >
                      <Icon className="h-5 w-5" />
                    </div>
                    <div>
                      <h2 className="text-lg sm:text-xl font-bold text-[#F2EEE5]">
                        {layer.title}
                      </h2>
                      <p className="text-xs text-[#9CA3AF] mt-0.5">
                        {layer.subtitle}
                      </p>
                    </div>
                  </div>
                  <span
                    className={`bidi-isolate self-start rounded-full border px-3 py-1 text-[11px] font-semibold shrink-0 ${layer.statusStyle}`}
                  >
                    {layer.status}
                  </span>
                </div>

                <p className="text-sm text-[#9CA3AF] leading-relaxed mb-5">
                  {layer.description}
                </p>

                <div className="rounded-xl bg-[#17191D] border border-[#2B303A] p-4 space-y-2 text-xs text-[#9CA3AF]">
                  <div className="font-semibold text-[#F2EEE5] flex items-center gap-2">
                    <Gem className="h-3.5 w-3.5 text-[#C9A45A]" />
                    <span>مرزهای شفافیت در این فاز:</span>
                  </div>
                  <ul className="space-y-1.5">
                    {layer.boundaries.map((b, i) => (
                      <li key={i} className="flex items-start gap-2">
                        <span className="text-[#4CB3A5]">•</span>
                        <span>{b}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              </section>
            );
          })}
        </div>

        {/* Bottom CTA */}
        <div className="mt-12 rounded-[20px] bg-[#101215] border border-[#2B303A] p-6 sm:p-8 flex flex-col sm:flex-row items-center justify-between gap-6">
          <div className="space-y-1 text-center sm:text-right">
            <h3 className="text-base font-bold text-[#F2EEE5]">
              دنبال کردن مسیر توسعه {brandConfig.name.fa}
            </h3>
            <p className="text-xs text-[#9CA3AF]">
              اطلاعیه‌ها، انتشار نسخه‌های جدید و دعوت به پایلوت از طریق کانال
              رسمی تلگرام منتشر می‌شود.
            </p>
          </div>
          <a
            href={brandConfig.telegram.channelUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-2 rounded-[10px] bg-[#286B63] px-5 py-3 text-xs font-bold text-white hover:bg-[#328076] transition-colors shrink-0"
          >
            <Send className="h-4 w-4" />
            <span className="bidi-isolate">
              @{brandConfig.telegram.channelUsername}
            </span>
          </a>
        </div>
      </main>

      <Footer />
    </>
  );
}
