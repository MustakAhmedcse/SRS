export type FieldError = { field: string; message: string };

// Thrown by clientFetch / serverFetch on non-2xx HTTP responses, and by
// feature api.ts files when the backend envelope returns success:false.
// `status` is 0 when the failure is logical-only (no HTTP status).
export class ApiError extends Error {
  status: number;
  errorCode: string | null;
  fieldErrors?: FieldError[];

  constructor(opts: {
    status: number;
    message: string;
    errorCode?: string | null;
    fieldErrors?: FieldError[];
  }) {
    super(opts.message);
    this.name = "ApiError";
    this.status = opts.status;
    this.errorCode = opts.errorCode ?? null;
    this.fieldErrors = opts.fieldErrors;
  }
}

export function isApiError(e: unknown): e is ApiError {
  return e instanceof ApiError;
}

// Shape the backend may send on errors. Same envelope as the success
// case, plus optional fieldErrors for validation failures.
type ErrorBody = {
  success?: boolean;
  message?: string;
  errorCode?: string | null;
  fieldErrors?: FieldError[];
};

export async function normalizeErrorResponse(res: Response): Promise<ApiError> {
  let body: ErrorBody | null = null;
  try {
    body = (await res.json()) as ErrorBody;
  } catch {
    // body wasn't json — leave null
  }
  return new ApiError({
    status: res.status,
    message: body?.message ?? `Request failed (${res.status})`,
    errorCode: body?.errorCode,
    fieldErrors: body?.fieldErrors,
  });
}
