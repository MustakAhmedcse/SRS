import { z, type ZodTypeAny } from "zod";

// Shape of `data` for any paginated backend endpoint. Pair with
// envelopeSchema so the full response is
//   ApiEnvelope<PaginatedData<Row>> =
//     { success, message, errorCode,
//       data: { items, page, pageSize, totalCount } }
//
// Field names mirror the backend exactly (per CLAUDE.md). `items` is
// the slice for the current page; `totalCount` is the total rows across
// all pages, used to compute how many pages exist. `page` and `pageSize`
// echo the request so callers can verify what was actually served.

export type PaginatedData<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export function paginatedDataSchema<T extends ZodTypeAny>(rowSchema: T) {
  return z.object({
    items: z.array(rowSchema),
    page: z.number().int().positive(),
    pageSize: z.number().int().positive(),
    totalCount: z.number().int().nonnegative(),
  });
}

// Query params every paginated endpoint accepts. `pageIndex` is 0-based
// to match TanStack Table's PaginationState. Backends that take 1-based
// `page` should add `page: pageIndex + 1` in their api.ts wrapper.
export type PageParams = {
  pageIndex: number;
  pageSize: number;
};
