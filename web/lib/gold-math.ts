/**
 * Iranian market mesghal equivalence for 18k (750) vs 17k (705) melted gold:
 * 1 mesghal = 4.6083g of 705 gold ≈ 4.3318g of 750 (18k) gold.
 */
export const MESGHAL_TO_18K_GRAM = 4.3318;

export type GoldUnit = "gram" | "mesghal";

const PERSIAN_DIGITS = "۰۱۲۳۴۵۶۷۸۹";
const ARABIC_DIGITS = "٠١٢٣٤٥٦٧٨٩";

/**
 * Normalizes Persian/Arabic numerals and decimal separators to standard ASCII.
 */
export function normalizeNumericText(input: string): string {
  let out = "";
  for (const ch of input) {
    const faIdx = PERSIAN_DIGITS.indexOf(ch);
    if (faIdx !== -1) {
      out += String(faIdx);
      continue;
    }
    const arIdx = ARABIC_DIGITS.indexOf(ch);
    if (arIdx !== -1) {
      out += String(arIdx);
      continue;
    }
    if (ch === "٫" || ch === "/") {
      out += ".";
      continue;
    }
    if (ch === "٬" || ch === "," || ch === " " || ch === "_") {
      continue;
    }
    out += ch;
  }
  return out.trim();
}

export function parseNonNegativeNumber(input: string): number {
  const normalized = normalizeNumericText(input);
  if (!normalized) return 0;
  const parsed = Number.parseFloat(normalized);
  if (!Number.isFinite(parsed) || parsed < 0) return 0;
  return parsed;
}

export function formatIntegerInput(input: string): string {
  const digitsOnly = normalizeNumericText(input).replace(/[^0-9]/g, "");
  if (!digitsOnly) return "";
  return digitsOnly.replace(/\B(?=(\d{3})+(?!\d))/g, ",");
}

export interface GoldConversionResult {
  grams18k: number;
  mesghals: number;
  gramRateToman: number;
  estimatedTotalToman: number;
}

export function convertGoldWeight(
  amountRaw: string,
  unit: GoldUnit,
  mesghalPriceRaw: string
): GoldConversionResult {
  const amount = parseNonNegativeNumber(amountRaw);
  const mesghalPrice = parseNonNegativeNumber(mesghalPriceRaw);

  const grams18k = unit === "gram" ? amount : amount * MESGHAL_TO_18K_GRAM;
  const mesghals = unit === "mesghal" ? amount : amount / MESGHAL_TO_18K_GRAM;
  const gramRateToman = mesghalPrice > 0 ? mesghalPrice / MESGHAL_TO_18K_GRAM : 0;
  const estimatedTotalToman =
    mesghalPrice > 0 ? Math.round(mesghals * mesghalPrice) : 0;

  return {
    grams18k,
    mesghals,
    gramRateToman,
    estimatedTotalToman,
  };
}
