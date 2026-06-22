"use client";

import { useEffect, useState, type ReactNode } from "react";
import { usePathname } from "next/navigation";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";

export function AppShell({ children }: { children: ReactNode }) {
  // Sidebar drawer state — only meaningful on <lg. On lg+ the sidebar is
  // always visible inline; this state is a no-op there.
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const pathname = usePathname();

  // Auto-close the drawer after a successful navigation so users see the
  // new page immediately instead of having to dismiss the drawer first.
  useEffect(() => {
    setSidebarOpen(false);
  }, [pathname]);

  return (
    <div className="flex h-full min-h-screen flex-col">
      <Topbar onMenuClick={() => setSidebarOpen((v) => !v)} />
      <div className="flex flex-1 overflow-hidden">
        <Sidebar open={sidebarOpen} onClose={() => setSidebarOpen(false)} />
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
    </div>
  );
}
