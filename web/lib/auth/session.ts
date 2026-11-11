import crypto from "crypto";
import { cookies } from "next/headers";

export const SESSION_COOKIE_NAME = "zarorbit_session";
export const SESSION_MAX_AGE_SECONDS = 4 * 60 * 60; // 4 hours

export interface SessionUser {
  userId: string;
  telegramId: number;
  username?: string;
  firstName?: string;
  lastName?: string;
  role: string;
  status: string;
  isActive: boolean;
}

export interface SessionPayload extends SessionUser {
  exp: number;
  iat: number;
}

function getSecretKey(): Buffer {
  const secret = process.env.SESSION_SECRET;
  if (!secret) {
    if (process.env.NODE_ENV === "production") {
      throw new Error(
        "FATAL: SESSION_SECRET environment variable is missing in production."
      );
    }
    return crypto
      .createHash("sha256")
      .update("dev-only-ephemeral-key-do-not-use-in-prod")
      .digest();
  }
  if (secret.length < 32) {
    throw new Error(
      "FATAL: SESSION_SECRET must be at least 32 characters long."
    );
  }
  return crypto.createHash("sha256").update(secret).digest();
}

/**
 * Creates an encrypted and authenticated token using AES-256-GCM.
 * The payload is completely protected against inspection and tampering.
 */
export function sealSessionToken(user: SessionUser): string {
  const key = getSecretKey();
  const iv = crypto.randomBytes(12); // 96-bit IV for AES-GCM
  const iat = Math.floor(Date.now() / 1000);
  const exp = iat + SESSION_MAX_AGE_SECONDS;

  const payload: SessionPayload = {
    ...user,
    iat,
    exp,
  };

  const cipher = crypto.createCipheriv("aes-256-gcm", key, iv);
  const jsonString = JSON.stringify(payload);
  const encrypted = Buffer.concat([
    cipher.update(jsonString, "utf8"),
    cipher.final(),
  ]);
  const tag = cipher.getAuthTag();

  // Format: base64url(iv).base64url(tag).base64url(encrypted)
  return [
    iv.toString("base64url"),
    tag.toString("base64url"),
    encrypted.toString("base64url"),
  ].join(".");
}

/**
 * Unseals and verifies an AES-256-GCM token.
 * Returns the session payload or null if invalid, expired, or tampered with.
 */
export function unsealSessionToken(token: string): SessionPayload | null {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;

    const [ivB64, tagB64, dataB64] = parts;
    const iv = Buffer.from(ivB64, "base64url");
    const tag = Buffer.from(tagB64, "base64url");
    const encrypted = Buffer.from(dataB64, "base64url");

    if (iv.length !== 12 || tag.length !== 16) return null;

    const key = getSecretKey();
    const decipher = crypto.createDecipheriv("aes-256-gcm", key, iv);
    decipher.setAuthTag(tag);

    const decrypted = Buffer.concat([
      decipher.update(encrypted),
      decipher.final(),
    ]);

    const payload: SessionPayload = JSON.parse(decrypted.toString("utf8"));
    const now = Math.floor(Date.now() / 1000);

    if (payload.exp && payload.exp < now) {
      return null; // Expired
    }

    return payload;
  } catch {
    return null;
  }
}

/**
 * Retrieves the current session user from the incoming request cookies.
 */
export async function getSessionUser(): Promise<SessionPayload | null> {
  const cookieStore = cookies();
  const sessionCookie = cookieStore.get(SESSION_COOKIE_NAME);
  if (!sessionCookie || !sessionCookie.value) {
    return null;
  }
  return unsealSessionToken(sessionCookie.value);
}

/**
 * Cookie options for setting httpOnly, secure, and short-lived session cookies.
 */
export function getSessionCookieOptions() {
  const isProduction = process.env.NODE_ENV === "production";
  return {
    name: SESSION_COOKIE_NAME,
    httpOnly: true,
    secure: isProduction,
    sameSite: "lax" as const,
    path: "/",
    maxAge: SESSION_MAX_AGE_SECONDS,
  };
}
