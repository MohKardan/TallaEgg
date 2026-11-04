import { NextResponse } from "next/server";
import { getSessionCookieOptions } from "@/lib/auth/session";

export async function POST() {
  const cookieOptions = getSessionCookieOptions();
  const response = NextResponse.json({
    success: true,
    message: "با موفقیت از حساب کاربری خارج شدید.",
  });

  // Expire cookie immediately
  response.cookies.set(cookieOptions.name, "", {
    ...cookieOptions,
    maxAge: 0,
    expires: new Date(0),
  });

  return response;
}
