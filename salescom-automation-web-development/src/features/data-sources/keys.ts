import type { PageParams } from "@/lib/api/pagination";

// Single source of truth for every TanStack Query key in this feature.
// Hierarchical on purpose — invalidating a parent invalidates all its
// descendants, so a mutation can pick the broadest safe scope without
// retyping (and risking drift from) the keys the queries use.
//
//   dataSourceKeys.all           → invalidate everything in the feature
//   dataSourceKeys.lists()       → all paginated list queries (any params)
//   dataSourceKeys.list(params)  → one specific page
//   dataSourceKeys.available()   → both the table list and any columns
//   dataSourceKeys.availableTables()
//   dataSourceKeys.availableColumns(tableName)
export const dataSourceKeys = {
  all: ["data-sources"] as const,
  lists: () => [...dataSourceKeys.all, "list"] as const,
  list: (params: PageParams) => [...dataSourceKeys.lists(), params] as const,
  details: () => [...dataSourceKeys.all, "detail"] as const,
  detail: (id: string) => [...dataSourceKeys.details(), id] as const,
  available: () => [...dataSourceKeys.all, "available"] as const,
  availableTables: () =>
    [...dataSourceKeys.available(), "tables"] as const,
  availableColumns: (tableName: string) =>
    [...dataSourceKeys.available(), tableName, "columns"] as const,
};
