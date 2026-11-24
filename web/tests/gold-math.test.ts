import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  MAX_GOLD_AMOUNT,
  MAX_MESGHAL_PRICE,
  MESGHAL_TO_18K_GRAM,
  convertGoldWeight,
  formatIntegerInput,
  isValidNonNegativeDecimalInput,
  normalizeNumericText,
  parseNonNegativeNumber,
} from "../lib/gold-math";

describe("Gold Conversion Math & Persian Numeral Parsing", () => {
  it("normalizes Persian and Arabic digits with thousand separators", () => {
    assert.equal(normalizeNumericText("۲۵٬۰۰۰٬۰۰۰"), "25000000");
    assert.equal(normalizeNumericText("۱۲٫۵"), "12.5");
    assert.equal(normalizeNumericText("١٠.٢٥"), "10.25");
  });

  it("rejects slash fractions and garbage strings instead of misreading them", () => {
    assert.equal(parseNonNegativeNumber("1/2"), 0);
    assert.equal(parseNonNegativeNumber("۱/۲"), 0);
    assert.equal(parseNonNegativeNumber("12abc"), 0);
    assert.equal(isValidNonNegativeDecimalInput("۱۲٫۵"), true);
    assert.equal(isValidNonNegativeDecimalInput(""), true);
    assert.equal(isValidNonNegativeDecimalInput("۱/۲"), false);
    assert.equal(isValidNonNegativeDecimalInput("abc"), false);
    assert.equal(isValidNonNegativeDecimalInput("-5"), false);
  });

  it("clamps negative and invalid inputs safely to zero", () => {
    assert.equal(parseNonNegativeNumber("-50"), 0);
    assert.equal(parseNonNegativeNumber("abc"), 0);
    assert.equal(parseNonNegativeNumber(""), 0);
  });

  it("caps extremely large inputs to finite bounds without Infinity overflow", () => {
    const huge = "9".repeat(400);
    const res = convertGoldWeight(huge, "mesghal", huge);
    assert.equal(Number.isFinite(res.grams18k), true);
    assert.equal(Number.isFinite(res.mesghals), true);
    assert.equal(Number.isFinite(res.gramRateToman), true);
    assert.equal(Number.isFinite(res.estimatedTotalToman), true);
    assert.equal(res.mesghals, MAX_GOLD_AMOUNT);
    assert.equal(
      res.estimatedTotalToman,
      Math.round(MAX_GOLD_AMOUNT * MAX_MESGHAL_PRICE)
    );
  });

  it("converts 1 mesghal to exact 18k gram equivalent", () => {
    const res = convertGoldWeight("۱", "mesghal", "۳۰٬۰۰۰٬۰۰۰");
    assert.equal(res.mesghals, 1);
    assert.equal(res.grams18k, MESGHAL_TO_18K_GRAM);
    assert.equal(res.estimatedTotalToman, 30_000_000);
    assert.equal(Math.round(res.gramRateToman), Math.round(30_000_000 / MESGHAL_TO_18K_GRAM));
  });

  it("converts grams to mesghals and returns zero total when rate is omitted", () => {
    const res = convertGoldWeight("10", "gram", "");
    assert.equal(res.grams18k, 10);
    assert.ok(Math.abs(res.mesghals - 10 / MESGHAL_TO_18K_GRAM) < 1e-9);
    assert.equal(res.gramRateToman, 0);
    assert.equal(res.estimatedTotalToman, 0);
  });

  it("formats integer price input cleanly with ASCII commas", () => {
    assert.equal(formatIntegerInput("۳۴۵۰۰۰۰۰"), "34,500,000");
    assert.equal(formatIntegerInput(""), "");
  });
});
