export interface BrandConfig {
  name: {
    fa: string; // "زَر اوربیت"
    en: string; // "Zarorbit"
    monogram: string; // "Z"
    legalFa: string; // "سامانه معاملات آنلاین طلای فیزیکی و دیجیتال زَر اوربیت"
  };
  slogan: {
    en: string; // "Physical & Digital Gold"
    fa: string; // "طلای فیزیکی و دیجیتال"
  };
  domain: {
    primary: string; // "zarorbit.ir"
    global: string; // "zarorbit.com"
  };
  tagline: {
    fa: string;
    subFa: string;
  };
  telegram: {
    botUsername: string; // "zarorbitbot"
    botUrl: string; // "https://t.me/zarorbitbot"
    channelUrl: string; // "https://t.me/zarorbit"
    supportUrl?: string;
  };
  seo: {
    title: string;
    titleTemplate: string;
    description: string;
    keywords: string[];
  };
  theme: {
    goldAccent: string; // "#E5B80B"
    obsidianBg: string; // "#080a0f"
  };
  assets: {
    iconNoOrbit: string; // Brushed gold 'Z' mark without outer ring (small icons, favicons, badges)
    logoLargeOrbit: string; // Brushed gold 'Z' inside circular gold ring bezel (hero, showcase, detailed banners)
  };
}

export const brandConfig: BrandConfig = {
  name: {
    fa: "زَر اوربیت",
    en: "Zarorbit",
    monogram: "Z",
    legalFa: "سامانه هوشمند معاملات آنلاین طلای فیزیکی و دیجیتال زَر اوربیت",
  },
  slogan: {
    en: "PHYSICAL & DIGITAL GOLD",
    fa: "طلای فیزیکی و دیجیتال",
  },
  domain: {
    primary: "zarorbit.ir",
    global: "zarorbit.com",
  },
  tagline: {
    fa: "مدار پیوسته نقدینگی و معاملات آنلاین طلای آبشده",
    subFa: "معاملات مستقیم با میز بازارساز، کارمزد صفر درصد، تسویه لحظه‌ای و دفترکل امن",
  },
  telegram: {
    botUsername:
      process.env.NEXT_PUBLIC_TELEGRAM_BOT_USERNAME || "zarorbitbot",
    botUrl:
      process.env.NEXT_PUBLIC_TELEGRAM_BOT_URL ||
      "https://t.me/zarorbitbot",
    channelUrl: "https://t.me/zarorbit",
    supportUrl: "https://t.me/zarorbit_support",
  },
  seo: {
    title: "زَر اوربیت | Zarorbit — سامانه معاملات آنلاین طلای فیزیکی و دیجیتال",
    titleTemplate: "%s | زَر اوربیت",
    description:
      "سامانه رسمی معاملات آنلاین طلای آبشده ۱۸ عیار با مدل بازارساز اختصاصی، کارمزد صفر، تسویه آنی دفترکل بانکی و اتصال به ربات معاملاتی تلگرام.",
    keywords: [
      "زَر اوربیت",
      "زر اوربیت",
      "Zarorbit",
      "طلای آبشده",
      "طلای فیزیکی",
      "طلای دیجیتال",
      "معاملات طلا",
      "مظنه طلا",
      "قیمت لحظه‌ای طلا",
      "خرید طلای آبشده",
    ],
  },
  theme: {
    goldAccent: "#E5B80B",
    obsidianBg: "#080a0f",
  },
  assets: {
    iconNoOrbit: "/images/zarorbit-icon.jpg",
    logoLargeOrbit: "/images/zarorbit-logo.jpg",
  },
};
