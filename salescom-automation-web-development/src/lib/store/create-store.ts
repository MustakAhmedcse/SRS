import {
  createStore as createVanillaStore,
  type StateCreator,
  type StoreApi,
} from "zustand/vanilla";
import { devtools } from "zustand/middleware";
import { immer } from "zustand/middleware/immer";

/**
 * Creates a *vanilla* Zustand store (a `StoreApi`, not a React hook) with the
 * project's standard middleware:
 *
 * - `immer`    — write nested updates as if mutating (`set((s) => { s.a.b = 1 })`).
 * - `devtools` — inspect state via the Redux DevTools extension (dev only).
 *
 * Per the Next.js App Router guide (https://zustand.docs.pmnd.rs/guides/nextjs),
 * a store must NOT be a module-level singleton — on the server that store would
 * be shared across every request. Always call this inside a per-request factory
 * (e.g. `createWizardStore()`); a `createStoreContext` Provider then
 * instantiates that factory once per render tree.
 *
 * `persist` is opt-in — add it to an individual factory when that store must
 * survive a page refresh.
 *
 * @example
 * // src/features/wizard/store/wizard-store.ts
 * export type WizardStore = WizardState & WizardActions;
 *
 * export const createWizardStore = (init?: Partial<WizardState>) =>
 *   createStore<WizardStore>("wizard", (set) => ({
 *     ...defaultWizardState,
 *     ...init,
 *     nextStep: () => set((s) => { s.step += 1; }),
 *   }));
 */
export function createStore<T>(
  name: string,
  initializer: StateCreator<
    T,
    [["zustand/devtools", never], ["zustand/immer", never]],
    [],
    T
  >,
): StoreApi<T> {
  // The middleware stack widens `.setState` (immer recipes, devtools action
  // names). Consumers never call `.setState` directly — they use the `set`
  // passed to the initializer, which keeps its immer typing via the generics
  // above — so we expose the plain `StoreApi<T>` for simple, stable consumer
  // types (and to sidestep middleware-mutator variance in `createStoreContext`).
  return createVanillaStore<T>()(
    devtools(immer(initializer), {
      name,
      enabled: process.env.NODE_ENV !== "production",
    }),
  ) as StoreApi<T>;
}
