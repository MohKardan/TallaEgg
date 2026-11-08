import { describe, it } from "node:test";
import assert from "node:assert";
import crypto from "crypto";
import { verifyTelegramAuth } from "../lib/auth/telegram";

describe("Telegram Authentication & Signature Verification", () => {
  const mockBotToken = "123456789:ABCdefGHIjklMNOpqrsTUVwxyz";

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
    const res1 = verifyTelegramAuth({ id: 123, auth_date: currentTimestamp, hash: 12345 }, mockBotToken);
    assert.strictEqual(res1.valid, false);
    assert.match(res1.error || "", /invalid cryptographic hash format/i);

    // Non-64 hex string
    const res2 = verifyTelegramAuth({ id: 123, auth_date: currentTimestamp, hash: "not-a-valid-hex" }, mockBotToken);
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
    assert.match(result.error || "", /Auth date expired or out of allowed window/);
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

  it("handles missing bot token safely", () => {
    const result = verifyTelegramAuth({ id: 123, hash: "abc", auth_date: 123 }, "");
    assert.strictEqual(result.valid, false);
    assert.match(result.error || "", /Bot token is not configured/);
  });
});
