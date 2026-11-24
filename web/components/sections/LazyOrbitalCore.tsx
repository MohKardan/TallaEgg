"use client";

import dynamic from "next/dynamic";
import { ZarorbitSymbolSvg } from "@/components/BrandLogo";

const ZarorbitOrbitalMotion = dynamic(
  () => import("@/components/ZarorbitOrbitalMotion"),
  {
    ssr: false,
    loading: () => (
      <div
        aria-hidden="true"
        className="zar-panel flex min-h-[380px] items-center justify-center p-6"
      >
        <ZarorbitSymbolSvg sizePx={116} className="opacity-85" />
      </div>
    ),
  }
);

export default function LazyOrbitalCore() {
  return <ZarorbitOrbitalMotion />;
}
