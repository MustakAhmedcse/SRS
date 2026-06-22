# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@AGENTS.md

## Commands

- `npm run dev` — start the dev server (Turbopack) on http://localhost:3000
- `npm run build` — production build
- `npm run start` — serve the production build
- `npm run lint` — ESLint (`eslint-config-next`)
- `npm run typecheck` — `tsc --noEmit`; run this after non-trivial changes
- `npm run format` — Prettier write (Tailwind class sorting plugin enabled)

No test runner is configured — there is no `test` script and no test files. Do not invent a test command.

Stack: Next.js 16.2.6 (App Router), React 19.2.4, TypeScript strict, Tailwind v4, shadcn/ui, TanStack Query v5, react-hook-form + Zod. Import alias: `@/*` → `./src/*`.

## Environment

`BACKEND_API_URL` is required (validated by Zod in `src/lib/env.ts`; server-only). `MOCK_AUTH=1` bypasses the real backend on login — dev only, must stay unset in production. Browser-exposed vars must be prefixed `NEXT_PUBLIC_`. See `.env.example`.

## Architecture

### Route layout
- `app/(auth)/` — unauthenticated routes (login). Own layout.
- `app/(app)/` — authenticated routes. `(app)/layout.tsx` calls `requireSession()` (server-side gate) then wraps children in `<AuthProvider>` + `<AppShell>`.
- `app/api/auth/{login,verify,logout}/route.ts` — Backend-for-Frontend route handlers.
- Route constants live in `src/lib/constants.ts` (`ROUTES`). Don't hardcode paths.

### Two-track session model — read this before touching auth
The session is deliberately split across two stores:

1. **Token** → httpOnly cookie `salescom_token`. The backend's `accessToken` (a JWE — opaque, never decode it). Set server-side by the login/verify route handlers; `expires` matches the backend's `accessTokenExpiresAtUtc`. Never readable by JS. This is the **only** credential the server and backend trust. There is **no refresh token** — the access token is the whole session.
2. **Session display data** → `localStorage["salescom.session"]` (`SessionData`: `fullName`, `userName`, `permissions[]`, `tokenExpireDate`, last-login timestamps). Written client-side after login. The schema (`features/auth/schema.ts`) maps the backend's `rights` → `permissions` and `accessTokenExpiresAtUtc` → `tokenExpireDate` so the rest of the app keeps consuming those names. Drives UI rendering only — **not** a security boundary, freely tamperable.

The backend has **no `/me` endpoint**, so the server cannot resolve identity or permissions. `requireSession()` (`src/lib/auth/session.ts`) only checks cookie *existence*. All permission logic is therefore client-side and advisory; real authorization must be enforced by the backend on every request (it re-checks `rights` from the DB per request).

The backend decides per user whether 2FA is required, so login has **two flows** discriminated by `data.authType`:
- **`Normal`** (external user) — session issued in one round-trip. Flow: `LoginForm` → `useLogin` → `login()` (`features/auth/api.ts`) → `POST /api/auth/login` → route handler validates with Zod, calls `POST {BACKEND}/api/account/login` (or `mockBackendLogin` when `MOCK_AUTH=1`), moves `session.accessToken` into the cookie, returns the display session → `writeSession()` persists it to localStorage.
- **`SSO`** (internal user, 2FA) — login returns a `redirectUrl`; `useLogin` sends the browser to the central OTP page. It redirects back to `ROUTES.LOGIN_CALLBACK` (`/login/callback`) with `?authToken=...`; that page (`useVerifyAuthToken`) calls `POST /api/auth/verify` → `POST {BACKEND}/api/account/verify-auth-token`, which returns the session **directly as `data`** (not nested under `data.session` like login does) — the route handler sets the cookie and returns the display session.

No backend logout/refresh endpoints exist — logout is client-side only (`/api/auth/logout` just clears the cookie). Four independent logout triggers: (a) `AuthProvider` `tokenExpireDate` watcher — open-tab expiry; (b) `AuthProvider` 3-hour inactivity watcher — idle logout; (c) cookie `expires` — closed-tab; (d) global 401 handler in `lib/query/client.ts` — token revoked / `User.Unauthorized` mid-session. Note 403 (`User.Forbidden`) does **not** log out — it means lack of permission, handled inline.

### Permissions
Permission codes live only in `PERM` (`features/auth/permissions.ts`) — never write raw integer codes in JSX or hooks. Gate UI with `<Can>` / `<CanAny>` / `<CanAll>` (`features/auth/components/Can.tsx`) or `useCan` / `usePermissions`. Gate whole pages with `<RequirePermission>`. All of these read the localStorage session.

### Data fetching — two fetch wrappers, don't mix them
- `serverFetch` (`lib/api/server.ts`) — server components / route handlers → backend **directly**; attaches `Authorization: Bearer <cookie token>`.
- `clientFetch` (`lib/api/client.ts`) — browser → **same-origin `/api/*` only** (never the backend directly).
- Both throw `ApiError` (`lib/api/errors.ts`) on non-2xx HTTP responses; use `isApiError` to narrow. `ApiError` carries `status`, `errorCode`, and `fieldErrors`.
- TanStack Query (`makeQueryClient`): a 401 from any query/mutation forces a hard nav to `/login`; retries skip 401/403/404.
- A catch-all proxy at `app/api/[...path]/route.ts` forwards every `/api/*` request (that doesn't have a more specific route handler) to `${BACKEND_API_URL}/<path>`, attaching the cookie token as a bearer. New endpoints work out of the box — you do NOT need a per-endpoint route handler unless the endpoint needs special server-side logic (like the auth routes do for token stripping).

### Tables — DataTable + server-side pagination
The reusable `<DataTable />` (`src/components/ui/data-table.tsx`) is built on `@tanstack/react-table` with `manualPagination: true` — server-side pagination is the default and only mode. Callers own pagination state so it can be keyed into TanStack Query (and later URL-synced).

- Backend paginated endpoint contract: response body is `ApiEnvelope<PaginatedData<Row>>` — i.e. `data: { items: Row[], page: number, pageSize: number, totalCount: number }`. Helpers in `src/lib/api/pagination.ts`: `PaginatedData<T>`, `paginatedDataSchema(rowSchema)`, `PageParams`. The backend uses 1-based `page`; per-feature `api.ts` converts from TanStack Table's 0-based `pageIndex` (`page: pageIndex + 1`).
- Caller pattern:
  ```tsx
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0, pageSize: DEFAULT_PAGE_SIZE,
  });
  const query = useQuery({
    queryKey: ["reports", pagination],
    queryFn: () => getReports(pagination),
    placeholderData: keepPreviousData, // smooth page transitions
  });
  return <DataTable columns={cols} data={query.data?.items ?? []}
    totalCount={query.data?.totalCount ?? 0}
    pagination={pagination} onPaginationChange={setPagination}
    isLoading={query.isFetching} />;
  ```
- Sorting / filtering are intentionally NOT in the v1 API surface — when needed, add them as controlled props (mirroring the pagination shape) and set `manualSorting` / `manualFiltering`. Don't enable client-side sort/filter — it would only sort the visible page and mislead users.

### Response envelope — every backend response is wrapped
Every backend response (real or mock) is an `ApiEnvelope<T>` (`lib/api/envelope.ts`):

```ts
{ success: boolean; message: string; errorCode?: string | null; data?: T | null }
```

- `clientFetch<T>` / `serverFetch<T>` return `ApiEnvelope<T>` on 2xx and **throw `ApiError` on non-2xx** — keeps the global 401 handler and TanStack retry logic working.
- Each `features/<feature>/api.ts` defines a typed envelope via `envelopeSchema(DataSchema)`, validates the response, throws `ApiError` when `success: false`, and returns the unwrapped `data` to callers. Hooks / forms then only deal with `data` or an `ApiError`.
- Route handlers under `app/api/*` MUST also return the envelope shape (including error paths) so the contract holds end-to-end.

### Client storage primitive
`useLocalStorage` (`src/hooks/useLocalStorage.ts`) is a `useSyncExternalStore` wrapper with a custom in-tab pub/sub — the browser `storage` event only fires in *other* tabs, so the writing tab needs this to stay reactive. Values are Zod-validated on read. Use the sync helpers (`readSession`/`writeSession`/`clearSession`) from event handlers and mutations; use the `useSession` hook inside components.

### Conventions
- Code is organized by feature under `src/features/` (currently `auth`); shared infra under `src/lib`, UI under `src/components`.
- Zod schemas mirror backend DTOs in camelCase — keep field names aligned with the backend, do not rename for frontend taste (see `features/auth/schema.ts`).
- shadcn/ui is configured (`components.json`, neutral base, components in `@/components/ui`).
