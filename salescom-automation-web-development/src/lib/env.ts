import { z } from "zod";

const ServerEnv = z.object({
  BACKEND_API_URL: z.string().url(),
});

let cached: z.infer<typeof ServerEnv> | undefined;

export function env() {
  if (cached) return cached;
  cached = ServerEnv.parse({
    BACKEND_API_URL: process.env.BACKEND_API_URL,
  });
  return cached;
}
