"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { useForm, useWatch } from "react-hook-form";
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
import { Select } from "@/components/ui/select";
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
import {
  useAvailableColumns,
  useAvailableTables,
  useCreateDataSource,
} from "../hooks";
import {
  CreateDataSourceFormSchema,
  type CreateDataSourceForm,
  type CreateDataSourceRequest,
} from "../schema";

const EMPTY_TABLE = "";

export function NewDataSourceForm() {
  const router = useRouter();
  const tables = useAvailableTables();
  const create = useCreateDataSource();

  const form = useForm<CreateDataSourceForm>({
    resolver: zodResolver(CreateDataSourceFormSchema),
    defaultValues: {
      aliasTableName: "",
      tableDescription: "",
      sourceTableName: EMPTY_TABLE,
      isActive: true,
      aliases: {},
    },
  });

  // useWatch (instead of form.watch) subscribes via context, which the
  // React Compiler can analyze — form.watch returns a non-memoizable
  // function and trips react-hooks/incompatible-library.
  const selectedTable = useWatch({
    control: form.control,
    name: "sourceTableName",
  });
  const columnsQuery = useAvailableColumns(
    selectedTable === EMPTY_TABLE ? undefined : selectedTable,
  );

  const onSubmit = form.handleSubmit((values) => {
    // Build the wire payload from the live query, not from form state.
    // This means a window-focus refetch can never wipe the user's
    // aliases — only columns the server currently returns are sent,
    // each looked up by name in the alias map (empty alias → fall back
    // to the real column name, per spec).
    const columns = columnsQuery.data ?? [];
    if (columns.length === 0) {
      toast.error("Select a table with at least one column.");
      return;
    }

    const payload: CreateDataSourceRequest = {
      sourceTableName: values.sourceTableName,
      aliasTableName: values.aliasTableName.trim(),
      tableDescription: values.tableDescription.trim(),
      isActive: values.isActive,
      columns: columns.map((c) => ({
        columnName: c.columnName,
        dataType: c.dataType,
        aliasColumnName:
          values.aliases[c.columnName]?.trim() || c.columnName,
      })),
    };

    create.mutate(payload, {
      onSuccess: () => {
        toast.success("Data source created");
        router.push(ROUTES.DATA_SOURCES);
      },
    });
  });

  const submitting = create.isPending;
  const tablesLoading = tables.isLoading;
  const tablesError = tables.error;
  const columns = columnsQuery.data ?? [];

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
                  <Input
                    placeholder="e.g. Agent daily sales"
                    autoComplete="off"
                    {...field}
                  />
                </FormControl>
                <FormDescription>
                  Shown in programmes, reports, and the data source list.
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="sourceTableName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Source table</FormLabel>
                <FormControl>
                  <Select disabled={tablesLoading || submitting} {...field}>
                    <option value={EMPTY_TABLE} disabled>
                      {tablesLoading
                        ? "Loading tables…"
                        : tablesError
                          ? "Failed to load tables"
                          : "Select a table"}
                    </option>
                    {tables.data?.map((t) => (
                      <option key={t.tableName} value={t.tableName}>
                        {t.tableName}
                      </option>
                    ))}
                  </Select>
                </FormControl>
                <FormDescription>
                  Backend tables available for commission programmes.
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

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

          {selectedTable === EMPTY_TABLE ? (
            <EmptyHint message="Select a table to see its columns." />
          ) : columnsQuery.isLoading ? (
            <EmptyHint message="Loading columns…" />
          ) : columnsQuery.error ? (
            <EmptyHint message="Failed to load columns for this table." />
          ) : columns.length === 0 ? (
            <EmptyHint message="This table has no columns." />
          ) : (
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
                  {columns.map((col) => (
                    <TableRow key={col.columnName}>
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
                          {...form.register(`aliases.${col.columnName}`)}
                        />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </section>

        <div className="flex items-center justify-end gap-2 border-t border-border pt-4">
          <Button variant="outline" asChild>
            <Link href={ROUTES.DATA_SOURCES}>Cancel</Link>
          </Button>
          <Button
            type="submit"
            disabled={submitting || columnsQuery.isLoading}
          >
            {submitting ? "Saving…" : "Save data source"}
          </Button>
        </div>
      </form>
    </Form>
  );
}

function EmptyHint({ message }: { message: string }) {
  return (
    <div className="rounded-md border border-dashed border-border bg-muted/40 px-4 py-6 text-center text-sm text-muted-foreground">
      {message}
    </div>
  );
}
