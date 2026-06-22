"use client";

import { Suspense, useEffect, useRef } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import { useVerifyAuthToken } from "@/features/auth/hooks";
import { toUserMessage } from "@/lib/api/user-error";
import { ROUTES } from "@/lib/constants";

// Landing page for the SSO/2FA flow. The central OTP service redirects the
// browser here with `?authToken=...` after the user passes OTP. We exchange
// that token for a session (which sets the cookie + persists the display
// session) and the hook then routes on to the dashboard.
function SsoCallback() {
  const params = useSearchParams();
  const authToken = params.get("authToken");
  const verify = useVerifyAuthToken();
  const router = useRouter();

  // Guard against React 18 StrictMode double-invoke / re-renders firing the
  // one-shot exchange twice.
  const started = useRef(false);
  useEffect(() => {
    if (started.current || !authToken) return;
    started.current = true;
    verify.mutate(authToken);
  }, [authToken, verify]);

  // Any dead-end — no token, or a failed exchange — shows a toast then sends
  // the user back to login to start over. Guarded so the toast/redirect fire
  // exactly once (StrictMode re-invoke / re-renders).
  const failed = !authToken || verify.isError;
  const handledFailure = useRef(false);
  useEffect(() => {
    if (!failed || handledFailure.current) return;
    handledFailure.current = true;
    toast.error(
      authToken
        ? toUserMessage(verify.error)
        : "Missing authentication token. Please sign in again.",
    );
    router.replace(ROUTES.LOGIN);
  }, [failed, authToken, verify.error, router]);

  const message = failed
    ? "Sign-in could not be completed. Redirecting to login…"
    : "Completing sign-in…";

  return (
    <div className="space-y-2 text-center">
      <h1 className="text-xl font-semibold">Signing you in</h1>
      <p className="text-sm text-muted-foreground">{message}</p>
    </div>
  );
}

export default function SsoCallbackPage() {
  return (
    <Suspense
      fallback={
        <div className="space-y-2 text-center">
          <h1 className="text-xl font-semibold">Signing you in</h1>
          <p className="text-sm text-muted-foreground">Completing sign-in…</p>
        </div>
      }
    >
      <SsoCallback />
    </Suspense>
  );
}
