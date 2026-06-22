import { clientFetch } from "@/lib/api/client";
import { unwrapEnvelope } from "@/lib/api/envelope";
import {
  LoginInputSchema,
  LoginResponseEnvelopeSchema,
  VerifyResponseEnvelopeSchema,
  type LoginInput,
  type LoginResult,
  type SessionData,
} from "./schema";

// Pattern for every feature api.ts: clientFetch returns the raw envelope,
// the feature function validates it with its envelopeSchema, then
// unwrapEnvelope throws ApiError on success:false and returns
// `{ data, message }`. Callers get the typed payload plus the backend's
// human-readable message — useful for success toasts on mutations.
//
// `login` resolves to either a ready-to-store session (external/Normal user)
// or an SSO redirect URL (internal/2FA user); the caller branches on
// `data.authType`.
export async function login(
  input: LoginInput,
): Promise<{ data: LoginResult; message: string }> {
  LoginInputSchema.parse(input);
  const envelope = await clientFetch<unknown>("/api/auth/login", {
    method: "POST",
    body: input,
  });
  const parsed = LoginResponseEnvelopeSchema.parse(envelope);
  return unwrapEnvelope(parsed);
}

// Completes the SSO/2FA flow: exchanges the post-OTP `authToken` (delivered
// to the callback page as a query param) for a session. The browser-facing
// route returns the display session directly as `data`.
export async function verifyAuthToken(
  authToken: string,
): Promise<{ data: SessionData; message: string }> {
  const envelope = await clientFetch<unknown>("/api/auth/verify", {
    method: "POST",
    body: { authToken },
  });
  const parsed = VerifyResponseEnvelopeSchema.parse(envelope);
  return unwrapEnvelope(parsed);
}

// The backend has no logout endpoint — the access token IS the session, with
// no server-side revocation. Logout is therefore client-side only: our local
// route handler clears the httpOnly cookie and the caller clears localStorage.
export async function logout(): Promise<void> {
  await clientFetch("/api/auth/logout", { method: "POST" });
}
