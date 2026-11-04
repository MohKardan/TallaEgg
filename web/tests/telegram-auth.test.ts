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
    // Tamper with username or id
    payload.id = 111111111;

    const result = verifyTelegramAuth(payload, mockBotToken);
    assert.strictEqual(result.valid, false);
    assert.match(result.error || "", /Invalid cryptographic signature/);
  });

  it("rejects payload when auth_date is older than maxAgeSeconds (Replay Attack)", () => {
    // 10 minutes ago
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

  it("handles missing bot token safely", () => {
    const result = verifyTelegramAuth({ id: 123, hash: "abc", auth_date: 123 }, "");
    assert.strictEqual(result.valid, false);
    assert.match(result.error || "", /Bot token is not configured/);
  });
});
