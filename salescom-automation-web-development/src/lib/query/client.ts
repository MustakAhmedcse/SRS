import { MutationCache, QueryCache, QueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ROUTES } from "@/lib/constants";
import { toUserMessage } from "@/lib/api/user-error";

// Per-mutation opt-out: `useMutation({ meta: { silent: true }, ... })`
// suppresses the global error toast (for flows that want to render the
// error inline instead). Queries can opt *in* with `meta: { toastOnError:
// true }` — by default a failed query just leaves the component to show
// inline state, since toasting on every refetch failure is noisy.
declare module "@tanstack/react-query" {
  interface Register {
    mutationMeta: { silent?: boolean };
    queryMeta: { toastOnError?: boolean };
  }
}

function getStatus(error: unknown): number | undefined {
  if (typeof error === "object" && error !== null && "status" in error) {
    const s = (error as { status: unknown }).status;
    if (typeof s === "number") return s;
  }
  return undefined;
}

function isOnLoginPage() {
  if (typeof window === "undefined") return false;
  const path = window.location.pathname;
  // The SSO callback is part of the login flow — let its page own the
  // toast + redirect on a failed token exchange instead of hard-redirecting.
  return path === ROUTES.LOGIN || path === ROUTES.LOGIN_CALLBACK;
}

// On any 401 from /api/* (expired token, revoked session), force a hard nav
// to /login. The full reload also clears React Query's in-memory cache for
// this tab, so no stale user/permissions linger. On /login itself we skip
// the redirect — that 401 means "bad credentials" and gets surfaced via
// the global toast handler below.
function handleUnauthorized() {
  if (typeof window === "undefined") return;
  if (isOnLoginPage()) return;
  window.location.href = ROUTES.LOGIN;
}

export function makeQueryClient() {
  return new QueryClient({
    queryCache: new QueryCache({
      onError: (error, query) => {
        if (getStatus(error) === 401) handleUnauthorized();
        // Queries stay silent by default — components render their own
        // inline empty/error states. Opt in per query with meta.toastOnError.
        if (query.meta?.toastOnError) {
          toast.error(toUserMessage(error));
        }
      },
    }),
    mutationCache: new MutationCache({
      onError: (error, _vars, _ctx, mutation) => {
        const status = getStatus(error);
        if (status === 401 && !isOnLoginPage()) {
          // Redirect handles UX; no toast needed since the page is about
          // to unload.
          handleUnauthorized();
          return;
        }
        if (mutation.meta?.silent) return;
        toast.error(toUserMessage(error));
      },
    }),
    defaultOptions: {
      queries: {
        staleTime: 60 * 1000,
        refetchOnWindowFocus: true,
        retry: (failureCount, error) => {
          const s = getStatus(error);
          if (s === 401 || s === 403 || s === 404) return false;
          return failureCount < 2;
        },
      },
    },
  });
}
