export const AUTH_COOKIE = "salescom_token";

const baseOptions = {
  httpOnly: true as const,
  secure: process.env.NODE_ENV === "production",
  sameSite: "lax" as const,
  path: "/",
};

// Cookie expires exactly when the backend token does. If parsing fails we
// fall back to a session cookie (no `expires`) so the browser drops it on
// close — safer than guessing a duration.
export function authCookieOptionsExpiringAt(expireIso: string) {
  const expiry = new Date(expireIso);
  if (Number.isNaN(expiry.getTime())) return baseOptions;
  return { ...baseOptions, expires: expiry };
}
