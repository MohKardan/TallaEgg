import { describe, it } from "node:test";
import assert from "node:assert";
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

  it("throws in production when SESSION_SECRET is missing", () => {
    const origEnv = process.env.NODE_ENV;
    const origSecret = process.env.SESSION_SECRET;
    try {
      (process.env as any).NODE_ENV = "production";
      delete process.env.SESSION_SECRET;
      assert.throws(() => sealSessionToken(sampleUser), /SESSION_SECRET environment variable is missing/);
    } finally {
      (process.env as any).NODE_ENV = origEnv;
      process.env.SESSION_SECRET = origSecret;
    }
  });
});
