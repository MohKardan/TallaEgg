import { describe, it } from "node:test";
import assert from "node:assert";
import { checkRateLimit } from "../lib/rate-limit";

describe("In-Memory Sliding Rate Limiter", () => {
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
});
