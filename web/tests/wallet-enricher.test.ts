import { describe, it } from "node:test";
import assert from "node:assert";
import { enrichWalletItem, RawWalletDto } from "../lib/api/wallet-client";

describe("Wallet Balance Enricher & Categorization", () => {
  it("enriches standard Toman wallet correctly", () => {
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
    assert.strictEqual(item.availableBalance, 13000000);
    assert.strictEqual(item.isCreditLedger, false);
    assert.strictEqual(item.persianLabel, "تومان");
    assert.strictEqual(item.unit, "تومان");
  });

  it("identifies CREDIT_MAUA as a credit ledger", () => {
    const raw: RawWalletDto = {
      asset: "CREDIT_MAUA",
      balance: 50.0,
      lockedBalance: 0,
      updatedAt: "2026-11-04T12:00:00Z",
    };

    const item = enrichWalletItem(raw);
    assert.strictEqual(item.isCreditLedger, true);
    assert.strictEqual(item.persianLabel, "سقف اعتبار آبشده");
    assert.strictEqual(item.unit, "گرم");
  });
});
