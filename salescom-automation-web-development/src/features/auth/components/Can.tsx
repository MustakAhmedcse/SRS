"use client";

import { type ReactNode } from "react";
import { useCan, usePermissions } from "../hooks";

type SingleProps = {
  permission: number;
  fallback?: ReactNode;
  children: ReactNode;
};

type MultiProps = {
  permissions: number[];
  fallback?: ReactNode;
  children: ReactNode;
};

export function Can({ permission, fallback = null, children }: SingleProps) {
  const allowed = useCan(permission);
  return <>{allowed ? children : fallback}</>;
}

export function CanAny({ permissions, fallback = null, children }: MultiProps) {
  const set = usePermissions();
  const allowed = permissions.some((p) => set.has(p));
  return <>{allowed ? children : fallback}</>;
}

export function CanAll({ permissions, fallback = null, children }: MultiProps) {
  const set = usePermissions();
  const allowed = permissions.every((p) => set.has(p));
  return <>{allowed ? children : fallback}</>;
}
