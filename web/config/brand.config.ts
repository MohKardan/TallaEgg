export interface BrandConfig {
  name: {
    fa: string;
    en: "ZARORBIT";
    monogram: string;
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
    monogram: "R1 (1–0–1)",
    ecosystemFa: "زیست‌بوم دارایی‌های واقعی و دیجیتال ZARORBIT",
  },
  slogan: {
    en: "Real & Digital Assets. Collective Ownership.",
    fa: "دارایی‌های واقعی و دیجیتال. مالکیت و مشارکت جمعی.",
    categoryEn: "DECENTRALIZED ASSET ECOSYSTEM",
    categoryFa: "زیست‌بوم غیرمتمرکز دارایی‌ها",
  },
  domain: {
    primary: process.env.NEXT_PUBLIC_SITE_HOST || "zarorbit.ir",
    siteUrl: process.env.NEXT_PUBLIC_SITE_URL || "https://zarorbit.ir",
  },
  tagline: {
    en: "A Community-Governed Ecosystem for Real and Digital Assets.",
    fa: "دارایی‌های واقعی و دیجیتال. مالکیت و مشارکت جمعی.",
    subFa:
      "اتصال شفاف دارایی‌های دنیای واقعی (با نقطه شروع طلا) و دارایی‌های دیجیتال شامل رمزارزها و NFTها در مسیری جامعه‌محور به سوی حکمرانی DAO.",
  },
  telegram: {
    botUsername:
      process.env.NEXT_PUBLIC_TELEGRAM_BOT_USERNAME || "zarorbitbot",
    botUrl:
      process.env.NEXT_PUBLIC_TELEGRAM_BOT_URL || "https://t.me/zarorbitbot",
  },
  seo: {
    title: "ZARORBIT (زَر اوربیت) — زیست‌بوم دارایی‌های واقعی و دیجیتال",
    titleTemplate: "%s | ZARORBIT",
    description:
      "زیست‌بوم روبه‌توسعه ZARORBIT برای اتصال شفاف دارایی‌های دنیای واقعی (RWA با نقطه شروع طلای ۱۸ عیار) و دارایی‌های دیجیتال (رمزارزها و NFTها) با مسیر تدریجی حکمرانی جامعه (DAO).",
    keywords: [
      "ZARORBIT",
      "زَر اوربیت",
      "زیست‌بوم دارایی",
      "Real-World Assets",
      "RWA",
      "طلای آبشده",
      "رمزارز",
      "NFT",
      "حکمرانی DAO",
      "مالکیت جمعی",
    ],
  },
  theme: {
    obsidianBg: "#17191D",
    surfaceBg: "#1F2329",
    elevatedBg: "#282D35",
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
