import { describe, it, beforeEach } from "node:test";
import assert from "node:assert";
import {
  checkRateLimit,
  MAX_STORE_ENTRIES,
  _resetRateLimitStoreForTests,
} from "../lib/rate-limit";

describe("In-Memory Sliding Rate Limiter", () => {
  beforeEach(() => {
    _resetRateLimitStoreForTests();
  });

  it("allows requests within configured threshold", () => {
    const testId = `test-ip-${Date.now()}`;
    const limit = 3;

    const r1 = checkRateLimit(testId, limit, 60);
    assert.strictEqual(r1.success, true);
    assert.strictEqual(r1.remaining, 2);

    const r2 = checkRateLimit(testId, limit, 60);
    assert.strictEqual(r2.success, true);
    assert.strictEqual(r2.remaining, 1);

    const r3 = checkRateLimit(testId, limit, 60);
    assert.strictEqual(r3.success, true);
    assert.strictEqual(r3.remaining, 0);

    // 4th request must fail
    const r4 = checkRateLimit(testId, limit, 60);
    assert.strictEqual(r4.success, false);
    assert.strictEqual(r4.remaining, 0);
  });

  it("isolates auth:ip floods so auth:id lockouts are never evicted", () => {
    const accountKey = "auth:id:424242";
    const idLimit = 2;

    assert.strictEqual(checkRateLimit(accountKey, idLimit, 60).success, true);
    assert.strictEqual(checkRateLimit(accountKey, idLimit, 60).success, true);
    assert.strictEqual(checkRateLimit(accountKey, idLimit, 60).success, false);

    // Flood the auth:ip namespace past MAX_STORE_ENTRIES to trigger eviction there
    for (let i = 0; i < MAX_STORE_ENTRIES + 600; i++) {
      checkRateLimit(`auth:ip:10.0.${Math.floor(i / 256)}.${i % 256}`, 10, 60);
    }

    // The per-account lockout in auth:id must remain intact
    const afterFlood = checkRateLimit(accountKey, idLimit, 60);
    assert.strictEqual(afterFlood.success, false);
    assert.strictEqual(afterFlood.remaining, 0);
  });
});
