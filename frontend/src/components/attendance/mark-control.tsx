"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

type Tone = "neutral" | "accent" | "success" | "warning" | "danger" | "info";

const selected: Record<Tone, string> = {
  neutral: "bg-surface text-ink shadow-[0_0_0_1px_var(--line-strong)]",
  accent: "bg-accent-soft text-accent shadow-[0_0_0_1px_var(--accent-line)]",
  success: "bg-success-soft text-[oklch(from_var(--success)_calc(l-0.08)_c_h)] dark:text-success shadow-[0_0_0_1px_color-mix(in_oklch,var(--success)_38%,transparent)]",
  warning: "bg-warning-soft text-[oklch(from_var(--warning)_calc(l-0.18)_c_h)] dark:text-warning shadow-[0_0_0_1px_color-mix(in_oklch,var(--warning)_42%,transparent)]",
  danger: "bg-danger-soft text-danger shadow-[0_0_0_1px_color-mix(in_oklch,var(--danger)_40%,transparent)]",
  info: "bg-info-soft text-info shadow-[0_0_0_1px_color-mix(in_oklch,var(--info)_38%,transparent)]",
};

export type MarkOption<T extends string> = { value: T; label: string; code: string; tone: Tone; shortcut?: string };

/**
 * The mark buttons of a register row: every status visible at once, the chosen one tinted with its meaning. Shows
 * register codes (P, A…) on phones and words on wider screens; the row itself carries the keyboard shortcuts.
 */
export function MarkControl<T extends string>({
  value,
  options,
  onChange,
  disabled,
  label,
  className,
  codesOnly,
}: {
  value: T;
  options: MarkOption<T>[];
  onChange: (value: T) => void;
  disabled?: boolean;
  label: string;
  className?: string;
  codesOnly?: boolean;
}) {
  return (
    <div role="radiogroup" aria-label={label} aria-disabled={disabled || undefined}
      className={cn("inline-flex shrink-0 rounded-md border border-line bg-sunken p-0.5", disabled && "opacity-70", className)}>
      {options.map((option) => {
        const checked = option.value === value;
        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={checked}
            aria-label={option.label}
            tabIndex={-1}
            disabled={disabled}
            title={option.shortcut ? `${option.label} (${option.shortcut.toUpperCase()})` : option.label}
            onClick={() => onChange(option.value)}
            className={cn(
              "h-7 min-w-8 rounded-[5px] px-2 text-sm font-medium text-ink-3 transition-colors select-none",
              !disabled && "hover:text-ink",
              checked && selected[option.tone],
              disabled && "cursor-default",
            )}
          >
            {codesOnly ? option.code : (
              <>
                <span className="sm:hidden">{option.code}</span>
                <span className="max-sm:hidden">{option.label}</span>
              </>
            )}
          </button>
        );
      })}
    </div>
  );
}
