"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";

export type Theme = "light" | "dark" | "system";

type ThemeContextValue = {
  /** The user's selection — may be "system". */
  theme: Theme;
  /** What is actually applied to the DOM right now (never "system"). */
  resolvedTheme: "light" | "dark";
  setTheme: (theme: Theme) => void;
};

const STORAGE_KEY = "salescom.theme";

const ThemeContext = createContext<ThemeContextValue | null>(null);

function getSystemTheme(): "light" | "dark" {
  if (typeof window === "undefined") return "light";
  return window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

function applyTheme(theme: Theme): "light" | "dark" {
  const resolved = theme === "system" ? getSystemTheme() : theme;
  const root = document.documentElement;
  root.classList.toggle("dark", resolved === "dark");
  root.style.colorScheme = resolved;
  return resolved;
}

export function ThemeProvider({
  children,
  defaultTheme = "system",
}: {
  children: ReactNode;
  defaultTheme?: Theme;
}) {
  const [theme, setThemeState] = useState<Theme>(defaultTheme);
  const [resolvedTheme, setResolvedTheme] = useState<"light" | "dark">("light");

  // Read persisted choice on mount; the inline boot script in <head> has
  // already applied the right class to <html>, so this just syncs React state.
  useEffect(() => {
    const stored = window.localStorage.getItem(STORAGE_KEY) as Theme | null;
    const initial: Theme =
      stored === "light" || stored === "dark" || stored === "system"
        ? stored
        : defaultTheme;
    setThemeState(initial);
    setResolvedTheme(applyTheme(initial));
  }, [defaultTheme]);

  // Follow system changes only while user is on "system".
  useEffect(() => {
    if (theme !== "system") return;
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const onChange = () => setResolvedTheme(applyTheme("system"));
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, [theme]);

  const setTheme = useCallback((next: Theme) => {
    setThemeState(next);
    window.localStorage.setItem(STORAGE_KEY, next);
    setResolvedTheme(applyTheme(next));
  }, []);

  const value = useMemo<ThemeContextValue>(
    () => ({ theme, resolvedTheme, setTheme }),
    [theme, resolvedTheme, setTheme],
  );

  return (
    <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
  );
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within <ThemeProvider>");
  return ctx;
}

/**
 * Inline script that runs before React hydrates so the correct theme class is
 * on <html> from the very first paint — prevents a light/dark flash.
 *
 * Injected via `dangerouslySetInnerHTML` in app/layout.tsx.
 */
export const themeBootScript = `
(function(){try{
  var s=localStorage.getItem('${STORAGE_KEY}');
  var t=(s==='light'||s==='dark'||s==='system')?s:'system';
  var r=t==='system'?(window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light'):t;
  var d=document.documentElement;
  if(r==='dark')d.classList.add('dark');else d.classList.remove('dark');
  d.style.colorScheme=r;
}catch(e){}})();
`;
