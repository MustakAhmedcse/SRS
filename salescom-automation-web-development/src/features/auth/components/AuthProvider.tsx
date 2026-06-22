"use client";

import { type ReactNode, useCallback, useEffect } from "react";
import { useRouter } from "next/navigation";
import { clearSession, useSession } from "../storage";
import { logout as logoutApi } from "../api";
import { ROUTES } from "@/lib/constants";

// setTimeout overflows past this (~24.8 days) and would fire immediately.
// Tokens never live that long, but clamp so a bad date can't misbehave.
const MAX_TIMEOUT_MS = 2_147_483_647;

// Log the user out after this much continuous inactivity, even if the token
// is still valid.
const IDLE_TIMEOUT_MS = 3 * 60 * 60 * 1000; // 3 hours

// Activity that counts as "the user is still here" and resets the idle timer.
const ACTIVITY_EVENTS = [
  "mousemove",
  "mousedown",
  "keydown",
  "scroll",
  "touchstart",
  "click",
] as const;

// Watches the persisted session and forces a logout on two client-side
// triggers while the tab is open:
//   1. Token expiry — the instant `tokenExpireDate` passes.
//   2. Inactivity — IDLE_TIMEOUT_MS of no user interaction.
// The cookie's `expires` covers the closed-tab case (the browser drops it),
// and the global 401 handler in query/client.ts covers tokens revoked
// server-side mid-session.
export function AuthProvider({ children }: { children: ReactNode }) {
  const session = useSession();
  const router = useRouter();

  const forceLogout = useCallback(() => {
    clearSession();
    // Best-effort cookie clear; we redirect regardless.
    logoutApi().catch(() => {});
    router.replace(ROUTES.LOGIN);
  }, [router]);

  // --- Token-expiry watcher ------------------------------------------------
  // Triggers: an immediate check on mount (covers a reload after expiry), a
  // precise timer set to the exact expiry moment, and a re-check when the tab
  // regains focus/visibility — timers don't fire reliably while a tab is
  // backgrounded or the machine sleeps.
  useEffect(() => {
    if (!session) return;
    const expiry = new Date(session.tokenExpireDate).getTime();
    if (Number.isNaN(expiry)) return;

    let timer: ReturnType<typeof setTimeout> | undefined;

    // Logs out and returns true when the token has expired.
    function checkExpiry(): boolean {
      if (Date.now() >= expiry) {
        forceLogout();
        return true;
      }
      return false;
    }

    function arm() {
      const delay = Math.min(Math.max(expiry - Date.now(), 0), MAX_TIMEOUT_MS);
      timer = setTimeout(() => {
        // If a clamped delay fell short of a far-future expiry, re-arm.
        if (!checkExpiry()) arm();
      }, delay);
    }

    if (checkExpiry()) return;
    arm();

    function onWake() {
      checkExpiry();
    }
    document.addEventListener("visibilitychange", onWake);
    window.addEventListener("focus", onWake);

    return () => {
      if (timer) clearTimeout(timer);
      document.removeEventListener("visibilitychange", onWake);
      window.removeEventListener("focus", onWake);
    };
  }, [session, forceLogout]);

  // --- Inactivity watcher --------------------------------------------------
  // Tracks the last activity timestamp and arms a single timer for the
  // remaining idle window. Activity listeners only bump the timestamp (cheap,
  // passive) — the timer re-checks and re-arms when it fires, so we never
  // thrash setTimeout on every mousemove.
  useEffect(() => {
    if (!session) return;

    let lastActivity = Date.now();
    let timer: ReturnType<typeof setTimeout> | undefined;

    function arm() {
      const remaining = IDLE_TIMEOUT_MS - (Date.now() - lastActivity);
      if (remaining <= 0) {
        forceLogout();
        return;
      }
      timer = setTimeout(arm, remaining);
    }

    function onActivity() {
      lastActivity = Date.now();
    }

    for (const event of ACTIVITY_EVENTS) {
      window.addEventListener(event, onActivity, { passive: true });
    }
    arm();

    return () => {
      if (timer) clearTimeout(timer);
      for (const event of ACTIVITY_EVENTS) {
        window.removeEventListener(event, onActivity);
      }
    };
  }, [session, forceLogout]);

  return <>{children}</>;
}
