interface RateLimitRecord {
  count: number;
  resetTime: number;
}

const memoryStore = new Map<string, RateLimitRecord>();

// Cleanup stale entries every 5 minutes
if (typeof setInterval !== "undefined") {
  setInterval(() => {
    const now = Date.now();
    memoryStore.forEach((record, key) => {
      if (record.resetTime <= now) {
        memoryStore.delete(key);
      }
    });
  }, 5 * 60 * 1000).unref?.();
}

export interface RateLimitResult {
  success: boolean;
  limit: number;
  remaining: number;
  resetInSeconds: number;
}

/**
 * In-memory sliding window rate limiter.
 * Mitigates brute-force attacks against sensitive endpoints like Telegram auth.
 */
export function checkRateLimit(
  identifier: string,
  limit = 5,
  windowSeconds = 60
): RateLimitResult {
  const now = Date.now();
  const windowMs = windowSeconds * 1000;
  const record = memoryStore.get(identifier);

  if (!record || record.resetTime <= now) {
    // New or expired window
    memoryStore.set(identifier, {
      count: 1,
      resetTime: now + windowMs,
    });
    return {
      success: true,
      limit,
      remaining: limit - 1,
      resetInSeconds: windowSeconds,
    };
  }

  if (record.count >= limit) {
    const resetInSeconds = Math.max(
      1,
      Math.ceil((record.resetTime - now) / 1000)
    );
    return {
      success: false,
      limit,
      remaining: 0,
      resetInSeconds,
    };
  }

  record.count += 1;
  const resetInSeconds = Math.max(
    1,
    Math.ceil((record.resetTime - now) / 1000)
  );

  return {
    success: true,
    limit,
    remaining: limit - record.count,
    resetInSeconds,
  };
}
