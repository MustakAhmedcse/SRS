"use client";

import { type ReactNode } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReactQueryDevtools } from "@tanstack/react-query-devtools";
import { makeQueryClient } from "./client";

let browserClient: QueryClient | undefined;

function getClient() {
  if (typeof window === "undefined") return makeQueryClient();
  if (!browserClient) browserClient = makeQueryClient();
  return browserClient;
}

export function QueryProvider({ children }: { children: ReactNode }) {
  const client = getClient();
  return (
    <QueryClientProvider client={client}>
      {children}
      <ReactQueryDevtools initialIsOpen={false} buttonPosition="bottom-right" />
    </QueryClientProvider>
  );
}
