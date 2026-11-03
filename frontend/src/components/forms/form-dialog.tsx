"use client";

import * as React from "react";
import type { FieldValues, Path, UseFormReturn } from "react-hook-form";
import { ApiError } from "@/lib/api/client";
import { Button } from "@/components/ui/button";
import { Dialog, DialogBody, DialogContent, DialogFooter, DialogHeader } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toaster";

/** Maps API validation problems onto form fields; returns false when the error was not a field error. */
export function applyFieldErrors<T extends FieldValues>(error: unknown, form: UseFormReturn<T>): boolean {
  if (!(error instanceof ApiError) || !error.isValidation) return false;
  const known = new Set(Object.keys(form.getValues()));
  let applied = false;
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    const root = field.split(".")[0]!;
    if (known.has(root)) {
      form.setError(field as Path<T>, { message: messages[0] ?? "Invalid value" });
      applied = true;
    }
  }
  return applied;
}

/**
 * Dialog + react-hook-form wiring used by every create/edit flow: resets to `defaults` each time it opens,
 * disables double submission, keeps the dialog open and highlights fields on validation errors, and toasts
 * everything else.
 */
export function FormDialog<T extends FieldValues>({
  open,
  onOpenChange,
  title,
  description,
  form,
  defaults,
  onSubmit,
  submitLabel = "Save",
  size = "md",
  children,
  footerStart,
  successMessage,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: React.ReactNode;
  description?: React.ReactNode;
  form: UseFormReturn<T>;
  defaults: T;
  onSubmit: (values: T) => Promise<unknown>;
  submitLabel?: string;
  size?: "sm" | "md" | "lg" | "xl";
  children: React.ReactNode;
  footerStart?: React.ReactNode;
  successMessage?: string | ((values: T) => string);
}) {
  const [pending, setPending] = React.useState(false);

  // Reset only when the dialog opens, to the defaults of that moment. `defaults` is rebuilt on every render by
  // callers, so depending on it would wipe the user's typing on each keystroke.
  React.useEffect(() => {
    if (open) form.reset(defaults);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const submit = form.handleSubmit(async (values) => {
    if (pending) return;
    setPending(true);
    try {
      await onSubmit(values);
      if (successMessage) toast.success(typeof successMessage === "function" ? successMessage(values) : successMessage);
      onOpenChange(false);
    } catch (error) {
      if (!applyFieldErrors(error, form)) toast.error(error, "That couldn't be saved");
    } finally {
      setPending(false);
    }
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent size={size}>
        <DialogHeader title={title} description={description} />
        <form onSubmit={submit} noValidate className="contents">
          <DialogBody className="grid gap-4">{children}</DialogBody>
          <DialogFooter>
            {footerStart && <div className="mr-auto">{footerStart}</div>}
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" variant="primary" loading={pending}>{submitLabel}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/** Convenience for optional text inputs: empty strings become null for the API. */
export function nullIfEmpty(value: string | null | undefined): string | null {
  return value && value.trim() ? value.trim() : null;
}
