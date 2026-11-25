import { NextRequest, NextResponse } from "next/server";
import { verifyTelegramAuth } from "@/lib/auth/telegram";
import {
  sealSessionToken,
  getSessionCookieOptions,
} from "@/lib/auth/session";
import { isSameOriginRequest } from "@/lib/auth/csrf";
import { checkRateLimit } from "@/lib/rate-limit";
import { getUserByTelegramId } from "@/lib/api/users-client";

const NO_STORE_HEADERS = {
  "Cache-Control": "no-store, private",
};

function extractClientIp(req: NextRequest): string {
  const candidates: string[] = [];

  const cfIp = req.headers.get("cf-connecting-ip");
  if (cfIp) candidates.push(cfIp.trim());

  const realIp = req.headers.get("x-real-ip");
  if (realIp) candidates.push(realIp.trim());

  const forwardedFor = req.headers.get("x-forwarded-for");
  if (forwardedFor) {
    // Prefer the rightmost valid IP added by the closest reverse proxy so a
    // client-spoofed leftmost header cannot evade rate limits.
    const parts = forwardedFor
      .split(",")
      .map((part) => part.trim())
      .filter(Boolean);
    for (let i = parts.length - 1; i >= 0; i--) {
      candidates.push(parts[i]);
    }
  }

  for (const candidate of candidates) {
    if (/^[\da-f.:]+$/i.test(candidate) && candidate.length <= 45) {
      return candidate;
    }
  }

  return "127.0.0.1";
}

function mapVerificationErrorToPersian(error?: string): string {
  if (!error) {
    return "امضای دیجیتال تلگرام نامعتبر است یا منقضی شده است.";
  }
  const lower = error.toLowerCase();
  if (lower.includes("not configured")) {
    return "پیکربندی سرویس احراز هویت تلگرام روی سرور ناقص است.";
  }
  if (lower.includes("replay")) {
    return "این درخواست ورود قبلاً استفاده شده است. لطفاً مجدداً روی دکمه ورود تلگرام کلیک کنید.";
  }
  if (lower.includes("expired") || lower.includes("window")) {
    return "زمان اعتبار درخواست ورود تلگرام به پایان رسیده است. لطفاً دوباره تلاش کنید.";
  }
  return "امضای دیجیتال تلگرام نامعتبر است یا منقضی شده است.";
}

const PERSIAN_STATUS_LABELS: Record<string, string> = {
  Pending: "در انتظار تأیید",
  Rejected: "رد شده",
  Suspended: "معلق",
  Blocked: "مسدود",
};

export async function POST(req: NextRequest) {
  // 0. CSRF / Same-Origin protection
  if (!isSameOriginRequest(req)) {
    return NextResponse.json(
      {
        success: false,
        code: "FORBIDDEN_ORIGIN",
        message: "درخواست از مبدأ غیرمجاز رد شد.",
      },
      { status: 403, headers: NO_STORE_HEADERS }
    );
  }

  // 1. Rate limiting check (keyed by IP in isolated `auth:ip` bucket)
  const clientIp = extractClientIp(req);
  const rateLimit = checkRateLimit(`auth:ip:${clientIp}`, 10, 60);

  if (!rateLimit.success) {
    return NextResponse.json(
      {
        success: false,
        code: "RATE_LIMITED",
        message: `تعداد تلاش‌ها بیش از حد مجاز است. لطفاً ${rateLimit.resetInSeconds} ثانیه دیگر دوباره امتحان کنید.`,
      },
      {
        status: 429,
        headers: {
          ...NO_STORE_HEADERS,
          "Retry-After": String(rateLimit.resetInSeconds),
        },
      }
    );
  }

  // 2. Parse request body safely
  let body: unknown;
  try {
    body = await req.json();
  } catch {
    return NextResponse.json(
      {
        success: false,
        code: "INVALID_JSON",
        message: "قالب داده ارسالی نامعتبر است.",
      },
      { status: 400, headers: NO_STORE_HEADERS }
    );
  }

  if (!body || typeof body !== "object" || Array.isArray(body)) {
    return NextResponse.json(
      {
        success: false,
        code: "INVALID_JSON",
        message: "قالب داده ارسالی نامعتبر است.",
      },
      { status: 400, headers: NO_STORE_HEADERS }
    );
  }

  // 3. Cryptographic Telegram signature verification & replay check
  const botToken = process.env.TELEGRAM_BOT_TOKEN || "";
  let verification;
  try {
    verification = verifyTelegramAuth(
      body as Record<string, unknown>,
      botToken,
      300
    );
  } catch {
    return NextResponse.json(
      {
        success: false,
        code: "VERIFICATION_ERROR",
        message: "خطا در پردازش امضای تلگرام.",
      },
      { status: 400, headers: NO_STORE_HEADERS }
    );
  }

  if (!verification.valid || !verification.telegramId) {
    return NextResponse.json(
      {
        success: false,
        code: "INVALID_SIGNATURE",
        message: mapVerificationErrorToPersian(verification.error),
      },
      { status: 401, headers: NO_STORE_HEADERS }
    );
  }

  // 4. Rate-limit per Telegram ID in isolated `auth:id` bucket
  const idRateLimit = checkRateLimit(`auth:id:${verification.telegramId}`, 5, 60);
  if (!idRateLimit.success) {
    return NextResponse.json(
      {
        success: false,
        code: "RATE_LIMITED",
        message: `تلاش بیش از حد برای این شناسه تلگرام. لطفاً ${idRateLimit.resetInSeconds} ثانیه دیگر دوباره امتحان کنید.`,
      },
      {
        status: 429,
        headers: {
          ...NO_STORE_HEADERS,
          "Retry-After": String(idRateLimit.resetInSeconds),
        },
      }
    );
  }

  // 5. Resolve user via Users.Api
  const { user, error } = await getUserByTelegramId(verification.telegramId);

  if (error) {
    return NextResponse.json(
      {
        success: false,
        code: "BACKEND_ERROR",
        message: "خطا در ارتباط با سرویس کاربران. لطفاً دقایقی دیگر تلاش کنید.",
        details: process.env.NODE_ENV === "development" ? error : undefined,
      },
      { status: 502, headers: NO_STORE_HEADERS }
    );
  }

  if (!user) {
    return NextResponse.json(
      {
        success: false,
        code: "USER_NOT_FOUND",
        message:
          "حساب کاربری شما در سامانه یافت نشد. لطفاً ابتدا در ربات تلگرام ثبت‌نام فرمایید.",
      },
      { status: 404, headers: NO_STORE_HEADERS }
    );
  }

  // 6. Verification of active & approved status
  if (!user.isActive || user.status !== "Approved") {
    const statusLabel = PERSIAN_STATUS_LABELS[user.status] || "غیرفعال";
    return NextResponse.json(
      {
        success: false,
        code: "USER_NOT_APPROVED",
        status: user.status,
        message:
          user.status === "Pending"
            ? "حساب کاربری شما در انتظار تأیید مدیریت است. پس از تأیید در ربات، امکان ورود خواهید داشت."
            : `حساب کاربری شما در وضعیت «${statusLabel}» قرار دارد و امکان ورود ندارد.`,
      },
      { status: 403, headers: NO_STORE_HEADERS }
    );
  }

  // 7. Issue short-lived, httpOnly, secure session cookie
  // Acceptance criterion: Browser never holds or receives raw userId
  let sessionToken: string;
  try {
    sessionToken = sealSessionToken({
      userId: user.id,
      telegramId: user.telegramId,
      username: user.username,
      firstName: user.firstName,
      lastName: user.lastName,
      role: user.role,
      status: user.status,
      isActive: user.isActive,
    });
  } catch {
    return NextResponse.json(
      {
        success: false,
        code: "SESSION_CONFIG_ERROR",
        message: "خطا در پیکربندی نشست امن سرور. لطفاً با پشتیبانی تماس بگیرید.",
      },
      { status: 500, headers: NO_STORE_HEADERS }
    );
  }

  const cookieOptions = getSessionCookieOptions();
  const response = NextResponse.json(
    {
      success: true,
      message: "ورود با موفقیت انجام شد.",
      user: {
        telegramId: user.telegramId,
        username: user.username,
        firstName: user.firstName,
        lastName: user.lastName,
        role: user.role,
        status: user.status,
      },
    },
    { headers: NO_STORE_HEADERS }
  );

  response.cookies.set(cookieOptions.name, sessionToken, cookieOptions);
  return response;
}
