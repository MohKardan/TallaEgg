import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  MESGHAL_TO_18K_GRAM,
  convertGoldWeight,
  formatIntegerInput,
  normalizeNumericText,
  parseNonNegativeNumber,
} from "../lib/gold-math";

describe("Gold Conversion Math & Persian Numeral Parsing", () => {
  it("normalizes Persian and Arabic digits with thousand separators", () => {
    assert.equal(normalizeNumericText("۲۵٬۰۰۰٬۰۰۰"), "25000000");
    assert.equal(normalizeNumericText("۱۲٫۵"), "12.5");
    assert.equal(normalizeNumericText("١٠.٢٥"), "10.25");
  });

  it("clamps negative and invalid inputs safely to zero", () => {
    assert.equal(parseNonNegativeNumber("-50"), 0);
    assert.equal(parseNonNegativeNumber("abc"), 0);
    assert.equal(parseNonNegativeNumber(""), 0);
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
