import { clientFetch } from "@/lib/api/client";
import { unwrapEnvelope } from "@/lib/api/envelope";
import type { PageParams } from "@/lib/api/pagination";
import {
  AvailableColumnsEnvelope,
  AvailableTablesEnvelope,
  CreateDataSourceEnvelope,
  DataSourcesPageEnvelope,
  GetDataSourceEnvelope,
  UpdateDataSourceEnvelope,
  type CreateDataSourceRequest,
  type UpdateDataSourceRequest,
} from "./schema";

// Standard feature api.ts pattern (see CLAUDE.md "Response envelope"):
// clientFetch returns the raw envelope, the schema validates it, and
// unwrapEnvelope throws ApiError on success:false and returns
// `{ data, message }`. Here we only need `data`.
//
// The backend takes 1-based `page`; TanStack Table uses 0-based
// `pageIndex`. Convert at this boundary so the rest of the feature
// stays in TanStack's idiom.
export async function getDataSources(params: PageParams) {
  const envelope = await clientFetch<unknown>("/api/data-sources", {
    query: {
      page: params.pageIndex + 1,
      pageSize: params.pageSize,
    },
  });
  const parsed = DataSourcesPageEnvelope.parse(envelope);
  return unwrapEnvelope(parsed).data;
}

export async function getAvailableTables() {
  const envelope = await clientFetch<unknown>(
    "/api/data-sources/available-tables",
  );
  const parsed = AvailableTablesEnvelope.parse(envelope);
  return unwrapEnvelope(parsed).data;
}

export async function getAvailableColumns(tableName: string) {
  const envelope = await clientFetch<unknown>(
    `/api/data-sources/available-tables/${encodeURIComponent(tableName)}/columns`,
  );
  const parsed = AvailableColumnsEnvelope.parse(envelope);
  return unwrapEnvelope(parsed).data;
}

export async function createDataSource(input: CreateDataSourceRequest) {
  const envelope = await clientFetch<unknown>("/api/data-sources", {
    method: "POST",
    body: input,
  });
  const parsed = CreateDataSourceEnvelope.parse(envelope);
  return unwrapEnvelope(parsed).data;
}

export async function getDataSource(id: string) {
  const envelope = await clientFetch<unknown>(
    `/api/data-sources/${encodeURIComponent(id)}`,
  );
  const parsed = GetDataSourceEnvelope.parse(envelope);
  return unwrapEnvelope(parsed).data;
}

export async function updateDataSource(
  id: string,
  input: UpdateDataSourceRequest,
) {
  const envelope = await clientFetch<unknown>(
    `/api/data-sources/${encodeURIComponent(id)}`,
    { method: "PUT", body: input },
  );
  const parsed = UpdateDataSourceEnvelope.parse(envelope);
  return unwrapEnvelope(parsed).data;
}
