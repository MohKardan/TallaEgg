import { NextResponse } from "next/server";
import { getSessionUser } from "@/lib/auth/session";
import { getUserByTelegramId } from "@/lib/api/users-client";
import { getUserWallets } from "@/lib/api/wallet-client";

const NO_STORE_HEADERS = {
  "Cache-Control": "no-store, private",
};

export async function GET() {
  // 1. Resolve user from the secure session cookie (never from client request parameters)
  const session = await getSessionUser();

  if (!session) {
    return NextResponse.json(
      {
        success: false,
        code: "UNAUTHORIZED",
        message: "لطفاً ابتدا وارد حساب کاربری خود شوید.",
      },
      { status: 401, headers: NO_STORE_HEADERS }
    );
  }

  // 2. Re-verify live account status against Users.Api so suspended/blocked
  // accounts lose wallet access immediately rather than waiting for cookie expiry.
  const { user, error: userError } = await getUserByTelegramId(
    session.telegramId
  );

  if (userError) {
    return NextResponse.json(
      {
        success: false,
        code: "USER_SERVICE_ERROR",
        message: "خطا در بررسی وضعیت حساب کاربری.",
        details: process.env.NODE_ENV === "development" ? userError : undefined,
      },
      { status: 502, headers: NO_STORE_HEADERS }
    );
  }

  if (!user || !user.isActive || user.status !== "Approved") {
    return NextResponse.json(
      {
        success: false,
        code: "ACCOUNT_NOT_ACTIVE",
        status: user?.status ?? "Unknown",
        message: "حساب کاربری شما در وضعیت مجاز برای مشاهده موجودی قرار ندارد.",
      },
      { status: 403, headers: NO_STORE_HEADERS }
    );
  }

  // 3. Fetch wallets from Wallet.Api over loopback using the verified internal userId
  const { wallets, error } = await getUserWallets(user.id);

  if (error) {
    return NextResponse.json(
      {
        success: false,
        code: "WALLET_SERVICE_ERROR",
        message: "خطا در برقراری ارتباط با سرویس کیف پول.",
        details: process.env.NODE_ENV === "development" ? error : undefined,
      },
      { status: 502, headers: NO_STORE_HEADERS }
    );
  }

  return NextResponse.json(
    {
      success: true,
      wallets,
      summary: {
        accountStatus: user.status,
        timestamp: new Date().toISOString(),
      },
    },
    { headers: NO_STORE_HEADERS }
  );
}
