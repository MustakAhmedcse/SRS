"use client";

import { type ReactNode, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useCan } from "../hooks";
import { useSession } from "../storage";
import { ROUTES } from "@/lib/constants";

// Client-side page guard. Use at the top of any protected page that requires
// a specific permission:
//
//   export default function ProgrammesPage() {
//     return (
//       <RequirePermission permission={PERM.PROGRAMMES_VIEW}>
//         <ProgrammesList />
//       </RequirePermission>
//     );
//   }
//
// Until the session hydrates (very brief, only on a hard reload before
// AuthProvider mounts) we render nothing — never the page content for a
// user we haven't yet confirmed has the permission.
export function RequirePermission({
  permission,
  children,
}: {
  permission: number;
  children: ReactNode;
}) {
  const session = useSession();
  const allowed = useCan(permission);
  const router = useRouter();

  useEffect(() => {
    if (session && !allowed) router.replace(ROUTES.NOT_AUTHORIZED);
  }, [session, allowed, router]);

  if (!session) return null;
  if (!allowed) return null;
  return <>{children}</>;
}
