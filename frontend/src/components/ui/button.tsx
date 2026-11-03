import * as React from "react";
import { Slot } from "radix-ui";
import { cva, type VariantProps } from "class-variance-authority";
import { LoaderCircle } from "lucide-react";
import { cn } from "@/lib/utils";

export const buttonVariants = cva(
  [
    "relative inline-flex shrink-0 items-center justify-center gap-1.5 whitespace-nowrap rounded-md font-medium",
    "transition-[background-color,border-color,color,box-shadow] duration-150 select-none",
    "disabled:pointer-events-none disabled:opacity-50 aria-disabled:pointer-events-none aria-disabled:opacity-50",
    "data-[loading=true]:pointer-events-none data-[loading=true]:opacity-100",
    "[&_svg]:pointer-events-none [&_svg]:shrink-0",
  ],
  {
    variants: {
      variant: {
        primary:
          "bg-accent text-accent-ink shadow-[inset_0_1px_0_oklch(1_0_0/0.12),0_1px_1px_oklch(0.2_0.01_70/0.12)] hover:bg-accent-hover",
        secondary:
          "border border-line-strong bg-surface text-ink shadow-[0_1px_1px_oklch(0.2_0.01_70/0.04)] hover:bg-sunken hover:border-line-strong",
        ghost: "text-ink-2 hover:bg-sunken hover:text-ink",
        subtle: "bg-sunken text-ink hover:bg-line",
        danger:
          "bg-danger-solid text-danger-solid-ink shadow-[inset_0_1px_0_oklch(1_0_0/0.12),0_1px_1px_oklch(0.2_0.01_70/0.12)] hover:bg-[oklch(from_var(--danger-solid)_calc(l-0.04)_c_h)]",
        "danger-ghost": "text-danger hover:bg-danger-soft",
        link: "h-auto px-0 text-accent underline-offset-4 hover:underline",
      },
      size: {
        xs: "h-6 px-2 text-xs [&_svg]:size-3.5",
        sm: "h-7 px-2.5 text-sm [&_svg]:size-3.5",
        md: "h-8 px-3 text-sm [&_svg]:size-4",
        lg: "h-10 px-4 text-base [&_svg]:size-4",
        icon: "size-8 [&_svg]:size-4",
        "icon-sm": "size-7 [&_svg]:size-3.5",
        "icon-xs": "size-6 [&_svg]:size-3.5",
      },
    },
    defaultVariants: { variant: "secondary", size: "md" },
  },
);

export type ButtonProps = React.ComponentProps<"button"> &
  VariantProps<typeof buttonVariants> & {
    asChild?: boolean;
    loading?: boolean;
  };

export function Button({ className, variant, size, asChild, loading, disabled, children, onClick, ...props }: ButtonProps) {
  const Component = asChild ? Slot.Root : "button";
  return (
    <Component
      data-slot="button"
      className={cn(buttonVariants({ variant, size }), className)}
      disabled={disabled}
      aria-disabled={loading || undefined}
      aria-busy={loading || undefined}
      data-loading={loading || undefined}
      onClick={(event: React.MouseEvent<HTMLButtonElement>) => {
        // A busy button must not be re-activated from the keyboard (prevents double submissions).
        if (loading) {
          event.preventDefault();
          return;
        }
        onClick?.(event);
      }}
      {...props}
    >
      {asChild ? (
        children
      ) : (
        <>
          {loading && <LoaderCircle className="absolute animate-spin" aria-hidden />}
          <span className={cn("contents", loading && "invisible")}>{children}</span>
        </>
      )}
    </Component>
  );
}
