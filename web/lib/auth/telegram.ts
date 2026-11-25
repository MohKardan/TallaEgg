import crypto from "crypto";

const recentHashes = new Map<string, number>();
export const MAX_RECENT_HASHES = 5000;
const ALLOWED_FIELDS = new Set([
  "id",
  "first_name",
  "last_name",
  "username",
  "photo_url",
  "auth_date",
]);

export function _resetRecentHashesForTests(): void {
  recentHashes.clear();
}

export interface TelegramAuthPayload {
  id: number | string;
  first_name?: string;
  last_name?: string;
  username?: string;
  photo_url?: string;
  auth_date: number | string;
  hash: string;
}

export interface VerificationResult {
  valid: boolean;
  error?: string;
  telegramId?: number;
  username?: string;
  firstName?: string;
  lastName?: string;
  photoUrl?: string;
  authDate?: number;
}

/**
 * Validates a Telegram Login Widget authentication payload.
 * Enforces:
 * 1. HMAC-SHA256 signature verification using the SHA256 hash of the Telegram bot token.
 * 2. Strict replay attack protection (rejects auth_date older than maxAgeSeconds or more than 30s in the future).
 * 3. Case-normalized, size-bounded in-memory replay suppression cache that fails closed under saturation
 *    rather than evicting active non-expired hashes.
 * 4. Positive safe integer validation for Telegram user IDs and whitelisted field processing.
 */
export function verifyTelegramAuth(
  payload: Record<string, any>,
  botToken: string,
  maxAgeSeconds = 300,
  maxCacheEntries = MAX_RECENT_HASHES
): VerificationResult {
  if (!botToken) {
    return { valid: false, error: "Bot token is not configured on the server." };
  }

  if (!payload || typeof payload !== "object") {
    return { valid: false, error: "Invalid Telegram authentication payload." };
  }

  const { hash, ...data } = payload;
  if (!hash || typeof hash !== "string" || !/^[0-9a-f]{64}$/i.test(hash)) {
    return {
      valid: false,
      error: "Missing or invalid cryptographic hash format in Telegram payload.",
    };
  }

  const normalizedHash = hash.toLowerCase();

  const authDateNum = Number(data.auth_date);
  if (!authDateNum || !Number.isFinite(authDateNum)) {
    return { valid: false, error: "Missing or invalid auth_date." };
  }

  const telegramId = Number(data.id);
  if (!Number.isSafeInteger(telegramId) || telegramId <= 0) {
    return { valid: false, error: "Invalid Telegram user id." };
  }

  // Replay protection: verify auth_date is within window
  const currentTimestamp = Math.floor(Date.now() / 1000);
  const timeDifference = currentTimestamp - authDateNum;
  if (timeDifference > maxAgeSeconds || timeDifference < -30) {
    return {
      valid: false,
      error: `Auth date expired or out of allowed window (${timeDifference}s difference).`,
    };
  }

  // Check for immediate replay of identical hash (case-insensitive via normalization)
  const existingExpiry = recentHashes.get(normalizedHash);
  if (existingExpiry !== undefined) {
    if (existingExpiry > currentTimestamp) {
      return {
        valid: false,
        error: "Authentication payload has already been used (replay detected).",
      };
    }
    recentHashes.delete(normalizedHash);
  }

  // Build the data-check-string (whitelisted keys sorted alphabetically, key=value joined with \n)
  const dataCheckString = Object.keys(data)
    .filter(
      (key) =>
        ALLOWED_FIELDS.has(key) &&
        data[key] !== undefined &&
        data[key] !== null
    )
    .sort()
    .map((key) => `${key}=${String(data[key]).replace(/[\r\n]+/g, " ")}`)
    .join("\n");

  // Secret key = SHA256(botToken)
  const secretKey = crypto.createHash("sha256").update(botToken).digest();

  // HMAC-SHA256(dataCheckString, secretKey)
  const calculatedHmac = crypto
    .createHmac("sha256", secretKey)
    .update(dataCheckString)
    .digest("hex");

  // Constant-time comparison
  const calculatedBuffer = Buffer.from(calculatedHmac, "hex");
  const receivedBuffer = Buffer.from(normalizedHash, "hex");

  if (
    calculatedBuffer.length !== receivedBuffer.length ||
    !crypto.timingSafeEqual(calculatedBuffer, receivedBuffer)
  ) {
    return { valid: false, error: "Invalid cryptographic signature." };
  }

  // Prune expired hashes first.
  recentHashes.forEach((expiry, h) => {
    if (expiry <= currentTimestamp) recentHashes.delete(h);
  });

  // Fail closed when saturated with active non-expired hashes — never evict
  // valid entries early, or captured payloads could be replayed within the window.
  if (recentHashes.size >= maxCacheEntries) {
    return {
      valid: false,
      error: "Replay protection cache saturated.",
    };
  }

  // Record verified normalized hash to prevent replay within validity window
  recentHashes.set(
    normalizedHash,
    Math.max(currentTimestamp, authDateNum) + maxAgeSeconds
  );

  return {
    valid: true,
    telegramId,
    username: data.username,
    firstName: data.first_name,
    lastName: data.last_name,
    photoUrl: data.photo_url,
    authDate: authDateNum,
  };
}
