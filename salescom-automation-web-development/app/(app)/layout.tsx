import { type ReactNode } from "react";
import { requireSession } from "@/lib/auth/session";
import { AppShell } from "@/components/layout/AppShell";
import { AuthProvider } from "@/features/auth/components/AuthProvider";

export default async function AppLayout({
  children,
}: {
  children: ReactNode;
}) {
  await requireSession();
  return (
    <AuthProvider>
      <AppShell>{children}</AppShell>
    </AuthProvider>
  );
}
