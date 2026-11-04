import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "طلااگ | TallaEgg — پلتفرم معاملات هوشمند و آنی طلای آبشده",
  description:
    "سامانه رسمی معاملات آنلاین طلای آبشده ۱۸ عیار با مدل بازارساز اختصاصی، کارمزد صفر درصد، تسویه لحظه‌ای و امنیت دفترکل بانکی.",
  keywords: [
    "طلااگ",
    "TallaEgg",
    "طلای آبشده",
    "معاملات طلا",
    "مظنه طلا",
    "قیمت لحظه‌ای طلا",
    "خرید طلای آبشده",
  ],
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
