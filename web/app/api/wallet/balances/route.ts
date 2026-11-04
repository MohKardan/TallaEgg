import { NextResponse } from "next/server";
import { getSessionUser } from "@/lib/auth/session";
import { getUserWallets } from "@/lib/api/wallet-client";

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
      { status: 401 }
    );
  }

  // 2. Enforce active & approved state
  if (!session.isActive || session.status !== "Approved") {
    return NextResponse.json(
      {
        success: false,
        code: "ACCOUNT_NOT_ACTIVE",
        status: session.status,
        message: "حساب کاربری شما در وضعیت مجاز برای مشاهده موجودی قرار ندارد.",
      },
      { status: 403 }
    );
  }

  // 3. Fetch wallets from Wallet.Api over loopback using the internal userId
  const { wallets, error } = await getUserWallets(session.userId);

  if (error) {
    return NextResponse.json(
      {
        success: false,
        code: "WALLET_SERVICE_ERROR",
        message: "خطا در برقراری ارتباط با سرویس کیف پول.",
        details: process.env.NODE_ENV === "development" ? error : undefined,
      },
      { status: 502 }
    );
  }

  return NextResponse.json({
    success: true,
    wallets,
    summary: {
      accountStatus: session.status,
      timestamp: new Date().toISOString(),
    },
  });
}
