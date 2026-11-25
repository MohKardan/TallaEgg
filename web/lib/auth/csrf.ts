import type { NextRequest } from "next/server";

type RequestLike = Pick<NextRequest, "headers" | "url">;

function getExpectedHost(req: RequestLike): string | null {
  const forwardedHost = req.headers.get("x-forwarded-host");
  if (forwardedHost) {
    const first = forwardedHost.split(",")[0]?.trim().toLowerCase();
    if (first) return first;
  }

  const hostHeader = req.headers.get("host")?.trim().toLowerCase();
  if (hostHeader) {
    return hostHeader;
  }

  try {
    return new URL(req.url).host.toLowerCase();
  } catch {
    return null;
  }
}

/**
 * Validates that a mutating browser request originates from the same origin.
 * - Rejects requests explicitly marked `Sec-Fetch-Site: cross-site`.
 * - When `Origin` is present, requires an HTTP(S) URL whose host matches the
 *   request's `X-Forwarded-Host`, `Host`, or URL host.
 * - Allows missing `Origin` only when `Sec-Fetch-Site` is not `cross-site`.
 */
export function isSameOriginRequest(req: RequestLike): boolean {
  const secFetchSite = req.headers.get("sec-fetch-site")?.trim().toLowerCase();
  if (secFetchSite === "cross-site") {
    return false;
  }

  const origin = req.headers.get("origin")?.trim();
  if (!origin) {
    return true;
  }

  let originUrl: URL;
  try {
    originUrl = new URL(origin);
  } catch {
    return false;
  }

  if (originUrl.protocol !== "http:" && originUrl.protocol !== "https:") {
    return false;
  }

  const expectedHost = getExpectedHost(req);
  if (!expectedHost) {
    return false;
  }

  return originUrl.host.toLowerCase() === expectedHost;
}
