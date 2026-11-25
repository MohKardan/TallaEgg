// ZARORBIT Design Tokens v1.2 — TypeScript / Tailwind helper
export const zarorbitTokens = {
  version: "1.2.0",
  brand: {
    name: "ZARORBIT",
    nameFa: "زَر اوربیت",
    symbol: "S1 — Zero-Interrupt Orbital Z",
    tagline: "Real & Digital Assets. Collective Ownership.",
  },
  colors: {
    obsidian: "#17191D",
    surface: "#1F2329",
    elevated: "#252A32",
    metallicGold: "#C9A45A",
    metallicGoldHover: "#D4B16A",
    bronzeGoldAccessible: "#82611C",
    ivory: "#F2EEE5",
    deepTeal: "#286B63",
    orbitTealAccessible: "#4CB3A5",
    borderSubtle: "#2B303A",
    borderControl: "#6B7485",
    borderFocusDark: "#4CB3A5",
    borderFocusLight: "#286B63",
  },
  fonts: {
    enDisplay: ["Manrope", "Inter", "sans-serif"],
    enUi: ["Inter", "sans-serif"],
    fa: ["Vazirmatn", "Tahoma", "sans-serif"],
  },
} as const;
