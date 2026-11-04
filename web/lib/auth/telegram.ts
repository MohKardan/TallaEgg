import crypto from "crypto";

const recentHashes = new Map<string, number>();
const ALLOWED_FIELDS = new Set([
  "id",
  "first_name",
  "last_name",
  "username",
  "photo_url",
  "auth_date",
]);

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
 * 3. In-memory replay suppression cache rejecting duplicate use of identical hashes.
 * 4. Whitelisted field processing and newline stripping.
 */
export function verifyTelegramAuth(
  payload: Record<string, any>,
  botToken: string,
  maxAgeSeconds = 300
): VerificationResult {
  if (!botToken) {
    return { valid: false, error: "Bot token is not configured on the server." };
  }

  const { hash, ...data } = payload;
  if (!hash || typeof hash !== "string" || !/^[0-9a-f]{64}$/i.test(hash)) {
    return {
      valid: false,
      error: "Missing or invalid cryptographic hash format in Telegram payload.",
    };
  }

  const authDateNum = Number(data.auth_date);
  if (!authDateNum || isNaN(authDateNum)) {
    return { valid: false, error: "Missing or invalid auth_date." };
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

  // Check for immediate replay of identical hash
  if (recentHashes.has(hash)) {
    return {
      valid: false,
      error: "Authentication payload has already been used (replay detected).",
    };
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
  const receivedBuffer = Buffer.from(hash, "hex");

  if (
    calculatedBuffer.length !== receivedBuffer.length ||
    !crypto.timingSafeEqual(calculatedBuffer, receivedBuffer)
  ) {
    return { valid: false, error: "Invalid cryptographic signature." };
  }

  // Record verified hash to prevent replay within validity window
  recentHashes.set(hash, currentTimestamp + maxAgeSeconds);

  // Prune expired hashes
  recentHashes.forEach((expiry, h) => {
    if (expiry <= currentTimestamp) recentHashes.delete(h);
  });

  return {
    valid: true,
    telegramId: Number(data.id),
    username: data.username,
    firstName: data.first_name,
    lastName: data.last_name,
    photoUrl: data.photo_url,
    authDate: authDateNum,
  };
}
