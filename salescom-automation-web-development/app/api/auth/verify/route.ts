import { NextResponse } from "next/server";
import { cookies } from "next/headers";
import { env } from "@/lib/env";
import { AUTH_COOKIE, authCookieOptionsExpiringAt } from "@/lib/auth/cookie";
import {
  BackendVerifyEnvelopeSchema,
  VerifyInputSchema,
  toSessionData,
} from "@/features/auth/schema";

// Completes the SSO/2FA flow. The central OTP page redirects the browser back
// to the callback page with an `authToken`; the callback POSTs it here and we
// exchange it for a session. Unlike /api/account/login, verify-auth-token
// returns the session AS `data` (not nested under data.session).
export async function POST(req: Request) {
  const json = await req.json().catch(() => null);
  const parsed = VerifyInputSchema.safeParse(json);
  if (!parsed.success) {
    return NextResponse.json(
      {
        success: false,
        message: "Invalid request",
        errorCode: "VALIDATION_ERROR",
        data: null,
        fieldErrors: parsed.error.flatten().fieldErrors,
      },
      { status: 400 },
    );
  }

  const backendRes = await fetch(
    `${env().BACKEND_API_URL.replace(/\/$/, "")}/api/account/verify-auth-token`,
    {
      method: "POST",
      headers: {
        "content-type": "application/json",
        accept: "application/json",
      },
      body: JSON.stringify(parsed.data),
    },
  );

  const rawResponse = await backendRes.json().catch(() => null);

  if (!backendRes.ok) {
    const body = (rawResponse ?? {}) as {
      message?: string;
      errorCode?: string | null;
    };
    return NextResponse.json(
      {
        success: false,
        message: body?.message ?? "Verification failed",
        errorCode: body?.errorCode ?? null,
        data: null,
      },
      { status: backendRes.status },
    );
  }

  const result = BackendVerifyEnvelopeSchema.safeParse(rawResponse);
  if (!result.success) {
    return NextResponse.json(
      {
        success: false,
        message: "Unexpected verification response from backend",
        errorCode: "BACKEND_SHAPE_MISMATCH",
        data: null,
      },
      { status: 502 },
    );
  }

  const envelope = result.data;
  if (!envelope.success || !envelope.data) {
    return NextResponse.json(
      {
        success: false,
        message: envelope.message,
        errorCode: envelope.errorCode ?? null,
        data: null,
      },
      { status: 200 },
    );
  }

  // Move the access token into the httpOnly cookie, return the display session.
  const session = envelope.data;
  const jar = await cookies();
  jar.set(
    AUTH_COOKIE,
    session.accessToken,
    authCookieOptionsExpiringAt(session.accessTokenExpiresAtUtc),
  );

  return NextResponse.json({
    success: true,
    message: envelope.message,
    errorCode: null,
    data: toSessionData(session),
  });
}
