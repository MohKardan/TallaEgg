import type { Metadata } from "next";
import "./globals.css";
import { brandConfig } from "@/config/brand.config";

export const metadata: Metadata = {
  metadataBase: new URL(`https://${brandConfig.domain.primary}`),
  title: {
    default: brandConfig.seo.title,
    template: brandConfig.seo.titleTemplate,
  },
  description: brandConfig.seo.description,
  keywords: brandConfig.seo.keywords,
  authors: [
    { name: brandConfig.name.en, url: `https://${brandConfig.domain.primary}` },
  ],
  openGraph: {
    title: brandConfig.seo.title,
    description: brandConfig.seo.description,
    url: `https://${brandConfig.domain.primary}`,
    siteName: `${brandConfig.name.en} | ${brandConfig.name.fa}`,
    locale: "fa_IR",
    type: "website",
    images: [
      {
        url: brandConfig.assets.ogImage,
        width: 1200,
        height: 630,
        alt: `${brandConfig.name.en} — ${brandConfig.slogan.en}`,
      },
    ],
  },
  icons: {
    icon: [
      { url: brandConfig.assets.favicon, type: "image/svg+xml" },
      { url: brandConfig.assets.symbol, type: "image/svg+xml", sizes: "128x128" },
    ],
    apple: brandConfig.assets.appIcon,
  },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="fa" dir="rtl" data-theme="dark">
      <body className="min-h-screen bg-[#17191D] text-[#F2EEE5] antialiased selection:bg-[#C9A45A]/25 selection:text-[#F2EEE5]">
        <div className="relative min-h-screen flex flex-col justify-between overflow-x-hidden">
          {children}
        </div>
      </body>
    </html>
  );
}
