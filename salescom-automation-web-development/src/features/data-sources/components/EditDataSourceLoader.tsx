"use client";

import { useDataSource } from "../hooks";
import { EditDataSourceForm } from "./EditDataSourceForm";

// Two-layer split: the loader handles the async lifecycle (loading,
// error, missing) so the form itself can assume it always has initial
// data. Mounting the form only once data has arrived means
// useForm({ defaultValues }) sees the final shape and a later refetch
// can never overwrite the user's edits.
export function EditDataSourceLoader({ id }: { id: string }) {
  const query = useDataSource(id);

  if (query.isLoading) {
    return (
      <div className="rounded-md border border-dashed border-border bg-muted/40 px-4 py-12 text-center text-sm text-muted-foreground">
        Loading data source…
      </div>
    );
  }

  if (query.error || !query.data) {
    return (
      <div className="rounded-md border border-dashed border-border bg-muted/40 px-4 py-12 text-center text-sm text-muted-foreground">
        Couldn&apos;t load this data source. Try refreshing.
      </div>
    );
  }

  return <EditDataSourceForm initial={query.data} />;
}
