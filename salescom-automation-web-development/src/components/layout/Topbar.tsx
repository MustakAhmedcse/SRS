"use client";

import { LogOut, Menu } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ThemeToggle } from "@/components/theme/theme-toggle";
import { useCurrentUser, useLogout } from "@/features/auth/hooks";

export function Topbar({ onMenuClick }: { onMenuClick?: () => void }) {
  const user = useCurrentUser();
  const logout = useLogout();
  return (
    <header className="flex h-16 shrink-0 items-center justify-between border-b border-border bg-card px-4 sm:px-6">
      <div className="flex items-center gap-3">
        {onMenuClick && (
          // Hamburger toggle — only visible below the lg breakpoint where the
          // sidebar collapses to a slide-in drawer.
          <Button
            variant="ghost"
            size="icon"
            className="lg:hidden"
            onClick={onMenuClick}
            aria-label="Toggle navigation"
          >
            <Menu className="h-5 w-5" />
          </Button>
        )}
        <div className="font-semibold">Salescom Commission Automation</div>
      </div>
      <div className="flex items-center gap-3">
        <ThemeToggle />
        {user && (
          <>
            <div className="hidden text-sm sm:block">
              <span className="font-medium">{user.fullName}</span>
              <span className="ml-2 text-muted-foreground">
                @{user.userName}
              </span>
            </div>
            <Button
              variant="default"
              size="sm"
              onClick={() => logout.mutate()}
              disabled={logout.isPending}
              className="gap-2"
            >
              <LogOut className="h-4 w-4" />
              <span className="hidden sm:inline">Sign out</span>
            </Button>
          </>
        )}
      </div>
    </header>
  );
}
