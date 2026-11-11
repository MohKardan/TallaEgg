import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./components/**/*.{js,ts,jsx,tsx,mdx}",
    "./app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        background: "#17191D",
        ivory: "#F2EEE5",
        obsidian: "#17191D",
        surface: {
          50: "#282D35",
          100: "#1F2329",
          200: "#17191D",
          300: "#101215",
          border: "#2B303A",
          borderControl: "#6B7485",
          borderHover: "#C9A45A",
        },
        gold: {
          50: "#FAF8F3",
          100: "#F2EEE5",
          200: "#E8D4A8",
          300: "#DFC080",
          400: "#D4B16A",
          500: "#C9A45A",
          600: "#A8843B",
          700: "#82611C",
          800: "#634914",
          900: "#47340D",
        },
        teal: {
          orbit: "#4CB3A5",
          deep: "#286B63",
          subtle: "#162A24",
        },
        semantic: {
          success: "#48B884",
          successBg: "#162A24",
          warning: "#E2A93B",
          warningBg: "#2B2416",
          error: "#F07F83",
          errorBg: "#2D1B1E",
          info: "#6BA4E8",
          infoBg: "#192535",
        },
      },
      fontFamily: {
        sans: ["Vazirmatn", "Inter", "Tahoma", "system-ui", "sans-serif"],
        display: ["Manrope", "Inter", "Vazirmatn", "sans-serif"],
        ui: ["Inter", "Vazirmatn", "sans-serif"],
      },
      borderRadius: {
        card: "20px",
        control: "10px",
      },
      boxShadow: {
        card: "0 12px 32px -12px rgba(0, 0, 0, 0.55)",
        subtle: "0 4px 16px -4px rgba(0, 0, 0, 0.35)",
      },
    },
  },
  plugins: [],
};

export default config;
