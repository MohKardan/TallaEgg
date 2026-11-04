/**
 * Formats a number with standard thousand separators.
 */
export function formatNumber(
  value: number | string,
  minimumFractionDigits = 0,
  maximumFractionDigits = 3
): string {
  const num = typeof value === "string" ? parseFloat(value) : value;
  if (isNaN(num)) return "۰";

  return num.toLocaleString("fa-IR", {
    minimumFractionDigits,
    maximumFractionDigits,
  });
}

/**
 * Formats an English numeral with thousand commas (e.g. 1,234,567.89).
 */
export function formatNumberEn(
  value: number | string,
  maxDecimals = 3
): string {
  const num = typeof value === "string" ? parseFloat(value) : value;
  if (isNaN(num)) return "0";

  const parts = num.toFixed(maxDecimals).split(".");
  parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ",");
  if (maxDecimals > 0 && parts[1]) {
    // Trim trailing zeros
    const decimals = parts[1].replace(/0+$/, "");
    return decimals ? `${parts[0]}.${decimals}` : parts[0];
  }
  return parts[0];
}

/**
 * Formats a Gregorian ISO timestamp into a Jalali date string at Tehran offset (+03:30).
 */
export function formatJalaliDateTime(isoString: string): string {
  if (!isoString) return "-";
  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return "-";

    return new Intl.DateTimeFormat("fa-IR", {
      timeZone: "Asia/Tehran",
      year: "numeric",
      month: "long",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    }).format(date);
  } catch {
    return isoString;
  }
}
