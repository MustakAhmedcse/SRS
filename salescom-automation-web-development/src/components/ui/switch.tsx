"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

// Accessible toggle built on a native button with role="switch" — no
// extra radix dep, full keyboard support via the button element, and
// the ref/disabled/className surface needed for RHF Controller binding.
type SwitchProps = {
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
  disabled?: boolean;
  className?: string;
  id?: string;
  name?: string;
  "aria-labelledby"?: string;
};

const Switch = React.forwardRef<HTMLButtonElement, SwitchProps>(
  (
    { checked, onCheckedChange, disabled, className, ...rest },
    ref,
  ) => (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      data-state={checked ? "checked" : "unchecked"}
      disabled={disabled}
      onClick={() => onCheckedChange(!checked)}
      ref={ref}
      className={cn(
        "relative inline-flex h-6 w-11 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50",
        checked ? "bg-primary" : "bg-input",
        className,
      )}
      {...rest}
    >
      <span
        aria-hidden
        className={cn(
          "pointer-events-none block h-5 w-5 rounded-full bg-background shadow ring-0 transition-transform",
          checked ? "translate-x-5" : "translate-x-0",
        )}
      />
    </button>
  ),
);
Switch.displayName = "Switch";

export { Switch };
