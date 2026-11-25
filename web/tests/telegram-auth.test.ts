import { describe, it, beforeEach } from "node:test";
import assert from "node:assert";
import crypto from "crypto";
import {
  verifyTelegramAuth,
  _resetRecentHashesForTests,
} from "../lib/auth/telegram";

describe("Telegram Authentication & Signature Verification", () => {
  const mockBotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz";

  beforeEach(() => {
    _resetRecentHashesForTests();
  });

  function createValidPayload(
    data: Record<string, any>,
    botToken: string
  ): Record<string, any> {
    const dataCheckString = Object.keys(data)
      .sort()
      .map((k) => `${k}=${data[k]}`)
      .join("\n");

    const secretKey = crypto.createHash("sha256").update(botToken).digest();
    const hash = crypto
      .createHmac("sha256", secretKey)
      .update(dataCheckString)
      .digest("hex");

    return { ...data, hash };
  }

  it("verifies a valid Telegram authentication payload successfully", () => {
    const currentTimestamp = Math.floor(Date.now() / 1000);
    const rawData = {
      id: 987654321,
      first_name: "Ali",
      last_name: "Tehrani",
      username: "ali_gold",
      auth_date: currentTimestamp,
    };

    const payload = createValidPayload(rawData, mockBotToken);
    const result = verifyTelegramAuth(payload, mockBotToken);

    assert.strictEqual(result.valid, true);
    assert.strictEqual(result.telegramId, 987654321);
    assert.strictEqual(result.username, "ali_gold");
    assert.strictEqual(result.firstName, "Ali");
  });

  it("rejects payload when hash is tampered or invalid", () => {
    const currentTimestamp = Math.floor(Date.now() / 1000);
    const rawData = {
      id: 987654321,
      first_name: "Ali",
      auth_date: currentTimestamp,
    };

    const payload = createValidPayload(rawData, mockBotToken);
    payload.id = 111111111;

    const result = verifyTelegramAuth(payload, mockBotToken);
    assert.strictEqual(result.valid, false);
    assert.match(result.error || "", /Invalid cryptographic signature/);
  });

  it("rejects non-string or malformed hex hash without throwing an unhandled exception", () => {
    const currentTimestamp = Math.floor(Date.now() / 1000);
    // Integer hash
    const res1 = verifyTelegramAuth(
      { id: 123, auth_date: currentTimestamp, hash: 12345 },
      mockBotToken
    );
    assert.strictEqual(res1.valid, false);
    assert.match(res1.error || "", /invalid cryptographic hash format/i);

    // Non-64 hex string
    const res2 = verifyTelegramAuth(
      { id: 123, auth_date: currentTimestamp, hash: "not-a-valid-hex" },
      mockBotToken
    );
    assert.strictEqual(res2.valid, false);
    assert.match(res2.error || "", /invalid cryptographic hash format/i);
  });

  it("rejects payload when auth_date is older than maxAgeSeconds (Past Replay Attack)", () => {
    const oldTimestamp = Math.floor(Date.now() / 1000) - 600;
    const rawData = {
      id: 987654321,
      first_name: "Ali",
      auth_date: oldTimestamp,
    };

    const payload = createValidPayload(rawData, mockBotToken);
    const result = verifyTelegramAuth(payload, mockBotToken, 300);

    assert.strictEqual(result.valid, false);
    assert.match(result.error || "", /Auth date expired/);
  });

  it("rejects payload when auth_date is in the future beyond allowed clock skew", () => {
    const futureTimestamp = Math.floor(Date.now() / 1000) + 120; // 2 minutes ahead
    const rawData = {
      id: 987654321,
      first_name: "Ali",
      auth_date: futureTimestamp,
    };

    const payload = createValidPayload(rawData, mockBotToken);
    const result = verifyTelegramAuth(payload, mockBotToken, 300);

    assert.strictEqual(result.valid, false);
    assert.match(
      result.error || "",
      /Auth date expired or out of allowed window/
    );
  });

  it("rejects immediate replay of the exact same valid payload", () => {
    const currentTimestamp = Math.floor(Date.now() / 1000);
    const rawData = {
      id: 555666777,
      first_name: "Saeed",
      auth_date: currentTimestamp,
    };

    const payload = createValidPayload(rawData, mockBotToken);
    const firstCheck = verifyTelegramAuth(payload, mockBotToken);
    assert.strictEqual(firstCheck.valid, true);

    // Immediate replay of identical hash
    const secondCheck = verifyTelegramAuth(payload, mockBotToken);
    assert.strictEqual(secondCheck.valid, false);
    assert.match(secondCheck.error || "", /replay detected/);
  });

  it("rejects case-flipped uppercase hash replay of a previously verified payload", () => {
    const currentTimestamp = Math.floor(Date.now() / 1000);
    const rawData = {
      id: 777888999,
      first_name: "Maryam",
      auth_date: currentTimestamp,
    };

    const payload = createValidPayload(rawData, mockBotToken);
    const firstCheck = verifyTelegramAuth(payload, mockBotToken);
    assert.strictEqual(firstCheck.valid, true);

    const upperReplay = {
      ...payload,
      hash: String(payload.hash).toUpperCase(),
    };
    const secondCheck = verifyTelegramAuth(upperReplay, mockBotToken);
    assert.strictEqual(secondCheck.valid, false);
    assert.match(secondCheck.error || "", /replay detected/);
  });

  it("fails closed when replay cache is saturated and never evicts active hashes", () => {
    const currentTimestamp = Math.floor(Date.now() / 1000);
    const maxCacheEntries = 3;

    const firstPayload = createValidPayload(
      { id: 1001, first_name: "User1", auth_date: currentTimestamp },
      mockBotToken
    );
    const secondPayload = createValidPayload(
      { id: 1002, first_name: "User2", auth_date: currentTimestamp },
      mockBotToken
    );
    const thirdPayload = createValidPayload(
      { id: 1003, first_name: "User3", auth_date: currentTimestamp },
      mockBotToken
    );
    const fourthPayload = createValidPayload(
      { id: 1004, first_name: "User4", auth_date: currentTimestamp },
      mockBotToken
    );

    assert.strictEqual(
      verifyTelegramAuth(firstPayload, mockBotToken, 300, maxCacheEntries).valid,
      true
    );
    assert.strictEqual(
      verifyTelegramAuth(secondPayload, mockBotToken, 300, maxCacheEntries).valid,
      true
    );
    assert.strictEqual(
      verifyTelegramAuth(thirdPayload, mockBotToken, 300, maxCacheEntries).valid,
      true
    );

    // 4th distinct valid payload must fail closed rather than evicting an active entry
    const saturatedResult = verifyTelegramAuth(
      fourthPayload,
      mockBotToken,
      300,
      maxCacheEntries
    );
    assert.strictEqual(saturatedResult.valid, false);
    assert.match(saturatedResult.error || "", /Replay protection cache saturated/);

    // The oldest active entry must still be present and rejected as a replay
    const replayOfOldest = verifyTelegramAuth(
      firstPayload,
      mockBotToken,
      300,
      maxCacheEntries
    );
    assert.strictEqual(replayOfOldest.valid, false);
    assert.match(replayOfOldest.error || "", /replay detected/);
  });

  it("rejects invalid or non-positive Telegram user ids", () => {
    const currentTimestamp = Math.floor(Date.now() / 1000);
    const invalidIds = [0, -10, 12.34, "abc", Number.MAX_SAFE_INTEGER + 10];

    for (const invalidId of invalidIds) {
      const payload = createValidPayload(
        { id: invalidId, auth_date: currentTimestamp },
        mockBotToken
      );
      const result = verifyTelegramAuth(payload, mockBotToken);
      assert.strictEqual(result.valid, false);
      assert.match(result.error || "", /Invalid Telegram user id/);
    }
  });

  it("handles missing bot token safely", () => {
    const result = verifyTelegramAuth(
      { id: 123, hash: "abc", auth_date: 123 },
      ""
    );
    assert.strictEqual(result.valid, false);
    assert.match(result.error || "", /Bot token is not configured/);
  });
});
