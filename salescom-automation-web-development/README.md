# SalesCom Commission Automation — Frontend

Web app for configuring and running sales commission programmes for the Trade
Marketing team. This is the frontend layer only; all business data lives in a
separate backend service that this app communicates with.

## Tech stack

- **Next.js 16** (App Router) + **React 19**
- **TypeScript** (strict mode)
- **Tailwind CSS v4** + **shadcn/ui** components
- **TanStack Query v5** for server state
- **react-hook-form** + **Zod** for forms and validation

## Getting started

Prerequisites: **Node.js 20+** and npm.

```bash
npm install
cp .env.example .env.local   # then edit the values
npm run dev
```

The app runs at http://localhost:3000.

> Tip: set `MOCK_AUTH=1` in `.env.local` to develop without a running backend —
> any username/password will log you in.

### Environment variables

| Variable          | Required | Description                                                              |
| ----------------- | -------- | ------------------------------------------------------------------------ |
| `BACKEND_API_URL` | Yes      | Base URL of the backend API. Server-only, never exposed to the browser.  |
| `MOCK_AUTH`       | No       | Set to `1` to accept any login without calling the backend. **Dev only.** |

Any browser-readable variable must be prefixed `NEXT_PUBLIC_`. See `.env.example`.

## Scripts

| Command             | Purpose                            |
| ------------------- | ---------------------------------- |
| `npm run dev`       | Start the dev server (Turbopack)   |
| `npm run build`     | Production build                   |
| `npm run start`     | Serve the production build         |
| `npm run lint`      | Lint with ESLint                   |
| `npm run typecheck` | Type-check with `tsc --noEmit`     |
| `npm run format`    | Format with Prettier               |

There is no automated test suite configured yet.

## Project structure

```
app/                Routes (App Router)
  (auth)/           Public routes — login
  (app)/            Authenticated routes — wrapped by the app shell
  api/auth/         Login/logout route handlers (Backend-for-Frontend)
src/
  features/         Domain slices (e.g. auth) — schema, api, hooks, components
  lib/
    api/            serverFetch / clientFetch wrappers, shared URL + error types
    auth/           Cookie and server-session helpers
    query/          TanStack Query client and provider
  components/       Shared UI — ui/ (shadcn), layout/, feedback/
  hooks/            Shared hooks (e.g. useLocalStorage)
  providers/        App-wide context providers
```

New domain areas go under `src/features/<name>/`, using the `auth` slice as a
template.

## Architecture

### Authentication — two-track session

The session is split deliberately across two stores:

- **Token** — kept in an httpOnly cookie (`salescom_token`), never readable by
  JavaScript. This is the only credential the server and backend trust.
- **Display data** (user, permissions, expiry) — kept in `localStorage`. It
  drives UI rendering only and is **not** a security boundary.

The backend has no `/auth/me` endpoint, so the server can only verify that the
cookie *exists*. All permission checks are client-side and advisory — real
authorization must be enforced by the backend on every request.

A user is logged out by any of: the client-side expiry watcher (`AuthProvider`),
the cookie expiring, or a `401` response from any API call.

### Data fetching

- `serverFetch` — used in server components and route handlers; calls the
  backend directly with the bearer token from the cookie.
- `clientFetch` — used in the browser; only ever calls this app's own `/api/*`
  routes, never the backend directly.

### Permissions

Permission codes are centralised in `src/features/auth/permissions.ts` (`PERM`).
Gate UI with `<Can>` / `<RequirePermission>` or the `useCan` hook — never write
raw numeric codes in components.

## Deployment

The app is stateless (cookie-based auth, no server-side session store), so it
scales horizontally behind a load balancer with no sticky sessions required.
Deploy the **same build artifact** to every instance to avoid chunk-loading
errors caused by build skew.

---

For a deeper architecture reference aimed at contributors and AI tooling, see
[`CLAUDE.md`](./CLAUDE.md).
