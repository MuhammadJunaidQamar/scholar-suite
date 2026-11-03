import * as React from "react";
import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";

export type Crumb = { label: string; href?: string };

export function Breadcrumbs({ items, className }: { items: Crumb[]; className?: string }) {
  if (items.length === 0) return null;
  return (
    <nav aria-label="Breadcrumb" className={cn("flex items-center gap-1 text-sm text-ink-3", className)}>
      {items.map((item, index) => (
        <React.Fragment key={index}>
          {index > 0 && <ChevronRight className="size-3.5 shrink-0" aria-hidden />}
          {item.href ? (
            <Link href={item.href} className="truncate rounded-sm hover:text-ink">
              {item.label}
            </Link>
          ) : (
            <span className="truncate text-ink-2" aria-current="page">{item.label}</span>
          )}
        </React.Fragment>
      ))}
    </nav>
  );
}

/**
 * Every page opens the same way: optional breadcrumbs, a serif title, one line of context and the page's actions.
 * The title is the only large type on the page.
 */
export function PageHeader({
  title,
  description,
  breadcrumbs,
  actions,
  meta,
  className,
}: {
  title: React.ReactNode;
  description?: React.ReactNode;
  breadcrumbs?: Crumb[];
  actions?: React.ReactNode;
  meta?: React.ReactNode;
  className?: string;
}) {
  return (
    <header className={cn("grid gap-3 pt-6 pb-5", className)}>
      {breadcrumbs && <Breadcrumbs items={breadcrumbs} />}
      <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
        <div className="grid min-w-0 gap-1">
          <h1 className="font-display text-2xl text-ink">{title}</h1>
          {description && <p className="max-w-2xl text-base text-ink-2">{description}</p>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {meta && <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-ink-2">{meta}</div>}
    </header>
  );
}

/** Standard page width and gutters. */
export function Page({ children, className, width = "wide" }: { children: React.ReactNode; className?: string; width?: "wide" | "narrow" | "full" }) {
  return (
    <div
      className={cn(
        "mx-auto w-full px-4 pb-16 sm:px-6 lg:px-8",
        width === "wide" && "max-w-[1400px]",
        width === "narrow" && "max-w-4xl",
        className,
      )}
    >
      {children}
    </div>
  );
}

/** A ruled region of a page: title row + content, no box. */
export function Panel({
  title,
  description,
  actions,
  children,
  className,
  flush,
}: {
  title?: React.ReactNode;
  description?: React.ReactNode;
  actions?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
  /** Content runs edge to edge (tables). */
  flush?: boolean;
}) {
  return (
    <section className={cn("rounded-lg border border-line bg-surface", className)}>
      {(title || actions) && (
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-4 py-3">
          <div className="grid gap-0.5">
            {title && <h2 className="text-md font-semibold text-ink">{title}</h2>}
            {description && <p className="text-sm text-ink-2">{description}</p>}
          </div>
          {actions && <div className="flex items-center gap-2">{actions}</div>}
        </div>
      )}
      <div className={cn(!flush && "p-4")}>{children}</div>
    </section>
  );
}
