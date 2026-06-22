"use client";

import { type ReactNode } from "react";
import { QueryProvider } from "@/lib/query/provider";
import { Toaster } from "@/components/feedback/Toaster";
import { ThemeProvider } from "@/components/theme/theme-provider";

export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <ThemeProvider defaultTheme="system">
      <QueryProvider>
        {children}
        <Toaster />
      </QueryProvider>
    </ThemeProvider>
  );
}
