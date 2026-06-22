// Runs once inside every Node runtime Next.js spawns (server + Turbopack
// workers) before any request handler executes. We use it as the single
// reliable place to flip TLS verification off in dev so the BFF proxy can
// reach the local .NET backend's self-signed cert. Production builds short-
// circuit the assignment entirely — there is no way for this to leak.

export function register() {
  if (process.env.NEXT_RUNTIME === "nodejs" && process.env.NODE_ENV !== "production") {
    process.env.NODE_TLS_REJECT_UNAUTHORIZED = "0";
  }
}
