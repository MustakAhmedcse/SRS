// Builds an absolute request URL and appends any defined query params.
// Shared by the server- and client-side fetch wrappers, which are identical
// here except for their base origin (the backend vs. this app), so it's
// passed in. `null`/`undefined` params are skipped.

export type Query = Record<
  string,
  string | number | boolean | undefined | null
>;

export function buildUrl(path: string, base: string, query?: Query): string {
  const url = new URL(path.startsWith("/") ? path : `/${path}`, base);
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null) {
        url.searchParams.set(key, String(value));
      }
    }
  }
  return url.toString();
}
