import type { ApiEnvelope } from "./envelope";
import { normalizeErrorResponse } from "./errors";
import { buildUrl, type Query } from "./url";

type ClientFetchInit = Omit<RequestInit, "body"> & {
  body?: unknown;
  query?: Query;
};

// Returns the full backend envelope on 2xx responses; callers MUST check
// `envelope.success` before reading `envelope.data`. On non-2xx (network
// failure, 4xx, 5xx) throws ApiError carrying the parsed message/errorCode
// — keeps the global 401 handler in lib/query/client.ts and TanStack
// Query's retry logic working.
export async function clientFetch<T = unknown>(
  path: string,
  init: ClientFetchInit = {},
): Promise<ApiEnvelope<T>> {
  const headers = new Headers(init.headers);
  headers.set("accept", "application/json");
  if (init.body !== undefined) headers.set("content-type", "application/json");

  const res = await fetch(buildUrl(path, window.location.origin, init.query), {
    ...init,
    headers,
    body: init.body !== undefined ? JSON.stringify(init.body) : undefined,
    credentials: "same-origin",
  });

  if (!res.ok) throw await normalizeErrorResponse(res);

  // 204 — no envelope possible; synthesize a successful empty one so
  // callers don't have to branch on the absence of a body.
  if (res.status === 204) {
    return { success: true, message: "", errorCode: null, data: null };
  }
  return (await res.json()) as ApiEnvelope<T>;
}
