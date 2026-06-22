import { isApiError } from "./errors";

// Translates any thrown error from clientFetch / serverFetch / a feature
// api.ts function into a sentence the user should actually see. Keep
// technical detail (status codes, stack traces, server-thrown raw text)
// out of toasts — those belong in logs.
//
// Rules:
//   - 5xx / unknown server failure        → generic "try again"
//   - 408 / timeout                       → timeout copy
//   - 403                                 → permission copy
//   - 404                                 → "Not found"
//   - 4xx WITH a backend message          → that message (it's the
//                                           business reason, like
//                                           "Alias already in use")
//   - 4xx without one                     → generic
//   - logical envelope failure (status=0) → backend's message if any
//   - TypeError from fetch                → offline/network copy
//   - anything else                       → generic
export function toUserMessage(error: unknown): string {
  if (isApiError(error)) {
    const { status, message } = error;

    if (status >= 500) {
      return "Something went wrong on our end. Please try again in a moment.";
    }
    if (status === 408) {
      return "The request timed out. Please try again.";
    }
    if (status === 403) {
      return "You don't have permission to do this.";
    }
    if (status === 404) {
      return "We couldn't find what you were looking for.";
    }

    // Logical failure (no HTTP status). Backend's `message` is usually
    // the business reason — show it if present, fall back if not.
    if (status === 0) {
      return message || "Request failed. Please try again.";
    }

    // 4xx with a usable backend message. The fallback string from
    // normalizeErrorResponse ("Request failed (400)") is technical and
    // must never reach the user, so detect and replace it.
    if (message.startsWith("Request failed (")) {
      return "Request failed. Please try again.";
    }
    return message;
  }

  if (error instanceof TypeError) {
    // fetch() rejects with TypeError on offline / DNS failure / CORS / etc.
    return "Can't reach the server. Check your connection and try again.";
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return "Something went wrong.";
}
