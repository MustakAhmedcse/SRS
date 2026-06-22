"use client";

import { useCallback, useSyncExternalStore } from "react";
import type { ZodType } from "zod";

// Same-tab subscribers, keyed by storage key. The browser's `storage` event
// only fires in *other* tabs, so we need our own pub/sub for the writing tab.
const subscribers = new Map<string, Set<() => void>>();

// Caches the parsed value per key so getSnapshot returns a stable reference
// between calls when the underlying storage hasn't changed. Without this,
// useSyncExternalStore would see a new object every render and loop.
const snapshotCache = new Map<string, { raw: string | null; parsed: unknown }>();

function subscribeKey(key: string, cb: () => void) {
  let set = subscribers.get(key);
  if (!set) {
    set = new Set();
    subscribers.set(key, set);
  }
  set.add(cb);
  return () => {
    set.delete(cb);
    if (set.size === 0) subscribers.delete(key);
  };
}

function notifyKey(key: string) {
  snapshotCache.delete(key);
  subscribers.get(key)?.forEach((cb) => cb());
}

function parseRaw<T>(raw: string | null, schema?: ZodType<T>): T | null {
  if (raw === null) return null;
  let json: unknown;
  try {
    json = JSON.parse(raw);
  } catch {
    return null;
  }
  if (!schema) return json as T;
  const result = schema.safeParse(json);
  return result.success ? result.data : null;
}

function getCachedSnapshot<T>(key: string, schema?: ZodType<T>): T | null {
  if (typeof window === "undefined") return null;
  const raw = window.localStorage.getItem(key);
  const cached = snapshotCache.get(key);
  if (cached && cached.raw === raw) return cached.parsed as T | null;
  const parsed = parseRaw(raw, schema);
  snapshotCache.set(key, { raw, parsed });
  return parsed;
}

export function readLocalStorage<T>(
  key: string,
  schema?: ZodType<T>,
): T | null {
  if (typeof window === "undefined") return null;
  return parseRaw<T>(window.localStorage.getItem(key), schema);
}

export function writeLocalStorage<T>(key: string, value: T): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // storage full / disabled — swallow; the call site can't usefully recover
  }
  notifyKey(key);
}

export function removeLocalStorage(key: string): void {
  if (typeof window === "undefined") return;
  try {
    window.localStorage.removeItem(key);
  } catch {
    // ignore
  }
  notifyKey(key);
}

type Options<T> = {
  defaultValue: T;
  schema?: ZodType<T>;
};

type Setter<T> = (next: T | ((prev: T) => T)) => void;

export function useLocalStorage<T>(
  key: string,
  options: Options<T>,
): [T, Setter<T>, () => void] {
  const { defaultValue, schema } = options;

  const subscribe = useCallback(
    (cb: () => void) => {
      const unsubInternal = subscribeKey(key, cb);
      const onStorage = (e: StorageEvent) => {
        if (e.key === key || e.key === null) {
          snapshotCache.delete(key);
          cb();
        }
      };
      window.addEventListener("storage", onStorage);
      return () => {
        unsubInternal();
        window.removeEventListener("storage", onStorage);
      };
    },
    [key],
  );

  const getSnapshot = useCallback((): T => {
    const value = getCachedSnapshot<T>(key, schema);
    return value ?? defaultValue;
  }, [key, schema, defaultValue]);

  const getServerSnapshot = useCallback((): T => defaultValue, [defaultValue]);

  const value = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  const setValue = useCallback<Setter<T>>(
    (next) => {
      const current = getCachedSnapshot<T>(key, schema) ?? defaultValue;
      const computed =
        typeof next === "function" ? (next as (p: T) => T)(current) : next;
      writeLocalStorage(key, computed);
    },
    [key, schema, defaultValue],
  );

  const clear = useCallback(() => removeLocalStorage(key), [key]);

  return [value, setValue, clear];
}
