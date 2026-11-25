import { NextResponse } from "next/server";
import { getSessionUser } from "@/lib/auth/session";

const NO_STORE_HEADERS = {
  "Cache-Control": "no-store, private",
};

export async function GET() {
  const session = await getSessionUser();

  if (!session) {
    return NextResponse.json(
      { authenticated: false, message: "احراز هویت نشده‌اید." },
      { status: 401, headers: NO_STORE_HEADERS }
    );
  }

  // Acceptance criterion: Never send raw userId to the browser
  return NextResponse.json(
    {
      authenticated: true,
      user: {
        telegramId: session.telegramId,
        username: session.username,
        firstName: session.firstName,
        lastName: session.lastName,
        role: session.role,
        status: session.status,
      },
    },
    { headers: NO_STORE_HEADERS }
  );
}
