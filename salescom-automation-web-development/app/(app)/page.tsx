"use client";

import { useCurrentUser } from "@/features/auth/hooks";

export default function DashboardPage() {
  const user = useCurrentUser();
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">
        Welcome{user ? `, ${user.fullName}` : ""}
      </h1>
      <p className="text-muted-foreground">
        Commission programme configuration and execution will appear here.
      </p>
    </div>
  );
}
