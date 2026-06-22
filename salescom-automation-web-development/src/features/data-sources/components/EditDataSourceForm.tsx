"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { ROUTES } from "@/lib/constants";
import { useUpdateDataSource } from "../hooks";
import {
  UpdateDataSourceFormSchema,
  type DataSourceDetail,
  type UpdateDataSourceForm,
  type UpdateDataSourceRequest,
} from "../schema";

// Mounted by EditDataSourceLoader only after the entity has loaded, so
// `initial` is always populated and useForm({ defaultValues }) snapshots
// the form *once*. A later refetch on window focus updates the cache
// but does not touch the form — pending edits survive.
export function EditDataSourceForm({
  initial,
}: {
  initial: DataSourceDetail;
}) {
  const router = useRouter();
  const update = useUpdateDataSource(initial.id);

  const form = useForm<UpdateDataSourceForm>({
    resolver: zodResolver(UpdateDataSourceFormSchema),
    defaultValues: {
      aliasTableName: initial.aliasTableName,
      tableDescription: initial.tableDescription ?? "",
      isActive: initial.isActive,
      // Key by server column id (stable for the data source's lifetime)
      // so the alias map can't be confused by a column rename later.
      aliases: Object.fromEntries(
        initial.columns.map((c) => [c.id, c.aliasColumnName]),
      ),
    },
  });

  const onSubmit = form.handleSubmit((values) => {
    const payload: UpdateDataSourceRequest = {
      aliasTableName: values.aliasTableName.trim(),
      tableDescription: values.tableDescription.trim(),
      isActive: values.isActive,
      columns: initial.columns.map((c) => ({
        id: c.id,
        columnName: c.columnName,
        dataType: c.dataType,
        aliasColumnName:
          values.aliases[c.id]?.trim() || c.columnName,
      })),
    };

    update.mutate(payload, {
      onSuccess: () => {
        toast.success("Data source updated");
        router.push(ROUTES.DATA_SOURCES);
      },
    });
  });

  const submitting = update.isPending;

  return (
    <Form {...form}>
      <form onSubmit={onSubmit} className="space-y-8">
        <section className="grid grid-cols-1 gap-6 md:grid-cols-2">
          <FormField
            control={form.control}
            name="aliasTableName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Alias name</FormLabel>
                <FormControl>
                  <Input autoComplete="off" {...field} />
                </FormControl>
                <FormDescription>
                  Shown in programmes, reports, and the data source list.
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormItem>
            <FormLabel>Source table</FormLabel>
            <FormControl>
              <Input
                value={initial.sourceTableName}
                readOnly
                disabled
                className="font-mono text-xs"
              />
            </FormControl>
            <FormDescription>
              The backend table cannot be changed once a data source is
              created.
            </FormDescription>
          </FormItem>

          <FormField
            control={form.control}
            name="tableDescription"
            render={({ field }) => (
              <FormItem className="md:col-span-2">
                <FormLabel>Description</FormLabel>
                <FormControl>
                  <Textarea
                    placeholder="What is this data source for? (optional)"
                    rows={3}
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="isActive"
            render={({ field }) => (
              <FormItem className="flex items-center justify-between rounded-md border border-border p-4 md:col-span-2">
                <div className="space-y-0.5">
                  <FormLabel>Status</FormLabel>
                  <FormDescription>
                    {field.value
                      ? "Active — selectable in programmes and reports."
                      : "Inactive — hidden from programmes and reports."}
                  </FormDescription>
                </div>
                <FormControl>
                  <Switch
                    checked={field.value}
                    onCheckedChange={field.onChange}
                    aria-label="Active"
                  />
                </FormControl>
              </FormItem>
            )}
          />
        </section>

        <section className="space-y-3">
          <div>
            <h2 className="text-base font-semibold">Columns</h2>
            <p className="text-sm text-muted-foreground">
              Leave alias blank to use the real column name.
            </p>
          </div>

          <div className="rounded-md border border-border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[35%]">Real column</TableHead>
                  <TableHead className="w-[20%]">Type</TableHead>
                  <TableHead>Alias</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {initial.columns.map((col) => (
                  <TableRow key={col.id}>
                    <TableCell className="font-mono text-xs">
                      {col.columnName}
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground">
                      {col.dataType}
                    </TableCell>
                    <TableCell>
                      <Input
                        placeholder={col.columnName}
                        autoComplete="off"
                        {...form.register(`aliases.${col.id}`)}
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </section>

        <div className="flex items-center justify-end gap-2 border-t border-border pt-4">
          <Button variant="outline" asChild>
            <Link href={ROUTES.DATA_SOURCES}>Cancel</Link>
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? "Saving…" : "Save changes"}
          </Button>
        </div>
      </form>
    </Form>
  );
}
