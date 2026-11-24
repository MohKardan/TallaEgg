/**
 * Iranian market mesghal equivalence for 18k (750) vs 17k (705) melted gold:
 * 1 mesghal = 4.6083g of 705 gold ≈ 4.3318g of 750 (18k) gold.
 */
export const MESGHAL_TO_18K_GRAM = 4.3318;

/** Safe upper bounds to prevent floating-point overflow / Infinity on huge inputs. */
export const MAX_GOLD_AMOUNT = 1_000_000_000;
export const MAX_MESGHAL_PRICE = 1_000_000_000_000;

export type GoldUnit = "gram" | "mesghal";

const PERSIAN_DIGITS = "۰۱۲۳۴۵۶۷۸۹";
const ARABIC_DIGITS = "٠١٢٣٤٥٦٧٨٩";

/**
 * Normalizes Persian/Arabic numerals and Persian decimal separator (٫) to standard ASCII.
 * Intentionally does NOT map `/` to `.` so fractions like "1/2" are not silently misread as 1.2.
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
    if (ch === "٫") {
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

/**
 * Returns true if the input is empty or a valid non-negative decimal string
 * (after normalizing Persian/Arabic digits and thousand separators).
 */
export function isValidNonNegativeDecimalInput(input: string): boolean {
  const normalized = normalizeNumericText(input);
  if (!normalized) return true;
  if (!/^\d+(\.\d+)?$/.test(normalized)) return false;
  const parsed = Number(normalized);
  return Number.isFinite(parsed) && parsed >= 0;
}

export function parseNonNegativeNumber(input: string, maxValue?: number): number {
  const normalized = normalizeNumericText(input);
  if (!normalized || !/^\d+(\.\d+)?$/.test(normalized)) return 0;
  const parsed = Number(normalized);
  if (Number.isNaN(parsed) || parsed < 0) return 0;
  if (!Number.isFinite(parsed)) {
    return typeof maxValue === "number" ? maxValue : 0;
  }
  if (typeof maxValue === "number" && parsed > maxValue) return maxValue;
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
  const amount = parseNonNegativeNumber(amountRaw, MAX_GOLD_AMOUNT);
  const mesghalPrice = parseNonNegativeNumber(mesghalPriceRaw, MAX_MESGHAL_PRICE);

  const rawGrams = unit === "gram" ? amount : amount * MESGHAL_TO_18K_GRAM;
  const rawMesghals = unit === "mesghal" ? amount : amount / MESGHAL_TO_18K_GRAM;
  const rawGramRate = mesghalPrice > 0 ? mesghalPrice / MESGHAL_TO_18K_GRAM : 0;
  const rawTotal = mesghalPrice > 0 ? Math.round(rawMesghals * mesghalPrice) : 0;

  return {
    grams18k: Number.isFinite(rawGrams) ? rawGrams : 0,
    mesghals: Number.isFinite(rawMesghals) ? rawMesghals : 0,
    gramRateToman: Number.isFinite(rawGramRate) ? rawGramRate : 0,
    estimatedTotalToman: Number.isFinite(rawTotal) ? rawTotal : 0,
  };
}
