import { z } from "zod";
import { envelopeSchema } from "@/lib/api/envelope";

// Login request. Backend rules: username non-empty ≤ 128, password
// non-empty ≤ 256, rememberMe optional (defaults false).
export const LoginInputSchema = z.object({
  username: z.string().min(1, "Required").max(128),
  password: z.string().min(1, "Required").max(256),
  rememberMe: z.boolean().optional(),
});

// Token-exchange request for the SSO/2FA flow. The central OTP page
// redirects back with `authToken`; we POST it to obtain a session.
export const VerifyInputSchema = z.object({
  authToken: z.string().min(1),
});

// Raw session object exactly as the backend returns it (camelCase). Shared
// by both auth endpoints, but in different positions:
//   POST /api/account/login          → nested under data.session (Normal users)
//   POST /api/account/verify-auth-token → returned AS data (post-OTP)
// `accessToken` is the whole session — there is no refresh token.
export const BackendSessionSchema = z.object({
  accessToken: z.string().min(1),
  accessTokenExpiresAtUtc: z.string(),
  rights: z.array(z.number().int()),
  fullName: z.string(),
  userName: z.string(),
  lastLoginSuccessAtUtc: z.string().nullable().optional(),
  lastLoginFailedAtUtc: z.string().nullable().optional(),
});

// Display session persisted to localStorage after the token is stripped into
// the httpOnly cookie. `permissions` / `tokenExpireDate` keep the names the
// rest of the app already consumes (mapped from the backend's `rights` /
// `accessTokenExpiresAtUtc`). UI-only — not a security boundary.
export const SessionSchema = z.object({
  fullName: z.string(),
  userName: z.string(),
  permissions: z.array(z.number().int()),
  tokenExpireDate: z.string(),
  lastLoginSuccessAtUtc: z.string().nullable().optional(),
  lastLoginFailedAtUtc: z.string().nullable().optional(),
});

// --- Backend login envelope (route handler ↔ backend) --------------------
// The server decides per user whether 2FA is required, so login has two
// success shapes discriminated by `authType`:
//   Normal → external user, session issued immediately (data.session)
//   SSO    → internal user, must redirect to the central OTP page
export const BackendLoginDataSchema = z.discriminatedUnion("authType", [
  z.object({ authType: z.literal("Normal"), session: BackendSessionSchema }),
  z.object({ authType: z.literal("SSO"), redirectUrl: z.string().min(1) }),
]);
export const BackendLoginEnvelopeSchema = envelopeSchema(BackendLoginDataSchema);

// verify-auth-token returns the session AS data (note the shape difference
// from login, which nests it under data.session).
export const BackendVerifyEnvelopeSchema = envelopeSchema(BackendSessionSchema);

// --- Client-facing envelopes (route handler → browser) -------------------
// Mirror the two login branches, but with the token already stripped: Normal
// carries the display session, SSO carries the redirect URL.
export const LoginResultSchema = z.discriminatedUnion("authType", [
  z.object({ authType: z.literal("Normal"), session: SessionSchema }),
  z.object({ authType: z.literal("SSO"), redirectUrl: z.string().min(1) }),
]);
export const LoginResponseEnvelopeSchema = envelopeSchema(LoginResultSchema);

// verify-auth-token (browser-facing) returns the display session as data.
export const VerifyResponseEnvelopeSchema = envelopeSchema(SessionSchema);

// Maps a raw backend session to the display session persisted client-side:
// drops the token, renames rights → permissions and
// accessTokenExpiresAtUtc → tokenExpireDate.
export function toSessionData(s: BackendSession): SessionData {
  return {
    fullName: s.fullName,
    userName: s.userName,
    permissions: s.rights,
    tokenExpireDate: s.accessTokenExpiresAtUtc,
    lastLoginSuccessAtUtc: s.lastLoginSuccessAtUtc ?? null,
    lastLoginFailedAtUtc: s.lastLoginFailedAtUtc ?? null,
  };
}

export type LoginInput = z.infer<typeof LoginInputSchema>;
export type VerifyInput = z.infer<typeof VerifyInputSchema>;
export type BackendSession = z.infer<typeof BackendSessionSchema>;
export type SessionData = z.infer<typeof SessionSchema>;
export type LoginResult = z.infer<typeof LoginResultSchema>;
