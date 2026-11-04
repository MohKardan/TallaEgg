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
        background: "#080a0f",
        surface: {
          50: "#181d29",
          100: "#121620",
          200: "#0d1017",
          border: "rgba(212, 175, 55, 0.15)",
          borderHover: "rgba(212, 175, 55, 0.35)",
        },
        gold: {
          50: "#fdfbf5",
          100: "#f9f4e2",
          200: "#f1e5ba",
          300: "#e7d188",
          400: "#dcb957",
          500: "#D4AF37",
          600: "#b89228",
          700: "#937021",
          800: "#785a21",
          900: "#674a20",
          metallic: "linear-gradient(135deg, #FFF6D5 0%, #D4AF37 50%, #AA8010 100%)",
        },
      },
      fontFamily: {
        sans: ["Vazirmatn", "Tahoma", "Segoe UI", "sans-serif"],
      },
      boxShadow: {
        goldGlow: "0 0 25px -5px rgba(212, 175, 55, 0.25)",
        goldGlowSubtle: "0 0 15px -3px rgba(212, 175, 55, 0.12)",
      },
    },
  },
  plugins: [],
};

export default config;
