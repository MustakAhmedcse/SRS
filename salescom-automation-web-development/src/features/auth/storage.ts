"use client";

import {
  readLocalStorage,
  removeLocalStorage,
  useLocalStorage,
  writeLocalStorage,
} from "@/hooks/useLocalStorage";
import { SessionSchema, type SessionData } from "./schema";

export const SESSION_STORAGE_KEY = "salescom.session";

// Sync, non-React helpers. Use these from event handlers and mutations
// (writeSession from useLogin.onSuccess, clearSession from useLogout, etc.).
// All in-tab consumers of useSession see the change immediately via the
// shared pub/sub inside useLocalStorage.

export function readSession(): SessionData | null {
  return readLocalStorage(SESSION_STORAGE_KEY, SessionSchema);
}

export function writeSession(session: SessionData): void {
  writeLocalStorage(SESSION_STORAGE_KEY, session);
}

export function clearSession(): void {
  removeLocalStorage(SESSION_STORAGE_KEY);
}

// Reactive hook. Returns null when no session is stored, when stored data
// fails SessionSchema validation, or while rendering on the server.
export function useSession(): SessionData | null {
  const [value] = useLocalStorage<SessionData | null>(SESSION_STORAGE_KEY, {
    defaultValue: null,
    schema: SessionSchema.nullable(),
  });
  return value;
}
