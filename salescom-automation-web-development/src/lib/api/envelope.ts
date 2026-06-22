import { z, type ZodTypeAny } from "zod";
import { ApiError } from "./errors";

// The standard wrapper every backend response carries. `success` and
// `message` are always present; `errorCode` is non-null on failures;
// `data` carries the operation payload and is null for operations with
// no return value (e.g. logout, delete).
//
// On 2xx HTTP responses, clientFetch / serverFetch return this envelope
// untouched — callers MUST check `success` before using `data`. Non-2xx
// responses are translated to a thrown ApiError instead, so the global
// 401 handler in lib/query/client.ts and TanStack Query's retry logic
// continue to see HTTP failures as errors.

export type ApiEnvelope<T = unknown> = {
  success: boolean;
  message: string;
  errorCode?: string | null;
  data?: T | null;
};

// Untyped envelope schema — useful when you only care about the
// success/message/errorCode fields and will validate `data` separately.
export const ApiEnvelopeSchema = z.object({
  success: z.boolean(),
  message: z.string(),
  errorCode: z.string().nullable().optional(),
  data: z.unknown().nullable().optional(),
});

// Typed envelope schema builder. Pass the Zod schema for the `data`
// payload and get back a schema for the full envelope.
//
//   const LoginEnvelope = envelopeSchema(LoginDataSchema);
//   const parsed = LoginEnvelope.parse(json);
export function envelopeSchema<T extends ZodTypeAny>(dataSchema: T) {
  return z.object({
    success: z.boolean(),
    message: z.string(),
    errorCode: z.string().nullable().optional(),
    data: dataSchema.nullable().optional(),
  });
}

// Takes an already-parsed envelope and returns `{ data, message }` so
// callers can both consume the payload and surface the backend's message
// (e.g. for success toasts on mutations). Throws ApiError when the
// envelope reports failure or carries no data — TanStack Query and
// catch-blocks then handle logical failures the same as HTTP ones.
//
// Use this in every features/<feature>/api.ts function. The pattern:
//
//   const envelope = await clientFetch<unknown>(...);
//   const parsed = MyEnvelopeSchema.parse(envelope);
//   return unwrapEnvelope(parsed);
export function unwrapEnvelope<T>(envelope: {
  success: boolean;
  message: string;
  errorCode?: string | null;
  data?: T | null;
}): { data: T; message: string } {
  if (!envelope.success || envelope.data == null) {
    throw new ApiError({
      status: 0,
      message: envelope.message,
      errorCode: envelope.errorCode ?? null,
    });
  }
  return { data: envelope.data, message: envelope.message };
}
