/**
 * Formats a number with Persian thousand separators.
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
 * Formats a Gregorian ISO timestamp into a Jalali date string at Tehran offset (+03:30).
 * Explicitly declares Persian calendar to prevent Gregorian fallback on minimal ICU environments.
 */
export function formatJalaliDateTime(isoString: string): string {
  if (!isoString) return "-";
  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return "-";

    return new Intl.DateTimeFormat("fa-IR-u-ca-persian", {
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
