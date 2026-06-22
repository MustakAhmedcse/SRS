// Single source of truth for permission codes. Keep entries alphabetized
// within their group. Raw integer codes must never appear in JSX or hooks —
// always reference PERM.X so a rename is a single find-and-replace.
//
// Replace the placeholders with real backend codes once the catalog is
// confirmed with the Trade Marketing API team.
export const PERM = {
  // Reserved slot for verification gating tests. Never granted in production.
  TEST: -1,

  // === Data sources (Administrator setup) ===
  // TODO: best-guess from the mock login's 900021–900037 range; replace
  // with the real code once the Trade Marketing API team confirms.
  DATA_SOURCES_VIEW: 900021,
  DATA_SOURCES_CREATE: 900022,
  DATA_SOURCES_EDIT: 900023,

  // === Programmes ===
  // PROGRAMMES_APPROVE: 1004,
  // PROGRAMMES_CREATE: 1002,
  // PROGRAMMES_EDIT: 1003,
  // PROGRAMMES_RUN: 1005,
  // PROGRAMMES_VIEW: 1001,

  // === Runs ===
  // RUNS_EXECUTE: 2002,
  // RUNS_VIEW: 2001,

  // === Reports ===
  // REPORTS_EXPORT: 3002,
  // REPORTS_VIEW: 3001,
} as const;

export type PermCode = (typeof PERM)[keyof typeof PERM];
