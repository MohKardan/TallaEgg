import type { Metadata } from "next";
import "./globals.css";
import { brandConfig } from "@/config/brand.config";

export const metadata: Metadata = {
  metadataBase: new URL(`https://${brandConfig.domain.primary}`),
  title: brandConfig.seo.title,
  description: brandConfig.seo.description,
  keywords: brandConfig.seo.keywords,
  authors: [{ name: brandConfig.name.en, url: `https://${brandConfig.domain.primary}` }],
  openGraph: {
    title: brandConfig.seo.title,
    description: brandConfig.seo.description,
    url: `https://${brandConfig.domain.primary}`,
    siteName: `${brandConfig.name.fa} | ${brandConfig.name.en}`,
    locale: "fa_IR",
    type: "website",
    images: [
      {
        url: brandConfig.assets.logoLargeOrbit,
        width: 800,
        height: 800,
        alt: `${brandConfig.name.fa} - ${brandConfig.slogan.en}`,
      },
    ],
  },
  icons: {
    icon: brandConfig.assets.iconNoOrbit,
    apple: brandConfig.assets.iconNoOrbit,
  },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="fa" dir="rtl">
      <body className="min-h-screen bg-[#080a0f] text-slate-100 antialiased selection:bg-amber-500/30 selection:text-amber-200">
        <div className="relative min-h-screen flex flex-col justify-between overflow-hidden">
          {children}
        </div>
      </body>
    </html>
  );
}
