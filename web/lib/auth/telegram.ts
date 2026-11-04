import crypto from "crypto";

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
 * 2. Strict replay attack protection (rejects auth_date older than maxAgeSeconds, default 300s).
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
  if (!hash) {
    return { valid: false, error: "Missing hash in Telegram payload." };
  }

  const authDateNum = Number(data.auth_date);
  if (!authDateNum || isNaN(authDateNum)) {
    return { valid: false, error: "Missing or invalid auth_date." };
  }

  // Replay protection: verify auth_date is within window
  const currentTimestamp = Math.floor(Date.now() / 1000);
  const timeDifference = Math.abs(currentTimestamp - authDateNum);
  if (timeDifference > maxAgeSeconds) {
    return {
      valid: false,
      error: `Auth date expired or out of allowed window (${timeDifference}s > ${maxAgeSeconds}s).`,
    };
  }

  // Build the data-check-string (keys sorted alphabetically, key=value joined with \n)
  const dataCheckString = Object.keys(data)
    .filter((key) => data[key] !== undefined && data[key] !== null)
    .sort()
    .map((key) => `${key}=${data[key]}`)
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
