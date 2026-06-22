import { type ReactNode } from "react";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { redirectIfAuthenticated } from "@/lib/auth/session";

export default async function AuthLayout({
  children,
}: {
  children: ReactNode;
}) {
  await redirectIfAuthenticated();
  return (
    <div className="relative flex min-h-screen flex-1 items-center justify-center bg-muted/30 p-6">
      <div className="absolute right-4 top-4">
        <ThemeToggle />
      </div>
      <div className="w-full max-w-sm rounded-lg border border-border bg-card p-8 shadow-sm">
        {children}
      </div>
    </div>
  );
}
