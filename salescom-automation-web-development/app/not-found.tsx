import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ROUTES } from "@/lib/constants";

export default function NotFound() {
  return (
    <div className="flex min-h-screen flex-1 flex-col items-center justify-center gap-4 p-6 text-center">
      <h1 className="text-3xl font-semibold">Page not found</h1>
      <p className="text-muted-foreground">
        The page you were looking for doesn&apos;t exist.
      </p>
      <Button asChild variant="outline">
        <Link href={ROUTES.DASHBOARD}>Back home</Link>
      </Button>
    </div>
  );
}
