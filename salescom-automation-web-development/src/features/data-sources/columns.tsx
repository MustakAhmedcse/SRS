"use client";

import Link from "next/link";
import type { ColumnDef } from "@tanstack/react-table";
import { Button } from "@/components/ui/button";
import { Can } from "@/features/auth/components/Can";
import { PERM } from "@/features/auth/permissions";
import { ROUTES } from "@/lib/constants";
import { cn } from "@/lib/utils";
import type { DataSource } from "./schema";

// Column definitions for the data-sources DataTable. Headers translate
// backend field names into business-friendly labels; cell renderers
// shape values (status pill, edit button, truncation).

export const dataSourceColumns: ColumnDef<DataSource>[] = [
  {
    accessorKey: "aliasTableName",
    header: "Alias name",
    cell: ({ row }) => (
      <span className="font-medium">{row.original.aliasTableName}</span>
    ),
  },
  {
    accessorKey: "sourceTableName",
    header: "Base table",
    cell: ({ row }) => (
      <span className="font-mono text-xs text-muted-foreground">
        {row.original.sourceTableName}
      </span>
    ),
  },
  {
    accessorKey: "tableDescription",
    header: "Description",
    cell: ({ row }) => (
      <span className="line-clamp-2 text-sm text-muted-foreground">
        {row.original.tableDescription ?? "—"}
      </span>
    ),
  },
  {
    accessorKey: "isActive",
    header: "Status",
    cell: ({ row }) => <StatusPill active={row.original.isActive} />,
  },
  {
    id: "actions",
    header: () => <span className="sr-only">Actions</span>,
    cell: ({ row }) => (
      <Can permission={PERM.DATA_SOURCES_EDIT}>
        <Button variant="ghost" size="sm" asChild>
          <Link href={ROUTES.DATA_SOURCES_EDIT(row.original.id)}>Edit</Link>
        </Button>
      </Can>
    ),
  },
];

function StatusPill({ active }: { active: boolean }) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs font-medium",
        active
          ? "border-(--status-success)/30 text-success"
          : "border-border text-muted-foreground",
      )}
    >
      <span
        className={cn(
          "h-1.5 w-1.5 rounded-full",
          active ? "bg-success" : "bg-muted-foreground",
        )}
      />
      {active ? "Active" : "Inactive"}
    </span>
  );
}
