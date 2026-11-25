import { describe, it } from "node:test";
import assert from "node:assert";
import crypto from "crypto";
import {
  sealSessionToken,
  unsealSessionToken,
  SessionUser,
} from "../lib/auth/session";

describe("Secure Session Token Management (AES-256-GCM)", () => {
  const sampleUser: SessionUser = {
    userId: "d3b07384-d113-4638-b6ff-183059883624",
    telegramId: 12345678,
    username: "gold_trader",
    firstName: "Reza",
    lastName: "Farahani",
    role: "RegularUser",
    status: "Approved",
    isActive: true,
  };

  function encryptRawPayloadWithSecret(
    payload: Record<string, unknown>,
    secret: string
  ): string {
    const key = crypto.createHash("sha256").update(secret).digest();
    const iv = crypto.randomBytes(12);
    const cipher = crypto.createCipheriv("aes-256-gcm", key, iv);
    const encrypted = Buffer.concat([
      cipher.update(JSON.stringify(payload), "utf8"),
      cipher.final(),
    ]);
    const tag = cipher.getAuthTag();
    return [
      iv.toString("base64url"),
      tag.toString("base64url"),
      encrypted.toString("base64url"),
    ].join(".");
  }

  it("seals and unseals session token preserving all fields", () => {
    const token = sealSessionToken(sampleUser);
    assert.ok(token);
    assert.strictEqual(token.split(".").length, 3); // iv.tag.ciphertext

    const decrypted = unsealSessionToken(token);
    assert.ok(decrypted);
    assert.strictEqual(decrypted.userId, sampleUser.userId);
    assert.strictEqual(decrypted.telegramId, sampleUser.telegramId);
    assert.strictEqual(decrypted.username, sampleUser.username);
    assert.strictEqual(decrypted.status, "Approved");
    assert.ok(decrypted.exp > Math.floor(Date.now() / 1000));
  });

  it("rejects tampered token with authentication tag mismatch", () => {
    const token = sealSessionToken(sampleUser);
    const parts = token.split(".");
    // Tamper with the ciphertext
    const tamperedCiphertext = Buffer.from("tampered_data").toString("base64url");
    const tamperedToken = `${parts[0]}.${parts[1]}.${tamperedCiphertext}`;

    const decrypted = unsealSessionToken(tamperedToken);
    assert.strictEqual(decrypted, null);
  });

  it("rejects malformed token format", () => {
    assert.strictEqual(unsealSessionToken("invalid-token-without-parts"), null);
    assert.strictEqual(unsealSessionToken(""), null);
  });

  it("rejects tokens with missing, zero, non-numeric, or expired exp claims", () => {
    const origSecret = process.env.SESSION_SECRET;
    const testSecret = "0123456789abcdef0123456789abcdef";
    try {
      process.env.SESSION_SECRET = testSecret;
      const now = Math.floor(Date.now() / 1000);

      const missingExpToken = encryptRawPayloadWithSecret(
        { ...sampleUser, iat: now },
        testSecret
      );
      assert.strictEqual(unsealSessionToken(missingExpToken), null);

      const zeroExpToken = encryptRawPayloadWithSecret(
        { ...sampleUser, iat: now, exp: 0 },
        testSecret
      );
      assert.strictEqual(unsealSessionToken(zeroExpToken), null);

      const stringExpToken = encryptRawPayloadWithSecret(
        { ...sampleUser, iat: now, exp: String(now + 3600) },
        testSecret
      );
      assert.strictEqual(unsealSessionToken(stringExpToken), null);

      const expiredToken = encryptRawPayloadWithSecret(
        { ...sampleUser, iat: now - 100, exp: now - 1 },
        testSecret
      );
      assert.strictEqual(unsealSessionToken(expiredToken), null);
    } finally {
      if (origSecret === undefined) {
        delete process.env.SESSION_SECRET;
      } else {
        process.env.SESSION_SECRET = origSecret;
      }
    }
  });

  it("throws in production when SESSION_SECRET is missing", () => {
    const origEnv = process.env.NODE_ENV;
    const origSecret = process.env.SESSION_SECRET;
    try {
      (process.env as any).NODE_ENV = "production";
      delete process.env.SESSION_SECRET;
      assert.throws(
        () => sealSessionToken(sampleUser),
        /SESSION_SECRET environment variable is missing/
      );
    } finally {
      (process.env as any).NODE_ENV = origEnv;
      if (origSecret === undefined) {
        delete process.env.SESSION_SECRET;
      } else {
        process.env.SESSION_SECRET = origSecret;
      }
    }
  });
});
