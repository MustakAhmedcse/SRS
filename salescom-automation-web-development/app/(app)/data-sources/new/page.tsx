import { RequirePermission } from "@/features/auth/components/RequirePermission";
import { PERM } from "@/features/auth/permissions";
import { NewDataSourceForm } from "@/features/data-sources/components/NewDataSourceForm";

export default function NewDataSourcePage() {
  return (
    <RequirePermission permission={PERM.DATA_SOURCES_CREATE}>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold">Add data source</h1>
        </div>
        <NewDataSourceForm />
      </div>
    </RequirePermission>
  );
}
