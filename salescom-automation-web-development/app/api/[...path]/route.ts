import { cookies } from "next/headers";
import { AUTH_COOKIE } from "@/lib/auth/cookie";
import { env } from "@/lib/env";

// Generic same-origin proxy to the backend. Forwards everything under
// /api/* (that doesn't have a more specific route handler) straight
// through to ${BACKEND_API_URL}/<path>, attaching the bearer token from
// the httpOnly auth cookie. The browser never talks to the backend
// directly — same-origin requests mean cookies + CORS Just Work.
//
// Auth routes (/api/auth/login, /api/auth/logout) have dedicated handlers
// with special token-handling logic and win the route match by being
// more specific than this catch-all.

const PASS_REQUEST_HEADERS = ["accept", "content-type"];
const PASS_RESPONSE_HEADERS = ["content-type", "cache-control"];
const METHODS_WITHOUT_BODY = new Set(["GET", "HEAD"]);

// Common envelope shape so the browser's clientFetch / toUserMessage
// can handle proxy-side failures the same as backend-side ones.
function envelopeError(
  message: string,
  errorCode: string,
  status: number,
) {
  return Response.json(
    { success: false, message, errorCode, data: null },
    { status },
  );
}

async function proxy(
  req: Request,
  ctx: { params: Promise<{ path: string[] }> },
) {
  const { path } = await ctx.params;
  const jar = await cookies();
  const token = jar.get(AUTH_COOKIE)?.value;

  const base = env().BACKEND_API_URL.replace(/\/$/, "");
  const incoming = new URL(req.url);
  const backendUrl = `${base}/api/${path.join("/")}${incoming.search}`;

  const headers = new Headers();
  for (const name of PASS_REQUEST_HEADERS) {
    const v = req.headers.get(name);
    if (v) headers.set(name, v);
  }
  if (token) headers.set("authorization", `Bearer ${token}`);

  const body = METHODS_WITHOUT_BODY.has(req.method)
    ? undefined
    : await req.arrayBuffer();

  let backendRes: Response;
  try {
    backendRes = await fetch(backendUrl, {
      method: req.method,
      headers,
      body,
    });
  } catch (err) {
    // Connection failures (TLS, DNS, refused, timeout) end up here. They
    // mean we never reached the backend — distinct from a 5xx response.
    console.error(`[proxy] fetch failed for ${req.method} ${backendUrl}`, err);
    return envelopeError(
      "Can't reach the backend service. Please try again.",
      "UPSTREAM_UNREACHABLE",
      502,
    );
  }

  // Buffer the body instead of piping the backend stream straight into
  // `new Response`. Piping the stream sounds efficient but is a footgun
  // here: when the backend returns chunked transfer or compressed
  // content, Node's fetch decompresses transparently while leaving
  // `content-length` set to the *compressed* size. Forwarding that
  // mismatched length crashes the response writer and surfaces as an
  // opaque 500 with no useful logs. Admin API payloads are small —
  // we'd rather pay the buffer cost than chase that bug again.
  let bytes: ArrayBuffer;
  try {
    bytes = await backendRes.arrayBuffer();
  } catch (err) {
    console.error(
      `[proxy] failed to read response body from ${backendUrl}`,
      err,
    );
    return envelopeError(
      "Couldn't read response from the backend service.",
      "UPSTREAM_BAD_RESPONSE",
      502,
    );
  }

  const responseHeaders = new Headers();
  for (const name of PASS_RESPONSE_HEADERS) {
    const v = backendRes.headers.get(name);
    if (v) responseHeaders.set(name, v);
  }

  return new Response(bytes, {
    status: backendRes.status,
    headers: responseHeaders,
  });
}

export {
  proxy as GET,
  proxy as POST,
  proxy as PUT,
  proxy as PATCH,
  proxy as DELETE,
};
