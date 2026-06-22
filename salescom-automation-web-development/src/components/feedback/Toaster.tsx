"use client";

import { Toaster as SonnerToaster } from "sonner";
import { useTheme } from "@/components/theme/theme-provider";

// Sonner's `richColors` prop forces its own bright green/red/yellow palette,
// which clashes with the project's muted brand tokens. Instead, we drop
// richColors and style toasts from the project's CSS variables so they
// inherit brand + dark-mode automatically:
//   - surface  → --card / --card-foreground
//   - border   → --border
//   - status   → --status-success / --status-danger / --status-warning /
//                --status-info (defined in app/globals.css)
// Each variant gets a colored left bar + matching icon, which keeps the
// neutral surface but still signals success/error/etc at a glance.
export function Toaster() {
  const { resolvedTheme } = useTheme();
  return (
    <SonnerToaster
      theme={resolvedTheme}
      position="top-center"
      closeButton
      toastOptions={{
        classNames: {
          toast:
            "group rounded-md border border-border bg-card text-card-foreground shadow-md",
          title: "text-sm font-medium",
          description: "text-sm text-muted-foreground",
          actionButton:
            "bg-primary text-primary-foreground hover:opacity-90",
          cancelButton:
            "bg-muted text-muted-foreground hover:bg-muted/80",
          closeButton:
            "bg-card text-muted-foreground border-border hover:bg-muted",
          success:
            "border-l-4 !border-l-[color:var(--status-success)] [&_[data-icon]]:text-[color:var(--status-success)]",
          error:
            "border-l-4 !border-l-[color:var(--status-danger)] [&_[data-icon]]:text-[color:var(--status-danger)]",
          warning:
            "border-l-4 !border-l-[color:var(--status-warning)] [&_[data-icon]]:text-[color:var(--status-warning)]",
          info: "border-l-4 !border-l-[color:var(--status-info)] [&_[data-icon]]:text-[color:var(--status-info)]",
        },
      }}
    />
  );
}
