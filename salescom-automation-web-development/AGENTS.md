# AGENTS.md

Engineering conventions for `salescom_automation_frontend`. Imported by
`CLAUDE.md`. See `CLAUDE.md` for stack, routing, and the auth/session model.

## State management

State is split by ownership — pick the layer by what owns the data, never
duplicate the same data across layers.

| Layer | Owns | Tool |
| --- | --- | --- |
| Server state | Data fetched from the backend | TanStack Query |
| Client / global state | In-progress wizard JSON IR, pipeline builder draft, wizard navigation | **Zustand + immer** |
| Form state | A form's live field values while the user edits | **React Hook Form + Zod** |

- Do **not** copy server data from TanStack Query into a Zustand store.
- Do **not** mirror every keystroke from a form into a Zustand store.

### Zustand stores — per-request (Next.js App Router)

Following the official guide (https://zustand.docs.pmnd.rs/guides/nextjs), a
Zustand store is **never** a module-level singleton. The Next.js server handles
many requests at once, so a shared store would leak one user's state into
another request. Server Components must not read or write a store either.

Every store is therefore **created per request** and passed down through React
Context. A feature store is three pieces under `src/features/<feature>/store/`:

1. **Vanilla store factory** — `<feature>-store.ts`. A `create<Feature>Store()`
   function that returns a fresh vanilla store, built with `createStore` from
   `@/lib/store/create-store` (which applies immer + devtools). It may take an
   argument to seed the store with server-provided data.
2. **Context provider + hook** — `<feature>-store-provider.tsx` (`"use client"`).
   `createStoreContext` from `@/lib/store/create-store-context` turns the
   factory into a `Provider` and a selector hook.
3. **Consumption** — client components read state through the selector hook.

Mount the `Provider` at the narrowest layout/segment that needs the store (e.g.
the wizard route-group layout), not the root layout.

```tsx
// src/features/wizard/store/wizard-store.ts
import { createStore } from "@/lib/store/create-store";

export type WizardStore = WizardState & WizardActions;

export const createWizardStore = (init?: Partial<WizardState>) =>
  createStore<WizardStore>("wizard", (set) => ({
    ...defaultWizardState,
    ...init,
    nextStep: () => set((s) => { s.step += 1; }),
  }));

// src/features/wizard/store/wizard-store-provider.tsx
"use client";
import { createStoreContext } from "@/lib/store/create-store-context";
import { createWizardStore, type WizardStore } from "./wizard-store";

export const { Provider: WizardStoreProvider, useStore: useWizardStore } =
  createStoreContext<WizardStore>("Wizard", createWizardStore);
```

- immer: write updates as recipes — `set((s) => { s.draft.name = value; })`.
- Always read with a selector — `useWizardStore((s) => s.step)`.
- Persistence is opt-in: add `persist` inside a specific factory when that store
  must survive a refresh, and guard SSR hydration (render nothing until
  hydrated — same idea as `RequirePermission`).
- Split a large store into slice files combined inside its factory.

### Forms

- Build forms with React Hook Form + `zodResolver`; define the Zod schema in the
  feature's `schema.ts` and derive the type with `z.infer`.
- Use the `Form` primitives in `@/components/ui/form` (`Form`, `FormField`,
  `FormItem`, `FormLabel`, `FormControl`, `FormDescription`, `FormMessage`) for
  multi-field forms — they wire RHF context to accessible markup and render Zod
  errors automatically.

### RHF ↔ Zustand boundary (wizard)

RHF owns the **ephemeral per-step editing buffer**; the Zustand store owns the
**accumulated draft IR**.

- On step "Next": validate with Zod, then commit the values into the store.
- On navigating back: re-seed the step form via RHF `defaultValues` read from
  the store.
- Do not write to the store on every change — only on step commit.

## Feature folder layout

Code is organised by feature under `src/features/<feature>/`:

```
src/features/<feature>/
├── schema.ts      # Zod schemas (IR + per-step / per-form inputs)
├── api.ts         # backend calls (via clientFetch)
├── hooks.ts       # TanStack Query hooks + feature hooks
├── store/         # per-request Zustand store
│   ├── <feature>-store.ts           # vanilla factory (createStore)
│   └── <feature>-store-provider.tsx # createStoreContext: Provider + hook
└── components/
```

Shared infra lives in `src/lib`, shared UI in `src/components`.
