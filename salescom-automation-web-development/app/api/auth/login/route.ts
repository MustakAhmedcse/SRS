import { NextResponse } from "next/server";
import { cookies } from "next/headers";
import { env } from "@/lib/env";
import { AUTH_COOKIE, authCookieOptionsExpiringAt } from "@/lib/auth/cookie";
import {
  BackendLoginEnvelopeSchema,
  LoginInputSchema,
  toSessionData,
  type LoginInput,
} from "@/features/auth/schema";

// Frontend-only dev bypass. Set MOCK_AUTH=1 in .env.local to skip the real
// backend and accept any credentials. Off by default so production can't
// accidentally ship with auth disabled. Mocks the external/Normal branch —
// the SSO branch needs the real central-login service.
function mockBackendLogin(input: LoginInput) {
  return {
    success: true,
    message: "Login successful.",
    errorCode: null,
    data: {
      authType: "Normal",
      session: {
        accessToken: "mock-token",
        accessTokenExpiresAtUtc: new Date(
          Date.now() + 2 * 24 * 60 * 60 * 1000,
          // Date.now() + 120 * 1000,
        ).toISOString(),
        rights: [
          900021, 900022, 900023, 900024, 900025, 900026, 900027, 900028,
          900029, 900030, 900031, 900032, 900033, 900034, 900035, 900036,
          900037,
        ],
        fullName: "Muntakim",
        userName: input.username,
        lastLoginSuccessAtUtc: null,
        lastLoginFailedAtUtc: null,
      },
    },
  };
}

export async function POST(req: Request) {
  const json = await req.json().catch(() => null);
  const parsed = LoginInputSchema.safeParse(json);
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

  let rawResponse: unknown;

  if (process.env.MOCK_AUTH === "1") {
    rawResponse = mockBackendLogin(parsed.data);
  } else {
    const backendRes = await fetch(
      `${env().BACKEND_API_URL.replace(/\/$/, "")}/api/account/login`,
      {
        method: "POST",
        headers: {
          "content-type": "application/json",
          accept: "application/json",
        },
        body: JSON.stringify(parsed.data),
      },
    );

    rawResponse = await backendRes.json().catch(() => null);

    if (!backendRes.ok) {
      const body = (rawResponse ?? {}) as {
        message?: string;
        errorCode?: string | null;
      };
      return NextResponse.json(
        {
          success: false,
          message: body?.message ?? "Login failed",
          errorCode: body?.errorCode ?? null,
          data: null,
        },
        { status: backendRes.status },
      );
    }
  }

  const result = BackendLoginEnvelopeSchema.safeParse(rawResponse);
  if (!result.success) {
    return NextResponse.json(
      {
        success: false,
        message: "Unexpected login response from backend",
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

  // Internal user → 2FA. No session yet; pass the redirect URL straight to
  // the browser, which hands control to the central OTP page.
  if (envelope.data.authType === "SSO") {
    return NextResponse.json({
      success: true,
      message: envelope.message,
      errorCode: null,
      data: { authType: "SSO", redirectUrl: envelope.data.redirectUrl },
    });
  }

  // External user → session issued. Move the access token into the httpOnly
  // cookie (expiring exactly when the backend says it does) and return the
  // display session to the browser.
  const { session } = envelope.data;
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
    data: { authType: "Normal", session: toSessionData(session) },
  });
}
