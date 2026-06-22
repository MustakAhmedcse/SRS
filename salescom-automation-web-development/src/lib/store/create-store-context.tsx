"use client";

import { createContext, useContext, useState, type ReactNode } from "react";
import { useStore, type StoreApi } from "zustand";

/**
 * Builds the per-request React Context plumbing for a Zustand store, following
 * the official Next.js App Router guide:
 * https://zustand.docs.pmnd.rs/guides/nextjs
 *
 * The store is created once per `Provider` instance (held in `useState`), never
 * as a module-level singleton — so the server never shares one user's store
 * with another request. Server Components must not read or write the store.
 *
 * @param name              Label used in the hook's error message and as the
 *                          Provider's display name.
 * @param defaultCreateStore Per-request vanilla store factory (e.g.
 *                          `createWizardStore`). Override it per mount via the
 *                          Provider's `createStore` prop when the store needs
 *                          server-provided initial data.
 *
 * @returns `{ Provider, useStore }` — rename them per feature on destructure.
 *
 * @example
 * // src/features/wizard/store/wizard-store-provider.tsx
 * "use client";
 * import { createStoreContext } from "@/lib/store/create-store-context";
 * import { createWizardStore, type WizardStore } from "./wizard-store";
 *
 * export const { Provider: WizardStoreProvider, useStore: useWizardStore } =
 *   createStoreContext<WizardStore>("Wizard", createWizardStore);
 *
 * // in a client component:
 * const step = useWizardStore((s) => s.step);
 */
export function createStoreContext<T>(
  name: string,
  defaultCreateStore: () => StoreApi<T>,
) {
  const StoreContext = createContext<StoreApi<T> | null>(null);

  function Provider({
    children,
    createStore = defaultCreateStore,
  }: {
    children: ReactNode;
    /** Override the factory, e.g. to seed the store with server data. */
    createStore?: () => StoreApi<T>;
  }) {
    // useState's lazy initializer runs exactly once per Provider instance.
    const [store] = useState(createStore);
    return (
      <StoreContext.Provider value={store}>{children}</StoreContext.Provider>
    );
  }
  Provider.displayName = `${name}StoreProvider`;

  function useStoreSelector<U>(selector: (state: T) => U): U {
    const store = useContext(StoreContext);
    if (store === null) {
      throw new Error(
        `use${name}Store must be used within <${name}StoreProvider>`,
      );
    }
    return useStore(store, selector);
  }

  return { Provider, useStore: useStoreSelector };
}
