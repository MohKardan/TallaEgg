export interface BrandConfig {
  name: {
    fa: string;
    en: "ZARORBIT";
    symbolName: string;
    ecosystemFa: string;
  };
  slogan: {
    en: string;
    fa: string;
    categoryEn: string;
    categoryFa: string;
  };
  domain: {
    primary: string;
    secondary: string;
    siteUrl: string;
  };
  tagline: {
    en: string;
    fa: string;
    subFa: string;
  };
  telegram: {
    botUsername: string;
    botUrl: string;
    channelUsername: string;
    channelUrl: string;
  };
  seo: {
    title: string;
    titleTemplate: string;
    description: string;
    keywords: string[];
  };
  theme: {
    obsidianBg: string;
    surfaceBg: string;
    elevatedBg: string;
    goldAccent: string;
    bronzeGoldLight: string;
    ivoryText: string;
    deepTeal: string;
    orbitTeal: string;
  };
  assets: {
    symbol: string;
    horizontalDark: string;
    horizontalLight: string;
    favicon: string;
    appIcon: string;
    appleTouchIcon: string;
    ogImage: string;
  };
}

export const brandConfig: BrandConfig = {
  name: {
    fa: "زَر اوربیت",
    en: "ZARORBIT",
    symbolName: "Zero-Interrupt Orbital Z",
    ecosystemFa: "زیرساخت شفاف معاملات طلا و دارایی‌ها — زَر اوربیت",
  },
  slogan: {
    en: "Real & Digital Assets. Collective Ownership.",
    fa: "زیرساخت شفاف معاملات طلا و دارایی‌ها.",
    categoryEn: "TRANSPARENT GOLD & ASSET INFRASTRUCTURE",
    categoryFa: "زیرساخت شفاف معاملات طلا",
  },
  domain: {
    primary: process.env.NEXT_PUBLIC_SITE_HOST || "zarorbit.ir",
    secondary: "zarorbit.com",
    siteUrl: process.env.NEXT_PUBLIC_SITE_URL || "https://zarorbit.ir",
  },
  tagline: {
    en: "Transparent Gold Trading Infrastructure, Built to Evolve Into a Multi-Asset Ecosystem.",
    fa: "زیرساخت شفاف معاملات طلای آبشده برای بازار و مشتریان.",
    subFa:
      "اتصال مستقیم مظنه لحظه‌ای طلافروشی، دفترکل دقیق با کارمزد ۰٪ و تسویه شفاف روی تلگرام؛ با نقشه راه تدریجی به سوی زیست‌بوم دارایی‌های واقعی و دیجیتال.",
  },
  telegram: {
    botUsername:
      process.env.NEXT_PUBLIC_TELEGRAM_BOT_USERNAME || "zarorbitbot",
    botUrl:
      process.env.NEXT_PUBLIC_TELEGRAM_BOT_URL || "https://t.me/zarorbitbot",
    channelUsername:
      process.env.NEXT_PUBLIC_TELEGRAM_CHANNEL_USERNAME || "zarorbit",
    channelUrl:
      process.env.NEXT_PUBLIC_TELEGRAM_CHANNEL_URL || "https://t.me/zarorbit",
  },
  seo: {
    title: "ZARORBIT (زَر اوربیت) — زیرساخت شفاف معاملات طلا و دارایی‌ها",
    titleTemplate: "%s | ZARORBIT",
    description:
      "زَر اوربیت (ZARORBIT) زیرساخت معاملاتی و دفترکل دقیق برای معاملات شفاف طلای آبشده با اسپرد روشن و کارمزد ۰٪، همراه با چشم‌انداز بلندمدت زیست‌بوم دارایی‌های واقعی و دیجیتال.",
    keywords: [
      "ZARORBIT",
      "زَر اوربیت",
      "معاملات طلای آبشده",
      "زیرساخت معاملات طلا",
      "دفترکل معاملات طلا",
      "Real-World Assets",
      "RWA",
      "طلای ۱۸ عیار",
      "مظنه طلا",
    ],
  },
  theme: {
    obsidianBg: "#17191D",
    surfaceBg: "#1F2329",
    elevatedBg: "#252A32",
    goldAccent: "#C9A45A",
    bronzeGoldLight: "#82611C",
    ivoryText: "#F2EEE5",
    deepTeal: "#286B63",
    orbitTeal: "#4CB3A5",
  },
  assets: {
    symbol: "/images/zarorbit-symbol.svg",
    horizontalDark: "/images/zarorbit-logo-dark.svg",
    horizontalLight: "/images/zarorbit-logo-light.svg",
    favicon: "/images/zarorbit-favicon.svg",
    appIcon: "/images/zarorbit-app-icon.svg",
    appleTouchIcon: "/images/zarorbit-apple-touch-icon.png",
    ogImage: "/images/zarorbit-og.png",
  },
};
