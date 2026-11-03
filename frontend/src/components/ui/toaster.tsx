"use client";

import { Toaster as Sonner, toast as sonnerToast } from "sonner";
import { useTheme } from "next-themes";
import { ApiError } from "@/lib/api/client";

export function Toaster() {
  const { resolvedTheme } = useTheme();
  return (
    <Sonner
      theme={(resolvedTheme as "light" | "dark") ?? "light"}
      position="bottom-right"
      gap={8}
      offset={20}
      visibleToasts={4}
      toastOptions={{
        classNames: {
          toast:
            "!rounded-lg !border !border-line !bg-raised !text-ink !shadow-popover !font-sans !text-base !py-3 !px-3.5 !gap-2.5",
          title: "!font-medium !text-base",
          description: "!text-ink-2 !text-sm",
          actionButton: "!bg-accent !text-accent-ink !rounded-md !text-sm !font-medium",
          cancelButton: "!bg-sunken !text-ink !rounded-md",
          icon: "!mt-px",
          success: "[&_[data-icon]]:!text-success",
          error: "[&_[data-icon]]:!text-danger",
          warning: "[&_[data-icon]]:!text-warning",
        },
      }}
    />
  );
}

/** Toasts confirm outcomes; they never carry information that exists nowhere else. */
export const toast = {
  success: (title: string, description?: string) => sonnerToast.success(title, { description }),
  info: (title: string, description?: string) => sonnerToast(title, { description }),
  warning: (title: string, description?: string) => sonnerToast.warning(title, { description }),
  error: (error: unknown, fallback = "Something went wrong") => {
    if (error instanceof ApiError) {
      sonnerToast.error(error.problem.title && error.status >= 500 ? fallback : error.message, {
        description: error.status >= 500 && error.problem.traceId ? `Reference ${error.problem.traceId}` : undefined,
      });
    } else {
      sonnerToast.error(fallback, { description: error instanceof Error ? error.message : undefined });
    }
  },
};
