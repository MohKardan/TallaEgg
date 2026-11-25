interface RateLimitRecord {
  count: number;
  resetTime: number;
}

const namespaceStores = new Map<string, Map<string, RateLimitRecord>>();
export const MAX_STORE_ENTRIES = 5000;

function extractNamespace(identifier: string): string {
  const parts = identifier.split(":");
  if (parts.length >= 2) {
    return `${parts[0]}:${parts[1]}`;
  }
  return "default";
}

function getBucketStore(namespace: string): Map<string, RateLimitRecord> {
  let store = namespaceStores.get(namespace);
  if (!store) {
    store = new Map<string, RateLimitRecord>();
    namespaceStores.set(namespace, store);
  }
  return store;
}

// Cleanup stale entries across all isolated namespaces every 5 minutes
if (typeof setInterval !== "undefined") {
  setInterval(() => {
    const now = Date.now();
    namespaceStores.forEach((store, ns) => {
      store.forEach((record, key) => {
        if (record.resetTime <= now) {
          store.delete(key);
        }
      });
      if (store.size === 0) {
        namespaceStores.delete(ns);
      }
    });
  }, 5 * 60 * 1000).unref?.();
}

export function _resetRateLimitStoreForTests(): void {
  namespaceStores.clear();
}

export interface RateLimitResult {
  success: boolean;
  limit: number;
  remaining: number;
  resetInSeconds: number;
}

/**
 * In-memory sliding window rate limiter with namespace-isolated, bounded buckets.
 * Separating buckets by prefix (e.g. `auth:ip` vs `auth:id`) prevents spoofed IP
 * flooding from evicting active per-account lockouts.
 */
export function checkRateLimit(
  identifier: string,
  limit = 5,
  windowSeconds = 60
): RateLimitResult {
  const now = Date.now();
  const windowMs = windowSeconds * 1000;
  const namespace = extractNamespace(identifier);
  const store = getBucketStore(namespace);
  const record = store.get(identifier);

  if (!record || record.resetTime <= now) {
    if (record) {
      store.delete(identifier);
    }

    // Prevent memory bloat/OOM under flooding attacks within this namespace only
    if (store.size >= MAX_STORE_ENTRIES) {
      // First evict expired entries in this namespace
      store.forEach((entry, key) => {
        if (entry.resetTime <= now) {
          store.delete(key);
        }
      });

      // If still at capacity, evict the oldest 500 entries in this namespace
      if (store.size >= MAX_STORE_ENTRIES) {
        const keysToEvict = Array.from(store.keys()).slice(0, 500);
        keysToEvict.forEach((k) => store.delete(k));
      }
    }

    // New or expired window
    store.set(identifier, {
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
