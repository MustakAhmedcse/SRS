"use client";

import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
  type OnChangeFn,
  type PaginationState,
} from "@tanstack/react-table";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { DEFAULT_PAGE_SIZE } from "@/lib/constants";
import { cn } from "@/lib/utils";

// Generic, server-side-paginated data table built on TanStack Table.
//
// Server-side means: `data` only holds the CURRENT page's rows, and the
// caller refetches whenever pagination changes. The caller owns pagination
// state (so it can be persisted in the URL, synced across components,
// keyed into TanStack Query, etc).
//
// Recommended caller pattern with TanStack Query:
//
//   const [pagination, setPagination] = useState<PaginationState>({
//     pageIndex: 0,
//     pageSize: DEFAULT_PAGE_SIZE,
//   });
//   const query = useQuery({
//     queryKey: ["reports", pagination],
//     queryFn: () => getReports(pagination),
//     placeholderData: keepPreviousData, // smooth page transitions
//   });
//   return (
//     <DataTable
//       columns={reportColumns}
//       data={query.data?.items ?? []}
//       totalCount={query.data?.totalCount ?? 0}
//       pagination={pagination}
//       onPaginationChange={setPagination}
//       isLoading={query.isFetching}
//     />
//   );

const DEFAULT_PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

export type DataTableProps<TData, TValue> = {
  columns: ColumnDef<TData, TValue>[];
  data: TData[];
  totalCount: number;
  pagination: PaginationState;
  onPaginationChange: OnChangeFn<PaginationState>;
  isLoading?: boolean;
  emptyMessage?: string;
  pageSizeOptions?: number[];
  // Use the row's stable id (e.g. `(row) => row.id`) so React Query +
  // table state survive page changes cleanly. Defaults to row index.
  getRowId?: (row: TData, index: number) => string;
  className?: string;
};

export function DataTable<TData, TValue>({
  columns,
  data,
  totalCount,
  pagination,
  onPaginationChange,
  isLoading = false,
  emptyMessage = "No results.",
  pageSizeOptions = DEFAULT_PAGE_SIZE_OPTIONS,
  getRowId,
  className,
}: DataTableProps<TData, TValue>) {
  // TanStack Table's useReactTable returns an object with live method
  // bindings that React Compiler can't safely auto-memoize, so it would
  // skip compiling this whole function. Opting out explicitly keeps the
  // warning quiet and the behavior unchanged — the rest of the file
  // (DataTablePagination etc.) is still eligible for compilation.
  "use no memo";

  const pageCount = Math.max(1, Math.ceil(totalCount / pagination.pageSize));

  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data,
    columns,
    pageCount,
    state: { pagination },
    onPaginationChange,
    getCoreRowModel: getCoreRowModel(),
    manualPagination: true,
    getRowId,
  });

  const rows = table.getRowModel().rows;
  const isEmpty = !isLoading && rows.length === 0;

  return (
    <div className={cn("space-y-3", className)}>
      <div className="rounded-md border border-table-border">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <TableHead key={header.id} colSpan={header.colSpan}>
                    {header.isPlaceholder
                      ? null
                      : flexRender(
                          header.column.columnDef.header,
                          header.getContext(),
                        )}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody
            className={cn(
              "transition-opacity",
              isLoading && rows.length > 0 && "opacity-60",
            )}
          >
            {rows.length > 0 ? (
              rows.map((row) => (
                <TableRow
                  key={row.id}
                  data-state={row.getIsSelected() && "selected"}
                >
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id}>
                      {flexRender(
                        cell.column.columnDef.cell,
                        cell.getContext(),
                      )}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-24 text-center text-sm text-muted-foreground"
                >
                  {isLoading ? "Loading…" : isEmpty ? emptyMessage : null}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>

      <DataTablePagination
        pagination={pagination}
        pageCount={pageCount}
        totalCount={totalCount}
        rowsOnPage={rows.length}
        pageSizeOptions={pageSizeOptions}
        onPaginationChange={onPaginationChange}
        disabled={isLoading}
      />
    </div>
  );
}

type DataTablePaginationProps = {
  pagination: PaginationState;
  pageCount: number;
  totalCount: number;
  rowsOnPage: number;
  pageSizeOptions: number[];
  onPaginationChange: OnChangeFn<PaginationState>;
  disabled?: boolean;
};

function DataTablePagination({
  pagination,
  pageCount,
  totalCount,
  rowsOnPage,
  pageSizeOptions,
  onPaginationChange,
  disabled,
}: DataTablePaginationProps) {
  const { pageIndex, pageSize } = pagination;
  const currentPage = pageIndex + 1; // 1-based for display
  const isFirst = pageIndex <= 0;
  const isLast = pageIndex >= pageCount - 1;

  const items = getPageItems(currentPage, pageCount);

  // goTo accepts a 1-based page number to match the displayed UI.
  const goTo = (pageOneBased: number) =>
    onPaginationChange({
      pageIndex: Math.max(0, Math.min(pageOneBased - 1, pageCount - 1)),
      pageSize,
    });

  // Three-column grid on >=sm so the pagination nav sits dead-centre of
  // the row regardless of how wide the side groups are. Stacks on mobile.
  return (
    <div className="grid grid-cols-1 items-center gap-3 sm:grid-cols-3">
      <div className="flex justify-start">
        <label className="flex items-center gap-2 text-xs text-muted-foreground">
          <span>Rows</span>
          <select
            className="h-8 rounded-md border border-input bg-background px-2 text-xs focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
            value={pageSize}
            onChange={(e) =>
              onPaginationChange({
                pageIndex: 0,
                pageSize: Number(e.target.value),
              })
            }
            disabled={disabled}
          >
            {pageSizeOptions.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>
      </div>

      <nav
        className="flex flex-wrap items-center justify-center gap-1"
        aria-label="Pagination"
      >
        <Button
          variant="ghost"
          size="sm"
          className="gap-1 px-2"
          onClick={() => goTo(currentPage - 1)}
          disabled={disabled || isFirst}
        >
          <ChevronLeft className="h-4 w-4" /> Previous
        </Button>

        {items.map((item, idx) =>
          item === "ellipsis" ? (
            <span
              key={`e-${idx}`}
              className="flex h-9 w-9 items-center justify-center text-sm text-muted-foreground"
              aria-hidden
            >
              …
            </span>
          ) : (
            <Button
              key={item}
              variant={item === currentPage ? "default" : "ghost"}
              size="icon"
              className={cn(
                "h-9 w-9 rounded-full text-sm",
                item === currentPage && "pointer-events-none",
              )}
              onClick={() => goTo(item)}
              disabled={disabled}
              aria-current={item === currentPage ? "page" : undefined}
              aria-label={`Go to page ${item}`}
            >
              {item}
            </Button>
          ),
        )}

        <Button
          variant="ghost"
          size="sm"
          className="gap-1 px-2"
          onClick={() => goTo(currentPage + 1)}
          disabled={disabled || isLast}
        >
          Next <ChevronRight className="h-4 w-4" />
        </Button>
      </nav>

      <div className="flex justify-end text-sm text-muted-foreground">
        Showing {rowsOnPage.toLocaleString()} of {totalCount.toLocaleString()}{" "}
        results
      </div>
    </div>
  );
}

// Returns the sequence of page buttons to render with smart elision.
// Always shows page 1 and the last page; expands around the current page;
// inserts "ellipsis" markers where there's a gap. Mirrors the screenshot:
//   current=5, total=99   → [1, 2, 3, 4, 5, …, 99]
//   current=50, total=99  → [1, …, 49, 50, 51, …, 99]
//   current=98, total=99  → [1, …, 95, 96, 97, 98, 99]
function getPageItems(
  current: number,
  total: number,
): (number | "ellipsis")[] {
  if (total <= 7) return range(1, total);

  // Near the start — show first 5 pages, then ellipsis + last.
  if (current <= 5) {
    return [...range(1, 5), "ellipsis", total];
  }
  // Near the end — show first + ellipsis, then last 5 pages.
  if (current >= total - 4) {
    return [1, "ellipsis", ...range(total - 4, total)];
  }
  // Middle — first + ellipsis + window of 3 around current + ellipsis + last.
  return [
    1,
    "ellipsis",
    current - 1,
    current,
    current + 1,
    "ellipsis",
    total,
  ];
}

function range(start: number, end: number): number[] {
  return Array.from({ length: end - start + 1 }, (_, i) => start + i);
}

// Re-export pagination defaults so callers can seed initial state without
// re-importing from constants:
//   const [pagination, setPagination] = useState<PaginationState>({
//     pageIndex: 0, pageSize: DEFAULT_PAGE_SIZE,
//   });
export { DEFAULT_PAGE_SIZE };
