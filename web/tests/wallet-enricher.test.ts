import { describe, it } from "node:test";
import assert from "node:assert";
import { enrichWalletItem, RawWalletDto } from "../lib/api/wallet-client";

describe("Wallet Balance Enricher & Categorization", () => {
  it("computes availableBalance = balance and totalBalance = balance + lockedBalance", () => {
    const raw: RawWalletDto = {
      asset: "IRT",
      balance: 15000000,
      lockedBalance: 2000000,
      updatedAt: "2026-11-04T12:00:00Z",
    };

    const item = enrichWalletItem(raw);
    assert.strictEqual(item.asset, "IRT");
    assert.strictEqual(item.balance, 15000000);
    assert.strictEqual(item.lockedBalance, 2000000);
    assert.strictEqual(item.availableBalance, 15000000);
    assert.strictEqual(item.totalBalance, 17000000);
    assert.strictEqual(item.isCreditLedger, false);
    assert.strictEqual(item.category, "fiat");
    assert.strictEqual(item.decimalPlaces, 0);
    assert.strictEqual(item.persianLabel, "تومان");
    assert.strictEqual(item.unit, "تومان");
  });

  it("enriches XAU, USD, and BTC with accurate labels, categories, and decimal precision", () => {
    const xau = enrichWalletItem({
      asset: "XAU",
      balance: 1.25,
      lockedBalance: 0.25,
      updatedAt: "2026-11-25T10:00:00Z",
    });
    assert.strictEqual(xau.persianLabel, "انس طلا");
    assert.strictEqual(xau.unit, "انس");
    assert.strictEqual(xau.category, "gold");
    assert.strictEqual(xau.decimalPlaces, 4);
    assert.strictEqual(xau.availableBalance, 1.25);
    assert.strictEqual(xau.totalBalance, 1.5);

    const usd = enrichWalletItem({
      asset: "USD",
      balance: 450.5,
      lockedBalance: 50,
      updatedAt: "2026-11-25T10:00:00Z",
    });
    assert.strictEqual(usd.persianLabel, "دلار");
    assert.strictEqual(usd.unit, "دلار");
    assert.strictEqual(usd.category, "fiat");
    assert.strictEqual(usd.decimalPlaces, 2);
    assert.strictEqual(usd.availableBalance, 450.5);
    assert.strictEqual(usd.totalBalance, 500.5);

    const btc = enrichWalletItem({
      asset: "BTC",
      balance: 0.00004,
      lockedBalance: 0.00001,
      updatedAt: "2026-11-25T10:00:00Z",
    });
    assert.strictEqual(btc.persianLabel, "بیت‌کوین");
    assert.strictEqual(btc.unit, "BTC");
    assert.strictEqual(btc.category, "crypto");
    assert.strictEqual(btc.decimalPlaces, 8);
    assert.strictEqual(btc.availableBalance, 0.00004);
    assert.ok(Math.abs(btc.totalBalance - 0.00005) < 1e-12);
  });

  it("identifies CREDIT_MAUA and dynamically resolves CREDIT_* rows from base asset metadata", () => {
    const creditMaua = enrichWalletItem({
      asset: "CREDIT_MAUA",
      balance: 50.0,
      lockedBalance: 0,
      updatedAt: "2026-11-04T12:00:00Z",
    });
    assert.strictEqual(creditMaua.isCreditLedger, true);
    assert.strictEqual(creditMaua.category, "credit");
    assert.strictEqual(creditMaua.persianLabel, "سقف اعتبار آبشده");
    assert.strictEqual(creditMaua.unit, "گرم");
    assert.strictEqual(creditMaua.decimalPlaces, 4);

    const creditSeke = enrichWalletItem({
      asset: "CREDIT_SEKE_BAHAR",
      balance: 3,
      lockedBalance: 0,
      updatedAt: "2026-11-25T10:00:00Z",
    });
    assert.strictEqual(creditSeke.isCreditLedger, true);
    assert.strictEqual(creditSeke.category, "credit");
    assert.strictEqual(creditSeke.persianLabel, "سقف اعتبار سکه بهار آزادی");
    assert.strictEqual(creditSeke.unit, "سکه");
    assert.strictEqual(creditSeke.decimalPlaces, 4);

    const creditBtc = enrichWalletItem({
      asset: "CREDIT_BTC",
      balance: 0.05,
      lockedBalance: 0,
      updatedAt: "2026-11-25T10:00:00Z",
    });
    assert.strictEqual(creditBtc.isCreditLedger, true);
    assert.strictEqual(creditBtc.category, "credit");
    assert.strictEqual(creditBtc.persianLabel, "سقف اعتبار بیت‌کوین");
    assert.strictEqual(creditBtc.unit, "BTC");
    assert.strictEqual(creditBtc.decimalPlaces, 8);

    const creditXau = enrichWalletItem({
      asset: "CREDIT_XAU",
      balance: 2,
      lockedBalance: 0,
      updatedAt: "2026-11-25T10:00:00Z",
    });
    assert.strictEqual(creditXau.isCreditLedger, true);
    assert.strictEqual(creditXau.persianLabel, "سقف اعتبار انس طلا");
    assert.strictEqual(creditXau.unit, "انس");
  });
});
