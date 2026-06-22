export const ROUTES = {
  LOGIN: "/login",
  // Where the central OTP service redirects back to with `?authToken=...`.
  LOGIN_CALLBACK: "/login/callback",
  DASHBOARD: "/",
  DATA_SOURCES: "/data-sources",
  DATA_SOURCES_NEW: "/data-sources/new",
  DATA_SOURCES_EDIT: (id: string) =>
    `/data-sources/${encodeURIComponent(id)}/edit` as const,
  NOT_AUTHORIZED: "/403",
} as const;

export const DEFAULT_PAGE_SIZE = 25;
