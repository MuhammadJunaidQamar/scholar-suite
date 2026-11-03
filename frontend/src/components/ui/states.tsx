import * as React from "react";
import { CircleAlert, LoaderCircle, RefreshCw } from "lucide-react";
import { cn } from "@/lib/utils";
import { ApiError } from "@/lib/api/client";
import { Button } from "./button";

/**
 * Empty states explain what belongs here and offer the next step — no illustrations, just a quiet
 * typographic block with a single glyph.
 */
export function EmptyState({
  icon,
  title,
  description,
  action,
  className,
  compact,
}: {
  icon?: React.ReactNode;
  title: React.ReactNode;
  description?: React.ReactNode;
  action?: React.ReactNode;
  className?: string;
  compact?: boolean;
}) {
  return (
    <div className={cn("flex flex-col items-center justify-center text-center", compact ? "gap-2 px-6 py-10" : "gap-3 px-6 py-20", className)}>
      {icon && (
        <div className="grid size-10 place-items-center rounded-lg border border-line bg-surface text-ink-2 shadow-[0_1px_1px_oklch(0.2_0.01_70/0.04)] [&_svg]:size-5">
          {icon}
        </div>
      )}
      <div className="grid max-w-sm gap-1">
        <p className="text-md font-semibold text-ink">{title}</p>
        {description && <p className="text-sm text-ink-2">{description}</p>}
      </div>
      {action && <div className="mt-1 flex flex-wrap justify-center gap-2">{action}</div>}
    </div>
  );
}

export function ErrorState({ error, onRetry, className, title = "This couldn't be loaded" }: {
  error: unknown;
  onRetry?: () => void;
  className?: string;
  title?: string;
}) {
  const message =
    error instanceof ApiError
      ? error.status === 403
        ? "You don't have permission to see this. Ask an administrator if you need access."
        : error.message
      : "Check your connection and try again.";
  return (
    <div role="alert" className={cn("flex flex-col items-center gap-3 px-6 py-16 text-center", className)}>
      <div className="grid size-10 place-items-center rounded-lg bg-danger-soft text-danger">
        <CircleAlert className="size-5" />
      </div>
      <div className="grid max-w-sm gap-1">
        <p className="text-md font-semibold">{title}</p>
        <p className="text-sm text-ink-2">{message}</p>
      </div>
      {onRetry && (
        <Button size="sm" onClick={onRetry}>
          <RefreshCw /> Try again
        </Button>
      )}
    </div>
  );
}

export function Spinner({ className, label = "Loading" }: { className?: string; label?: string }) {
  return (
    <span role="status" className={cn("inline-flex items-center gap-2 text-sm text-ink-2", className)}>
      <LoaderCircle className="size-4 animate-spin" aria-hidden />
      <span className="sr-only">{label}</span>
    </span>
  );
}

export function LoadingBlock({ className, label }: { className?: string; label?: string }) {
  return (
    <div className={cn("flex items-center justify-center gap-2 py-16 text-sm text-ink-2", className)}>
      <LoaderCircle className="size-4 animate-spin" aria-hidden /> {label ?? "Loading…"}
    </div>
  );
}
