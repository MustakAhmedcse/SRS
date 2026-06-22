import { cookies } from "next/headers";
import { AUTH_COOKIE } from "@/lib/auth/cookie";
import { env } from "@/lib/env";
import type { ApiEnvelope } from "./envelope";
import { normalizeErrorResponse } from "./errors";
import { buildUrl, type Query } from "./url";

type ServerFetchInit = Omit<RequestInit, "body"> & {
  body?: unknown;
  query?: Query;
};

// Server-side counterpart to clientFetch. Returns the full backend
// envelope on 2xx; throws ApiError on non-2xx. Attaches the bearer
// token from the auth cookie.
export async function serverFetch<T = unknown>(
  path: string,
  init: ServerFetchInit = {},
): Promise<ApiEnvelope<T>> {
  const jar = await cookies();
  const token = jar.get(AUTH_COOKIE)?.value;

  const headers = new Headers(init.headers);
  headers.set("accept", "application/json");
  if (init.body !== undefined) headers.set("content-type", "application/json");
  if (token) headers.set("authorization", `Bearer ${token}`);

  const base = env().BACKEND_API_URL.replace(/\/$/, "");
  const res = await fetch(buildUrl(path, base, init.query), {
    ...init,
    headers,
    body: init.body !== undefined ? JSON.stringify(init.body) : undefined,
  });

  if (!res.ok) throw await normalizeErrorResponse(res);
  if (res.status === 204) {
    return { success: true, message: "", errorCode: null, data: null };
  }
  return (await res.json()) as ApiEnvelope<T>;
}
