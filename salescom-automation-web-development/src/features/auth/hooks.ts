"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useMemo } from "react";
import { toast } from "sonner";
import { ROUTES } from "@/lib/constants";
import { login, logout, verifyAuthToken } from "./api";
import { clearSession, useSession, writeSession } from "./storage";
import type { LoginInput, SessionData } from "./schema";

export function useCurrentUser(): SessionData | null {
  return useSession();
}

export function usePermissions(): Set<number> {
  const session = useSession();
  return useMemo(
    () => new Set(session?.permissions ?? []),
    [session?.permissions],
  );
}

export function useCan(perm: number): boolean {
  const set = usePermissions();
  return set.has(perm);
}

export function useLogin() {
  const router = useRouter();
  return useMutation({
    mutationFn: (input: LoginInput) => login(input),
    onSuccess: ({ data, message }) => {
      if (data.authType === "SSO") {
        // Internal user → 2FA. Hand the browser to the central OTP page; the
        // flow resumes at ROUTES.LOGIN_CALLBACK once OTP passes. No toast /
        // router push — we're navigating away from the app entirely.
        window.location.href = data.redirectUrl;
        return;
      }
      // External/Normal user — session issued in one round-trip.
      writeSession(data.session);
      toast.success(message);
      router.replace(ROUTES.DASHBOARD);
    },
  });
}

// Drives the SSO callback page: exchanges the post-OTP authToken for a
// session, persists it, then lands the user on the dashboard.
export function useVerifyAuthToken() {
  const router = useRouter();
  return useMutation({
    // Silent: the callback page owns the failure toast + redirect so it can
    // show one message before sending the user back to login.
    meta: { silent: true },
    mutationFn: (authToken: string) => verifyAuthToken(authToken),
    onSuccess: ({ data }) => {
      writeSession(data);
      router.replace(ROUTES.DASHBOARD);
    },
  });
}

export function useLogout() {
  const qc = useQueryClient();
  const router = useRouter();
  return useMutation({
    mutationFn: () => logout(),
    onSettled: () => {
      // Clear in this order: persistent store, then in-memory RQ cache for
      // any other-feature data, then route. Storage clear notifies same-tab
      // and cross-tab consumers (via the storage event) to drop their UI.
      clearSession();
      qc.clear();
      router.replace(ROUTES.LOGIN);
    },
  });
}
