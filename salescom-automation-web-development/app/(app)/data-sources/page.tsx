"use client";

import { useState } from "react";
import Link from "next/link";
import type { PaginationState } from "@tanstack/react-table";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { DataTable } from "@/components/ui/data-table";
import { Can } from "@/features/auth/components/Can";
import { RequirePermission } from "@/features/auth/components/RequirePermission";
import { PERM } from "@/features/auth/permissions";
import { dataSourceColumns } from "@/features/data-sources/columns";
import { useDataSources } from "@/features/data-sources/hooks";
import { DEFAULT_PAGE_SIZE, ROUTES } from "@/lib/constants";

export default function DataSourcesPage() {
  return (
    <RequirePermission permission={PERM.DATA_SOURCES_VIEW}>
      <DataSourcesContent />
    </RequirePermission>
  );
}

function DataSourcesContent() {
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: DEFAULT_PAGE_SIZE,
  });

  const query = useDataSources(pagination);

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">Data sources</h1>
        <Can permission={PERM.DATA_SOURCES_CREATE}>
          <Button asChild>
            <Link href={ROUTES.DATA_SOURCES_NEW}>
              <Plus className="h-4 w-4" />
              Add data source
            </Link>
          </Button>
        </Can>
      </div>

      <DataTable
        columns={dataSourceColumns}
        data={query.data?.items ?? []}
        totalCount={query.data?.totalCount ?? 0}
        pagination={pagination}
        onPaginationChange={setPagination}
        isLoading={query.isFetching}
        getRowId={(row) => row.id}
        emptyMessage="No data sources registered yet."
      />
    </div>
  );
}
