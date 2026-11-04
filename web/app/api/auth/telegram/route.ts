import { NextRequest, NextResponse } from "next/server";
import { verifyTelegramAuth } from "@/lib/auth/telegram";
import {
  sealSessionToken,
  getSessionCookieOptions,
} from "@/lib/auth/session";
import { checkRateLimit } from "@/lib/rate-limit";
import { getUserByTelegramId } from "@/lib/api/users-client";

export async function POST(req: NextRequest) {
  // 1. Rate limiting check (keyed by IP)
  const forwardedFor = req.headers.get("x-forwarded-for");
  const ip = forwardedFor ? forwardedFor.split(",")[0].trim() : "127.0.0.1";
  const rateLimit = checkRateLimit(`auth:${ip}`, 5, 60);

  if (!rateLimit.success) {
    return NextResponse.json(
      {
        success: false,
        code: "RATE_LIMITED",
        message: `تعداد تلاش‌های ناموفق بیش از حد مجاز است. لطفاً ${rateLimit.resetInSeconds} ثانیه دیگر دوباره امتحان کنید.`,
      },
      {
        status: 429,
        headers: {
          "Retry-After": String(rateLimit.resetInSeconds),
        },
      }
    );
  }

  // 2. Parse request body
  let body: any;
  try {
    body = await req.json();
  } catch {
    return NextResponse.json(
      { success: false, error: "قالب داده ارسالی نامعتبر است." },
      { status: 400 }
    );
  }

  // 3. Cryptographic Telegram signature verification & replay check
  const botToken = process.env.TELEGRAM_BOT_TOKEN || "";
  const verification = verifyTelegramAuth(body, botToken, 300);

  if (!verification.valid || !verification.telegramId) {
    return NextResponse.json(
      {
        success: false,
        code: "INVALID_SIGNATURE",
        message:
          verification.error ||
          "امضای دیجیتال تلگرام نامعتبر است یا منقضی شده است.",
      },
      { status: 401 }
    );
  }

  // 4. Resolve user via Users.Api
  const { user, error } = await getUserByTelegramId(verification.telegramId);

  if (error) {
    return NextResponse.json(
      {
        success: false,
        code: "BACKEND_ERROR",
        message: "خطا در ارتباط با سرویس کاربران. لطفاً دقایقی دیگر تلاش کنید.",
        details: process.env.NODE_ENV === "development" ? error : undefined,
      },
      { status: 502 }
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
      { status: 404 }
    );
  }

  // 5. Verification of active & approved status
  if (!user.isActive || user.status !== "Approved") {
    return NextResponse.json(
      {
        success: false,
        code: "USER_NOT_APPROVED",
        status: user.status,
        message:
          user.status === "Pending"
            ? "حساب کاربری شما در انتظار تأیید مدیریت است. پس از تأیید در ربات، امکان ورود خواهید داشت."
            : `حساب کاربری شما در وضعیت «${user.status}» قرار دارد و امکان ورود ندارد.`,
      },
      { status: 403 }
    );
  }

  // 6. Issue short-lived, httpOnly, secure session cookie
  // Acceptance criterion: Browser never holds or receives raw userId
  const sessionToken = sealSessionToken({
    userId: user.id,
    telegramId: user.telegramId,
    username: user.username,
    firstName: user.firstName,
    lastName: user.lastName,
    role: user.role,
    status: user.status,
    isActive: user.isActive,
  });

  const cookieOptions = getSessionCookieOptions();
  const response = NextResponse.json({
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
  });

  response.cookies.set(cookieOptions.name, sessionToken, cookieOptions);
  return response;
}
