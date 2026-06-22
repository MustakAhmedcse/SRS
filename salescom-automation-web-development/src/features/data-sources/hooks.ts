"use client";

import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import type { PageParams } from "@/lib/api/pagination";
import {
  createDataSource,
  getAvailableColumns,
  getAvailableTables,
  getDataSource,
  getDataSources,
  updateDataSource,
} from "./api";
import { dataSourceKeys } from "./keys";
import type { UpdateDataSourceRequest } from "./schema";

// `keepPreviousData` keeps the current page rendered while the next page
// is in flight — without it, every pagination click would flash the
// table back to its empty state.
export function useDataSources(params: PageParams) {
  return useQuery({
    queryKey: dataSourceKeys.list(params),
    queryFn: () => getDataSources(params),
    placeholderData: keepPreviousData,
  });
}

export function useAvailableTables() {
  return useQuery({
    queryKey: dataSourceKeys.availableTables(),
    queryFn: getAvailableTables,
  });
}

// Only fires once a table is selected; the `enabled` guard keeps the
// query inert until then so the select can render without flashing an
// error for missing input. The key always passes a string so the
// factory's type stays clean — the query never actually runs with the
// empty string thanks to `enabled`.
export function useAvailableColumns(tableName: string | undefined) {
  return useQuery({
    queryKey: dataSourceKeys.availableColumns(tableName ?? ""),
    queryFn: () => getAvailableColumns(tableName as string),
    enabled: Boolean(tableName),
  });
}

export function useCreateDataSource() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: createDataSource,
    onSuccess: () => {
      // Broad invalidation — every list page is now stale. We don't
      // touch `available` since the set of backend tables didn't change.
      qc.invalidateQueries({ queryKey: dataSourceKeys.lists() });
    },
  });
}

export function useDataSource(id: string) {
  return useQuery({
    queryKey: dataSourceKeys.detail(id),
    queryFn: () => getDataSource(id),
  });
}

export function useUpdateDataSource(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateDataSourceRequest) =>
      updateDataSource(id, input),
    onSuccess: (data) => {
      // Prime the detail cache with the fresh entity so navigating back
      // to /edit or any future detail view skips an extra fetch.
      qc.setQueryData(dataSourceKeys.detail(id), data);
      // List rows changed (alias/status/description), refetch on visit.
      qc.invalidateQueries({ queryKey: dataSourceKeys.lists() });
    },
  });
}
