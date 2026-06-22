"use client";

import { Monitor, Moon, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useTheme, type Theme } from "./theme-provider";

const NEXT: Record<Theme, Theme> = {
  light: "dark",
  dark: "system",
  system: "light",
};

const LABEL: Record<Theme, string> = {
  light: "Light",
  dark: "Dark",
  system: "System",
};

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const Icon = theme === "dark" ? Moon : theme === "light" ? Sun : Monitor;

  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={() => setTheme(NEXT[theme])}
      aria-label={`Theme: ${LABEL[theme]} (click to change)`}
      title={`Theme: ${LABEL[theme]}`}
    >
      <Icon className="h-4 w-4" />
    </Button>
  );
}
