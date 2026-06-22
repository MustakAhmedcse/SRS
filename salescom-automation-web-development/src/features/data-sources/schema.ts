import { z } from "zod";
import { envelopeSchema } from "@/lib/api/envelope";
import { paginatedDataSchema } from "@/lib/api/pagination";

// Mirrors the backend DataSource DTO exactly (camelCase, original names).
// Do not rename for frontend taste — column headers translate the field
// names to friendlier labels in columns.tsx.
export const DataSourceSchema = z.object({
  id: z.string().uuid(),
  sourceTableName: z.string(),
  aliasTableName: z.string(),
  tableDescription: z.string().nullable(),
  isActive: z.boolean(),
  createdOnUtc: z.string(),
});

export const DataSourcesPageEnvelope = envelopeSchema(
  paginatedDataSchema(DataSourceSchema),
);

export type DataSource = z.infer<typeof DataSourceSchema>;

// Available tables — returned by GET /sales-com-data-sources/available.
export const AvailableTableSchema = z.object({
  tableName: z.string(),
});
export const AvailableTablesEnvelope = envelopeSchema(
  z.array(AvailableTableSchema),
);
export type AvailableTable = z.infer<typeof AvailableTableSchema>;

// Available columns for a chosen table — returned by
// GET /sales-com-data-sources/available/{tableName}/columns.
export const AvailableColumnSchema = z.object({
  columnName: z.string(),
  dataType: z.string(),
});
export const AvailableColumnsEnvelope = envelopeSchema(
  z.array(AvailableColumnSchema),
);
export type AvailableColumn = z.infer<typeof AvailableColumnSchema>;

// What the form holds while the user edits. Only fields the user can
// change live here — the column list itself stays in TanStack Query
// (server data, never mirrored). `aliases` is keyed by the real column
// name; missing/blank entries fall back to the real name at submit time.
//
// This shape survives a refetch on window focus: new column data is
// rendered straight from the query, and any alias whose column still
// exists is preserved by name lookup. Aliases for columns that
// disappear simply don't make it into the submitted payload.
export const CreateDataSourceFormSchema = z.object({
  aliasTableName: z
    .string()
    .trim()
    .min(1, "Alias name is required")
    .max(120, "Alias name is too long"),
  tableDescription: z.string().trim().max(500, "Description is too long"),
  sourceTableName: z.string().min(1, "Select a table"),
  isActive: z.boolean(),
  aliases: z.record(z.string(), z.string()),
});
export type CreateDataSourceForm = z.infer<typeof CreateDataSourceFormSchema>;

// Wire-level shape POSTed to /api/data-sources. Empty description is
// sent as an empty string to match the backend's non-nullable contract.
export const CreateDataSourceRequestSchema = z.object({
  sourceTableName: z.string().min(1),
  aliasTableName: z.string().min(1),
  tableDescription: z.string(),
  isActive: z.boolean(),
  columns: z
    .array(
      z.object({
        columnName: z.string().min(1),
        aliasColumnName: z.string().min(1),
        dataType: z.string().min(1),
      }),
    )
    .min(1),
});
export type CreateDataSourceRequest = z.infer<
  typeof CreateDataSourceRequestSchema
>;

// Per-column entry on the create response. The backend stamps each
// column with its own id.
export const DataSourceColumnSchema = z.object({
  id: z.string().uuid(),
  columnName: z.string(),
  aliasColumnName: z.string(),
  dataType: z.string(),
});
export type DataSourceColumn = z.infer<typeof DataSourceColumnSchema>;

// Full detail shape returned by the create / get / update endpoints —
// adds `columns` and `updatedAtUtc` on top of the list row. Kept
// separate from DataSourceSchema so the list endpoint isn't forced to
// populate them. `updatedAtUtc` is null on a freshly-created record
// that hasn't been edited yet.
export const DataSourceDetailSchema = DataSourceSchema.extend({
  columns: z.array(DataSourceColumnSchema),
  updatedAtUtc: z.string().nullable(),
});
export type DataSourceDetail = z.infer<typeof DataSourceDetailSchema>;

export const CreateDataSourceEnvelope = envelopeSchema(DataSourceDetailSchema);

// GET /api/data-sources/{id} returns the same detail shape as create.
export const GetDataSourceEnvelope = envelopeSchema(DataSourceDetailSchema);

// PUT /api/data-sources/{id} — same shape as create *without* the
// immutable sourceTableName, and each column carries its server id so
// the backend can match rows to existing records.
export const UpdateDataSourceRequestSchema = z.object({
  aliasTableName: z.string().min(1),
  tableDescription: z.string(),
  isActive: z.boolean(),
  columns: z
    .array(
      z.object({
        id: z.string().uuid(),
        columnName: z.string().min(1),
        aliasColumnName: z.string().min(1),
        dataType: z.string().min(1),
      }),
    )
    .min(1),
});
export type UpdateDataSourceRequest = z.infer<
  typeof UpdateDataSourceRequestSchema
>;

export const UpdateDataSourceEnvelope = envelopeSchema(DataSourceDetailSchema);

// What the edit form holds while the user edits. `aliases` is keyed by
// the column's server id (stable for the lifetime of the data source),
// mirroring the create form's "don't mirror server data into form state"
// principle so a refetch on window focus can never wipe the user's
// pending edits.
export const UpdateDataSourceFormSchema = z.object({
  aliasTableName: z
    .string()
    .trim()
    .min(1, "Alias name is required")
    .max(120, "Alias name is too long"),
  tableDescription: z.string().trim().max(500, "Description is too long"),
  isActive: z.boolean(),
  aliases: z.record(z.string(), z.string()),
});
export type UpdateDataSourceForm = z.infer<typeof UpdateDataSourceFormSchema>;
