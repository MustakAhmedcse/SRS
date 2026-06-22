import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ROUTES } from "@/lib/constants";

export default function NotAuthorizedPage() {
  return (
    <div className="mx-auto max-w-md space-y-4 text-center">
      <h1 className="text-2xl font-semibold">Not authorized</h1>
      <p className="text-muted-foreground">
        You don&apos;t have permission to access this page. If you think this is a
        mistake, contact your administrator.
      </p>
      <Button asChild variant="outline">
        <Link href={ROUTES.DASHBOARD}>Back to dashboard</Link>
      </Button>
    </div>
  );
}
