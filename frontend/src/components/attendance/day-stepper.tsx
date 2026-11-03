"use client";

import * as React from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { addDays } from "@/lib/api/attendance";
import { formatDate, todayIso } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

/** ‹ Tue 3 Nov 2026 › with a jump back to today. Future days are allowed only when `allowFuture`. */
export function DayStepper({ value, onChange, allowFuture, className }: {
  value: string;
  onChange: (date: string) => void;
  allowFuture?: boolean;
  className?: string;
}) {
  const today = todayIso();
  const id = React.useId();
  const nextDisabled = !allowFuture && value >= today;
  return (
    <div className={cn("flex items-center gap-1", className)}>
      <Button variant="ghost" size="icon-sm" aria-label="Previous day" onClick={() => onChange(addDays(value, -1))}>
        <ChevronLeft />
      </Button>
      <label htmlFor={id} className="relative inline-flex h-8 cursor-pointer items-center rounded-md border border-line bg-surface px-2.5 text-sm font-medium text-ink tabular hover:border-line-strong">
        {formatDate(value, { weekday: true })}
        <input id={id} type="date" value={value} max={allowFuture ? undefined : today} aria-label="Choose a date"
          onChange={(e) => e.target.value && onChange(e.target.value)}
          className="absolute inset-0 cursor-pointer opacity-0 [&::-webkit-calendar-picker-indicator]:absolute [&::-webkit-calendar-picker-indicator]:inset-0 [&::-webkit-calendar-picker-indicator]:size-full [&::-webkit-calendar-picker-indicator]:cursor-pointer" />
      </label>
      <Button variant="ghost" size="icon-sm" aria-label="Next day" disabled={nextDisabled} onClick={() => onChange(addDays(value, 1))}>
        <ChevronRight />
      </Button>
      {value !== today && (
        <Button variant="ghost" size="sm" onClick={() => onChange(today)}>Today</Button>
      )}
    </div>
  );
}
