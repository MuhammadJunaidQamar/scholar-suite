import * as React from "react";
import { cn } from "@/lib/utils";

export const controlBase = cn(
  "w-full min-w-0 rounded-md border border-line-strong bg-surface text-ink",
  "shadow-[0_1px_1px_oklch(0.2_0.01_70/0.03)] transition-[border-color,box-shadow] duration-150",
  "placeholder:text-ink-3 hover:border-[oklch(from_var(--line-strong)_calc(l-0.06)_c_h)]",
  "focus:outline-none focus:border-accent focus:shadow-[0_0_0_3px_color-mix(in_oklch,var(--accent)_16%,transparent)]",
  "aria-invalid:border-danger aria-invalid:focus:shadow-[0_0_0_3px_color-mix(in_oklch,var(--danger)_16%,transparent)]",
  "disabled:cursor-not-allowed disabled:bg-sunken disabled:text-ink-2 disabled:opacity-80",
  "read-only:bg-sunken",
);

export type InputProps = React.ComponentProps<"input"> & {
  /** Icon or text rendered inside the control on the left (e.g. a search glyph or "Rs"). */
  leading?: React.ReactNode;
  /** Content rendered inside the control on the right (e.g. a unit or a clear button). */
  trailing?: React.ReactNode;
  inputSize?: "sm" | "md" | "lg";
};

export function Input({ className, leading, trailing, inputSize = "md", type = "text", ...props }: InputProps) {
  const height = inputSize === "sm" ? "h-7 text-sm" : inputSize === "lg" ? "h-10 text-md" : "h-8 text-base";
  if (!leading && !trailing) {
    return <input type={type} data-slot="input" className={cn(controlBase, height, "px-2.5", className)} {...props} />;
  }
  return (
    <div className={cn("relative flex items-center", className)}>
      {leading && (
        <span className="pointer-events-none absolute left-2.5 flex items-center text-ink-3 [&_svg]:size-4">{leading}</span>
      )}
      <input
        type={type}
        data-slot="input"
        className={cn(controlBase, height, "px-2.5", leading ? "pl-8" : undefined, trailing ? "pr-9" : undefined)}
        {...props}
      />
      {trailing && <span className="absolute right-1.5 flex items-center text-ink-3 [&_svg]:size-4">{trailing}</span>}
    </div>
  );
}

export function Textarea({ className, ...props }: React.ComponentProps<"textarea">) {
  return (
    <textarea
      data-slot="textarea"
      className={cn(controlBase, "min-h-20 px-2.5 py-2 text-base leading-relaxed", className)}
      {...props}
    />
  );
}

/** A native select styled like the other controls (best for long, simple option lists and mobile). */
export function NativeSelect({ className, children, ...props }: React.ComponentProps<"select">) {
  return (
    <select
      data-slot="native-select"
      className={cn(
        controlBase,
        "h-8 appearance-none bg-[length:16px] bg-[right_8px_center] bg-no-repeat pr-8 pl-2.5 text-base",
        "bg-[url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='%23857f75' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpath d='m7 15 5 5 5-5'/%3E%3Cpath d='m7 9 5-5 5 5'/%3E%3C/svg%3E\")]",
        className,
      )}
      {...props}
    >
      {children}
    </select>
  );
}
