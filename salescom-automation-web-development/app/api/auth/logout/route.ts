import { NextResponse } from "next/server";
import { cookies } from "next/headers";
import { AUTH_COOKIE } from "@/lib/auth/cookie";

export async function POST() {
  const jar = await cookies();
  jar.delete(AUTH_COOKIE);
  return NextResponse.json({
    success: true,
    message: "Logged out",
    errorCode: null,
    data: null,
  });
}
