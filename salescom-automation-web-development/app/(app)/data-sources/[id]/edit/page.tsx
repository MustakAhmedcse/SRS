import { RequirePermission } from "@/features/auth/components/RequirePermission";
import { PERM } from "@/features/auth/permissions";
import { EditDataSourceLoader } from "@/features/data-sources/components/EditDataSourceLoader";

export default async function EditDataSourcePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return (
    <RequirePermission permission={PERM.DATA_SOURCES_EDIT}>
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold">Edit data source</h1>
        </div>
        <EditDataSourceLoader id={id} />
      </div>
    </RequirePermission>
  );
}
