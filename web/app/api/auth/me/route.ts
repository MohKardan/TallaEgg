import { NextResponse } from "next/server";
import { getSessionUser } from "@/lib/auth/session";

export async function GET() {
  const session = await getSessionUser();

  if (!session) {
    return NextResponse.json(
      { authenticated: false, message: "احراز هویت نشده‌اید." },
      { status: 401 }
    );
  }

  // Acceptance criterion: Never send raw userId to the browser
  return NextResponse.json({
    authenticated: true,
    user: {
      telegramId: session.telegramId,
      username: session.username,
      firstName: session.firstName,
      lastName: session.lastName,
      role: session.role,
      status: session.status,
    },
  });
}
