import { describe, it } from "node:test";
import assert from "node:assert";
import { isSameOriginRequest } from "../lib/auth/csrf";

function makeRequest(
  headers: Record<string, string>,
  url = "https://zarorbit.ir/api/auth/telegram"
): Request {
  return new Request(url, { method: "POST", headers });
}

describe("CSRF / Same-Origin Request Validation", () => {
  it("allows same-origin browser requests matching Host", () => {
    const req = makeRequest({
      host: "zarorbit.ir",
      origin: "https://zarorbit.ir",
      "sec-fetch-site": "same-origin",
    });
    assert.strictEqual(isSameOriginRequest(req), true);
  });

  it("allows same-origin requests behind a reverse proxy using X-Forwarded-Host", () => {
    const req = makeRequest(
      {
        host: "127.0.0.1:3000",
        "x-forwarded-host": "zarorbit.ir",
        origin: "https://zarorbit.ir",
        "sec-fetch-site": "same-origin",
      },
      "http://127.0.0.1:3000/api/auth/logout"
    );
    assert.strictEqual(isSameOriginRequest(req), true);
  });

  it("rejects requests with Sec-Fetch-Site: cross-site", () => {
    const reqWithOrigin = makeRequest({
      host: "zarorbit.ir",
      origin: "https://zarorbit.ir",
      "sec-fetch-site": "cross-site",
    });
    assert.strictEqual(isSameOriginRequest(reqWithOrigin), false);

    const reqWithoutOrigin = makeRequest({
      host: "zarorbit.ir",
      "sec-fetch-site": "cross-site",
    });
    assert.strictEqual(isSameOriginRequest(reqWithoutOrigin), false);
  });

  it("rejects requests whose Origin host does not match the target host", () => {
    const req = makeRequest({
      host: "zarorbit.ir",
      origin: "https://evil.example",
      "sec-fetch-site": "same-site",
    });
    assert.strictEqual(isSameOriginRequest(req), false);
  });

  it("rejects null or non-HTTP(S) Origin headers", () => {
    const nullOrigin = makeRequest({
      host: "zarorbit.ir",
      origin: "null",
    });
    assert.strictEqual(isSameOriginRequest(nullOrigin), false);

    const fileOrigin = makeRequest({
      host: "zarorbit.ir",
      origin: "file:///etc/passwd",
    });
    assert.strictEqual(isSameOriginRequest(fileOrigin), false);
  });

  it("allows missing Origin only when Sec-Fetch-Site is not cross-site", () => {
    const reqNoHeaders = makeRequest({ host: "zarorbit.ir" });
    assert.strictEqual(isSameOriginRequest(reqNoHeaders), true);

    const reqSameOrigin = makeRequest({
      host: "zarorbit.ir",
      "sec-fetch-site": "same-origin",
    });
    assert.strictEqual(isSameOriginRequest(reqSameOrigin), true);
  });
});
