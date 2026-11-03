import * as React from "react";
import { Label as LabelPrimitive } from "radix-ui";
import { CircleAlert } from "lucide-react";
import { cn } from "@/lib/utils";

export function Label({ className, ...props }: React.ComponentProps<typeof LabelPrimitive.Root>) {
  return (
    <LabelPrimitive.Root
      data-slot="label"
      className={cn("text-sm font-medium text-ink select-none peer-disabled:opacity-60", className)}
      {...props}
    />
  );
}

type FieldProps = {
  label?: React.ReactNode;
  htmlFor?: string;
  hint?: React.ReactNode;
  error?: string | string[] | null;
  optional?: boolean;
  className?: string;
  /** Places the label beside the control (checkbox/switch rows). */
  inline?: boolean;
  action?: React.ReactNode;
  children: React.ReactNode;
};

/**
 * A labelled form control. Most fields in the product are required, so optional ones are marked
 * "(optional)" instead of decorating every required label with an asterisk.
 */
export function Field({ label, htmlFor, hint, error, optional, className, inline, action, children }: FieldProps) {
  const message = Array.isArray(error) ? error[0] : error;
  const hintId = htmlFor ? `${htmlFor}-hint` : undefined;
  const errorId = htmlFor ? `${htmlFor}-error` : undefined;
  const control = React.isValidElement<Record<string, unknown>>(children)
    ? React.cloneElement(children, {
        id: (children.props.id as string | undefined) ?? htmlFor,
        "aria-invalid": message ? true : (children.props["aria-invalid"] as boolean | undefined),
        "aria-describedby": cn(message ? errorId : undefined, hint ? hintId : undefined) || undefined,
      })
    : children;

  if (inline) {
    return (
      <div className={cn("flex items-start gap-2.5", className)}>
        <div className="pt-0.5">{control}</div>
        <div className="grid gap-0.5">
          {label && <Label htmlFor={htmlFor}>{label}</Label>}
          {hint && (
            <p id={hintId} className="text-xs text-ink-2">
              {hint}
            </p>
          )}
          {message && <FieldError id={errorId}>{message}</FieldError>}
        </div>
      </div>
    );
  }

  return (
    <div data-slot="field" className={cn("grid content-start gap-1.5", className)}>
      {(label || action) && (
        <div className="flex items-baseline justify-between gap-2">
          {label && (
            <Label htmlFor={htmlFor}>
              {label}
              {optional && <span className="ml-1 font-normal text-ink-3">(optional)</span>}
            </Label>
          )}
          {action}
        </div>
      )}
      {control}
      {message ? (
        <FieldError id={errorId}>{message}</FieldError>
      ) : hint ? (
        <p id={hintId} className="text-xs text-ink-2">
          {hint}
        </p>
      ) : null}
    </div>
  );
}

export function FieldError({ children, id }: { children: React.ReactNode; id?: string }) {
  return (
    <p id={id} role="alert" className="flex items-start gap-1 text-xs text-danger">
      <CircleAlert className="mt-px size-3.5 shrink-0" aria-hidden />
      <span>{children}</span>
    </p>
  );
}

/**
 * A titled block of a form. On wide screens the title/description sit in a left column and the fields on the
 * right — the arrangement used throughout settings and record editing.
 */
export function FormSection({
  title,
  description,
  children,
  className,
  aside,
}: {
  title: React.ReactNode;
  description?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
  aside?: React.ReactNode;
}) {
  return (
    <section className={cn("grid gap-x-10 gap-y-4 border-t border-line py-7 first:border-t-0 first:pt-0 lg:grid-cols-[minmax(0,15rem)_minmax(0,1fr)]", className)}>
      <div className="grid content-start gap-1">
        <h2 className="text-md font-semibold text-ink">{title}</h2>
        {description && <p className="text-sm text-ink-2">{description}</p>}
        {aside}
      </div>
      <div className="grid min-w-0 gap-4">{children}</div>
    </section>
  );
}

export function FieldRow({ children, className, cols = 2 }: { children: React.ReactNode; className?: string; cols?: 2 | 3 | 4 }) {
  return (
    <div
      className={cn(
        "grid gap-4",
        cols === 2 && "sm:grid-cols-2",
        cols === 3 && "sm:grid-cols-3",
        cols === 4 && "sm:grid-cols-2 lg:grid-cols-4",
        className,
      )}
    >
      {children}
    </div>
  );
}
