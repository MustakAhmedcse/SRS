"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  ChevronDown,
  Database,
  LayoutDashboard,
  type LucideIcon,
} from "lucide-react";
import { useCan } from "@/features/auth/hooks";
import { PERM } from "@/features/auth/permissions";
import { ROUTES } from "@/lib/constants";
import { cn } from "@/lib/utils";

// A nav entry is either a leaf (`href`) or a group (`children`). A group
// renders as a collapsible header; the children are permission-gated
// individually inside their <NavSubLink>.
type LeafItem = {
  href: string;
  label: string;
  permission?: number;
};

type GroupItem = {
  label: string;
  icon: LucideIcon;
  children: LeafItem[];
};

type NavItem = (LeafItem & { icon: LucideIcon }) | GroupItem;

const items: NavItem[] = [
  { href: ROUTES.DASHBOARD, label: "Dashboard", icon: LayoutDashboard },
  {
    label: "Data sources",
    icon: Database,
    children: [
      {
        href: ROUTES.DATA_SOURCES,
        label: "All data sources",
        permission: PERM.DATA_SOURCES_VIEW,
      },
      {
        href: ROUTES.DATA_SOURCES_NEW,
        label: "Add data source",
        permission: PERM.DATA_SOURCES_CREATE,
      },
    ],
  },
  // Uncomment as feature slices land. Use PERM.* from features/auth/permissions.
  // { href: "/programmes", label: "Programmes", icon: Briefcase, permission: PERM.PROGRAMMES_VIEW },
  // { href: "/runs", label: "Runs", icon: PlayCircle, permission: PERM.RUNS_VIEW },
  // { href: "/reports", label: "Reports", icon: FileBarChart, permission: PERM.REPORTS_VIEW },
];

function isGroup(item: NavItem): item is GroupItem {
  return "children" in item;
}

// Two layout modes from the same component:
//   - lg+: static side-by-side with main content (`lg:static`).
//   - <lg: fixed slide-in drawer below the topbar, with a backdrop.
// AppShell owns `open` so the hamburger in Topbar can toggle it; we close
// on Escape, backdrop click, and successful navigation (handled in AppShell).
export function Sidebar({
  open,
  onClose,
}: {
  open: boolean;
  onClose: () => void;
}) {
  const pathname = usePathname();

  useEffect(() => {
    if (!open) return;
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") onClose();
    }
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  return (
    <>
      {/* Backdrop — only rendered when open, only visible <lg. */}
      {open && (
        <button
          type="button"
          className="fixed inset-0 z-20 bg-black/40 lg:hidden"
          onClick={onClose}
          aria-label="Close navigation"
        />
      )}

      <aside
        className={cn(
          "z-30 flex w-64 shrink-0 flex-col border-r border-border bg-card",
          // Mobile: fixed drawer that slides in from the left, sits below the topbar.
          "fixed bottom-0 left-0 top-16 transform transition-transform duration-200",
          open ? "translate-x-0" : "-translate-x-full",
          // Desktop: static side-by-side, no transform, always visible.
          "lg:static lg:top-0 lg:translate-x-0 lg:transition-none",
        )}
      >
        <nav className="flex flex-1 flex-col gap-1 p-3">
          {items.map((item) =>
            isGroup(item) ? (
              <NavGroup key={item.label} group={item} pathname={pathname} />
            ) : (
              <NavLink
                key={item.href}
                href={item.href}
                label={item.label}
                icon={item.icon}
                permission={item.permission}
                active={pathname === item.href}
              />
            ),
          )}
        </nav>
      </aside>
    </>
  );
}

function NavGroup({
  group,
  pathname,
}: {
  group: GroupItem;
  pathname: string;
}) {
  const containsActive = group.children.some((c) => c.href === pathname);
  // Before the user clicks, the group simply follows the route (open when
  // the active page lives inside the group). After the user clicks, their
  // explicit choice sticks. This avoids a setState-in-effect and the
  // cascading-render warning that pattern triggers.
  const [override, setOverride] = useState<boolean | null>(null);
  const open = override ?? containsActive;

  const Icon = group.icon;

  return (
    <div>
      <button
        type="button"
        onClick={() => setOverride(!open)}
        aria-expanded={open}
        className={cn(
          "flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
          containsActive
            ? "text-foreground"
            : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
        )}
      >
        <Icon className="h-4 w-4" />
        <span className="flex-1 text-left">{group.label}</span>
        <ChevronDown
          className={cn(
            "h-4 w-4 transition-transform",
            open ? "rotate-0" : "-rotate-90",
          )}
        />
      </button>
      {open && (
        <div className="mt-1 ml-6 flex flex-col gap-1 border-l border-border pl-3">
          {group.children.map((child) => (
            <NavSubLink
              key={child.href}
              href={child.href}
              label={child.label}
              permission={child.permission}
              active={pathname === child.href}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function NavLink({
  href,
  label,
  icon: Icon,
  permission,
  active,
}: {
  href: string;
  label: string;
  icon: LucideIcon;
  permission?: number;
  active: boolean;
}) {
  // useCan is always called (hooks rules); the gate below ignores its result
  // when no permission is required on the item.
  const allowed = useCan(permission ?? Number.NaN);
  if (permission !== undefined && !allowed) return null;
  return (
    <Link
      href={href}
      className={cn(
        "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
        active
          ? "bg-accent text-accent-foreground"
          : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
      )}
    >
      <Icon className="h-4 w-4" />
      {label}
    </Link>
  );
}

function NavSubLink({
  href,
  label,
  permission,
  active,
}: {
  href: string;
  label: string;
  permission?: number;
  active: boolean;
}) {
  const allowed = useCan(permission ?? Number.NaN);
  if (permission !== undefined && !allowed) return null;
  return (
    <Link
      href={href}
      className={cn(
        "rounded-md px-3 py-1.5 text-sm transition-colors",
        active
          ? "bg-accent font-medium text-accent-foreground"
          : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
      )}
    >
      {label}
    </Link>
  );
}
