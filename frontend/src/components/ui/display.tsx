"use client";

import * as React from "react";
import { Avatar as AvatarPrimitive, Tabs as TabsPrimitive } from "radix-ui";
import { cn, hashString, initials } from "@/lib/utils";

export type Tone = "neutral" | "accent" | "success" | "warning" | "danger" | "info";

const toneStyles: Record<Tone, { soft: string; dot: string; text: string }> = {
  neutral: { soft: "bg-neutral-soft", dot: "bg-neutral", text: "text-ink-2" },
  accent: { soft: "bg-accent-soft", dot: "bg-accent", text: "text-accent" },
  success: { soft: "bg-success-soft", dot: "bg-success", text: "text-[oklch(from_var(--success)_calc(l-0.06)_c_h)] dark:text-success" },
  warning: { soft: "bg-warning-soft", dot: "bg-warning", text: "text-[oklch(from_var(--warning)_calc(l-0.16)_c_h)] dark:text-warning" },
  danger: { soft: "bg-danger-soft", dot: "bg-danger", text: "text-danger" },
  info: { soft: "bg-info-soft", dot: "bg-info", text: "text-info" },
};

/** Status label: a coloured dot and words, never colour alone. */
export function StatusBadge({ tone = "neutral", children, className, variant = "dot" }: {
  tone?: Tone;
  children: React.ReactNode;
  className?: string;
  variant?: "dot" | "soft";
}) {
  const t = toneStyles[tone];
  if (variant === "soft") {
    return (
      <span className={cn("inline-flex h-5 items-center gap-1.5 rounded-sm px-1.5 text-xs font-medium", t.soft, t.text, className)}>
        {children}
      </span>
    );
  }
  return (
    <span className={cn("inline-flex items-center gap-1.5 text-sm whitespace-nowrap text-ink", className)}>
      <span className={cn("size-1.5 shrink-0 rounded-full", t.dot)} aria-hidden />
      {children}
    </span>
  );
}

export function Badge({ className, children, tone = "neutral" }: { className?: string; children: React.ReactNode; tone?: Tone }) {
  const t = toneStyles[tone];
  return (
    <span className={cn("inline-flex h-5 items-center rounded-sm border border-line px-1.5 text-xs font-medium whitespace-nowrap",
      tone !== "neutral" && [t.soft, t.text, "border-transparent"], tone === "neutral" && "bg-surface text-ink-2", className)}>
      {children}
    </span>
  );
}

const avatarHues = [165, 245, 70, 25, 300, 125];

function avatarStyle(name: string): React.CSSProperties {
  const hue = avatarHues[hashString(name) % avatarHues.length];
  return {
    ["--avatar-hue" as string]: hue,
  };
}

export function Avatar({ name, src, size = 28, className, square, onImageError }: {
  name: string;
  src?: string | null;
  size?: number;
  className?: string;
  square?: boolean;
  onImageError?: () => void;
}) {
  return (
    <AvatarPrimitive.Root
      className={cn("relative inline-flex shrink-0 overflow-hidden select-none", square ? "rounded-md" : "rounded-full", className)}
      style={{ width: size, height: size }}
    >
      {src && (
        <AvatarPrimitive.Image src={src} alt="" className="size-full object-cover"
          onLoadingStatusChange={(status) => status === "error" && onImageError?.()} />
      )}
      <AvatarPrimitive.Fallback
        delayMs={src ? 300 : 0}
        className={cn(
          "grid size-full place-items-center font-medium",
          "bg-[oklch(0.935_0.028_var(--avatar-hue))] text-[oklch(0.42_0.07_var(--avatar-hue))]",
          "dark:bg-[oklch(0.33_0.04_var(--avatar-hue))] dark:text-[oklch(0.86_0.05_var(--avatar-hue))]",
        )}
        style={{ ...avatarStyle(name), fontSize: Math.max(10, Math.round(size * 0.38)) }}
      >
        {initials(name)}
      </AvatarPrimitive.Fallback>
    </AvatarPrimitive.Root>
  );
}

export const Tabs = TabsPrimitive.Root;

export function TabsList({ className, ...props }: React.ComponentProps<typeof TabsPrimitive.List>) {
  // On phones a long tab row scrolls sideways instead of running off the edge.
  return <TabsPrimitive.List className={cn("flex gap-5 border-b border-line max-sm:overflow-x-auto max-sm:overflow-y-hidden max-sm:[scrollbar-width:none]", className)} {...props} />;
}

export function TabsTrigger({ className, ...props }: React.ComponentProps<typeof TabsPrimitive.Trigger>) {
  return (
    <TabsPrimitive.Trigger
      className={cn(
        "relative -mb-px inline-flex h-9 shrink-0 items-center gap-1.5 border-b-2 border-transparent text-sm font-medium whitespace-nowrap text-ink-2 transition-colors",
        "hover:text-ink data-[state=active]:border-ink data-[state=active]:text-ink",
        className,
      )}
      {...props}
    />
  );
}

export const TabsContent = TabsPrimitive.Content;

export function Skeleton({ className, ...props }: React.ComponentProps<"div">) {
  return <div className={cn("animate-pulse rounded-md bg-line/70", className)} {...props} />;
}

export function Kbd({ children, className }: { children: React.ReactNode; className?: string }) {
  return (
    <kbd className={cn("inline-flex h-5 min-w-5 items-center justify-center rounded-sm border border-line bg-surface px-1 font-sans text-2xs font-medium text-ink-2 shadow-[0_1px_0_var(--line)]", className)}>
      {children}
    </kbd>
  );
}

export function Separator({ className, vertical }: { className?: string; vertical?: boolean }) {
  return <div role="separator" className={cn("shrink-0 bg-line", vertical ? "h-full w-px" : "h-px w-full", className)} />;
}

/** Definition list for record details: label column + value column, ruled rows. */
export function DetailList({ items, className, columns = 1 }: {
  items: { label: React.ReactNode; value: React.ReactNode; hidden?: boolean }[];
  className?: string;
  columns?: 1 | 2;
}) {
  return (
    <dl className={cn("grid", columns === 2 && "sm:grid-cols-2 sm:gap-x-10", className)}>
      {items.filter((i) => !i.hidden).map((item, index) => (
        <div key={index}
          className={cn(
            "grid grid-cols-[minmax(7rem,min(40%,13rem))_1fr] gap-3 border-b border-line py-2.5 text-base last:border-b-0",
            // In two columns the bottom row may hold two items: the left one loses its rule too.
            columns === 2 && "sm:[&:nth-last-child(2):nth-child(odd)]:border-b-0",
          )}>
          <dt className="text-ink-2">{item.label}</dt>
          <dd className="min-w-0 text-ink">{item.value ?? <span className="text-ink-3">—</span>}</dd>
        </div>
      ))}
    </dl>
  );
}
