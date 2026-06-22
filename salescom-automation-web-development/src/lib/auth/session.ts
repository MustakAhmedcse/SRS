import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AUTH_COOKIE } from "./cookie";
import { ROUTES } from "@/lib/constants";

// Server-side session is cookie-existence only. Backend has no `/auth/me`,
// so we can't resolve user identity or permissions on the server. Anything
// finer-grained than "has the user logged in?" must be done client-side
// after `<AuthProvider>` hydrates the session from localStorage.
export async function getSession(): Promise<{ hasToken: boolean }> {
  const jar = await cookies();
  return { hasToken: jar.get(AUTH_COOKIE) !== undefined };
}

export async function requireSession(): Promise<void> {
  const { hasToken } = await getSession();
  if (!hasToken) redirect(ROUTES.LOGIN);
}

// Inverse of requireSession — bounces an already-authenticated user away
// from auth-only pages (login, register, etc.) to the dashboard. Used in
// the (auth) route-group layout. Like requireSession, this only checks
// cookie existence; the AuthProvider expiry watcher and the global 401
// handler will catch a stale/invalid token shortly after.
export async function redirectIfAuthenticated(): Promise<void> {
  const { hasToken } = await getSession();
  if (hasToken) redirect(ROUTES.DASHBOARD);
}
