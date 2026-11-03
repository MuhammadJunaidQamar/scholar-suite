"use client";

import * as React from "react";
import { Ellipsis } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "./button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuTrigger } from "./menu";

/**
 * Ruled list for small catalogues (campuses, departments, rooms…): one row per record with a title, a quiet
 * secondary line, right-aligned facts and an optional actions menu. No cards, no boxes inside boxes.
 */
export function RecordList({ children, className }: { children: React.ReactNode; className?: string }) {
  return <ul className={cn("divide-y divide-line rounded-lg border border-line bg-surface", className)}>{children}</ul>;
}

export function RecordRow({
  leading,
  title,
  badges,
  subtitle,
  facts,
  actions,
  onClick,
  muted,
}: {
  leading?: React.ReactNode;
  title: React.ReactNode;
  badges?: React.ReactNode;
  subtitle?: React.ReactNode;
  facts?: React.ReactNode[];
  actions?: React.ReactNode;
  onClick?: () => void;
  muted?: boolean;
}) {
  return (
    <li
      className={cn("group/row flex items-center gap-3.5 px-4 py-3", onClick && "cursor-pointer transition-colors hover:bg-sunken/60")}
      onClick={(e) => {
        if ((e.target as HTMLElement).closest("button,a,[role=menuitem]")) return;
        onClick?.();
      }}
    >
      {leading}
      <div className={cn("grid min-w-0 flex-1 gap-0.5", muted && "opacity-60")}>
        <div className="flex min-w-0 items-center gap-2">
          <span className="truncate text-base font-medium text-ink">{title}</span>
          {badges}
        </div>
        {subtitle && <div className="truncate text-sm text-ink-2">{subtitle}</div>}
      </div>
      {facts?.map((fact, index) => (
        <div key={index} className="hidden shrink-0 text-right text-sm text-ink-2 tabular sm:block">
          {fact}
        </div>
      ))}
      {actions && (
        <DropdownMenu modal={false}>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon-sm" aria-label="Actions"
              className="opacity-0 group-hover/row:opacity-100 focus-visible:opacity-100 data-[state=open]:opacity-100 [@media(hover:none)]:opacity-100">
              <Ellipsis />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent>{actions}</DropdownMenuContent>
        </DropdownMenu>
      )}
    </li>
  );
}

export function SectionHeading({ title, description, actions }: { title: React.ReactNode; description?: React.ReactNode; actions?: React.ReactNode }) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-3 pb-2">
      <div className="grid gap-0.5">
        <h2 className="text-md font-semibold text-ink">{title}</h2>
        {description && <p className="text-sm text-ink-2">{description}</p>}
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}
