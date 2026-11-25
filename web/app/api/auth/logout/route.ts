import { NextRequest, NextResponse } from "next/server";
import { getSessionCookieOptions } from "@/lib/auth/session";
import { isSameOriginRequest } from "@/lib/auth/csrf";

const NO_STORE_HEADERS = {
  "Cache-Control": "no-store, private",
};

export async function POST(req: NextRequest) {
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

  const cookieOptions = getSessionCookieOptions();
  const response = NextResponse.json(
    {
      success: true,
      message: "با موفقیت از حساب کاربری خارج شدید.",
    },
    { headers: NO_STORE_HEADERS }
  );

  // Expire cookie immediately
  response.cookies.set(cookieOptions.name, "", {
    ...cookieOptions,
    maxAge: 0,
    expires: new Date(0),
  });

  return response;
}
