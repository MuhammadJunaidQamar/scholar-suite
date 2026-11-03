"use client";

import * as React from "react";
import type { ColumnDef, VisibilityState } from "@tanstack/react-table";
import { Check, Columns3, Download, ListFilter, Rows3, Search, X } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Segmented } from "@/components/ui/toggles";
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/menu";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import type { Density } from "./data-table";
import { useDebouncedValue } from "./use-list-params";

export function TableToolbar({ children, className }: { children: React.ReactNode; className?: string }) {
  return <div className={cn("flex flex-wrap items-center gap-2 px-4 py-3", className)}>{children}</div>;
}

/** Debounced search box bound to the URL `search` param. */
export function TableSearch({
  value,
  onChange,
  placeholder = "Search…",
  className,
}: {
  value: string | undefined;
  onChange: (value: string | undefined) => void;
  placeholder?: string;
  className?: string;
}) {
  const [text, setText] = React.useState(value ?? "");
  const debounced = useDebouncedValue(text, 300);
  const lastPushed = React.useRef(value ?? "");

  React.useEffect(() => {
    if (debounced !== lastPushed.current) {
      lastPushed.current = debounced;
      onChange(debounced || undefined);
    }
  }, [debounced, onChange]);

  React.useEffect(() => {
    // External changes (clear filters, back button) flow back into the box.
    if ((value ?? "") !== lastPushed.current) {
      lastPushed.current = value ?? "";
      setText(value ?? "");
    }
  }, [value]);

  return (
    <Input
      aria-label={placeholder}
      className={cn("w-full sm:w-72", className)}
      leading={<Search />}
      placeholder={placeholder}
      value={text}
      onChange={(e) => setText(e.target.value)}
      onKeyDown={(e) => {
        if (e.key === "Escape") setText("");
      }}
      trailing={
        text ? (
          <button type="button" aria-label="Clear search" onClick={() => setText("")}
            className="grid size-6 place-items-center rounded-sm text-ink-3 hover:bg-sunken hover:text-ink">
            <X className="size-3.5" />
          </button>
        ) : undefined
      }
    />
  );
}

export type FacetOption = { value: string; label: string; hint?: string };

/**
 * A filter chip that opens a searchable option list. Shows the selection inline ("Class · 5-A, 5-B") so active
 * filters are always visible without opening anything.
 */
export function FacetFilter({
  title,
  options,
  selected,
  onChange,
  single,
  searchable = options.length > 8,
}: {
  title: string;
  options: FacetOption[];
  selected: string[];
  onChange: (values: string[]) => void;
  single?: boolean;
  searchable?: boolean;
}) {
  const [open, setOpen] = React.useState(false);
  const labels = selected.map((v) => options.find((o) => o.value === v)?.label ?? v);
  const toggle = (value: string) => {
    if (single) {
      onChange(selected[0] === value ? [] : [value]);
      setOpen(false);
      return;
    }
    onChange(selected.includes(value) ? selected.filter((v) => v !== value) : [...selected, value]);
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          className={cn(
            "inline-flex h-8 items-center gap-1.5 rounded-md border border-dashed border-line-strong px-2.5 text-sm text-ink-2",
            "transition-colors hover:border-ink-3 hover:text-ink",
            selected.length > 0 && "border-solid border-line-strong bg-surface text-ink",
          )}
        >
          <ListFilter className="size-3.5 text-ink-3" />
          <span className="font-medium">{title}</span>
          {selected.length > 0 && (
            <>
              <span className="h-3.5 w-px bg-line-strong" aria-hidden />
              <span className="max-w-48 truncate">
                {labels.length > 2 ? `${labels.length} selected` : labels.join(", ")}
              </span>
            </>
          )}
        </button>
      </PopoverTrigger>
      <PopoverContent className="w-64 p-0" align="start">
        <Command>
          {searchable && <CommandInput placeholder={`Filter ${title.toLowerCase()}…`} className="h-10 text-base" />}
          <CommandList className="max-h-72">
            <CommandEmpty>No options</CommandEmpty>
            <CommandGroup>
              {options.map((option) => {
                const isSelected = selected.includes(option.value);
                return (
                  <CommandItem key={option.value} value={`${option.label} ${option.value}`} onSelect={() => toggle(option.value)}>
                    <span
                      className={cn(
                        "grid size-4 place-items-center border border-line-strong",
                        single ? "rounded-full" : "rounded-xs",
                        isSelected && "border-accent bg-accent text-accent-ink",
                      )}
                    >
                      {isSelected && <Check className="!size-3 !text-accent-ink" strokeWidth={3} />}
                    </span>
                    <span className="flex-1 truncate">{option.label}</span>
                    {option.hint && <span className="text-xs text-ink-3 tabular">{option.hint}</span>}
                  </CommandItem>
                );
              })}
            </CommandGroup>
          </CommandList>
          {selected.length > 0 && (
            <div className="border-t border-line p-1">
              <button type="button" onClick={() => onChange([])}
                className="w-full rounded-md px-2 py-1.5 text-center text-sm text-ink-2 hover:bg-sunken hover:text-ink">
                Clear filter
              </button>
            </div>
          )}
        </Command>
      </PopoverContent>
    </Popover>
  );
}

export function ClearFiltersButton({ count, onClear }: { count: number; onClear: () => void }) {
  if (count === 0) return null;
  return (
    <Button variant="ghost" size="sm" onClick={onClear}>
      <X /> Reset
    </Button>
  );
}

/** Column visibility + density, kept per viewer in local storage. */
export function ViewMenu<T>({
  columns,
  visibility,
  onVisibilityChange,
  density,
  onDensityChange,
}: {
  columns: ColumnDef<T, unknown>[];
  visibility: VisibilityState;
  onVisibilityChange: (state: VisibilityState) => void;
  density: Density;
  onDensityChange: (density: Density) => void;
}) {
  const hideable = columns.filter((c) => !c.meta?.pinned && (c.id || ("accessorKey" in c && c.accessorKey)));
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="secondary" size="md" aria-label="View options">
          <Columns3 /> <span className="max-sm:hidden">View</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-56">
        <div className="flex items-center justify-between px-2 py-1.5">
          <span className="flex items-center gap-1.5 text-sm text-ink-2"><Rows3 className="size-3.5" /> Density</span>
          <Segmented size="sm" value={density} onChange={onDensityChange} aria-label="Row density"
            options={[{ value: "comfortable", label: "Cosy" }, { value: "compact", label: "Compact" }]} />
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuLabel>Columns</DropdownMenuLabel>
        {hideable.map((column) => {
          const id = (column.id ?? ("accessorKey" in column ? String(column.accessorKey) : "")) as string;
          const label = column.meta?.label ?? (typeof column.header === "string" ? column.header : id);
          return (
            <DropdownMenuCheckboxItem
              key={id}
              checked={visibility[id] !== false}
              onSelect={(e) => e.preventDefault()}
              onCheckedChange={(checked) => onVisibilityChange({ ...visibility, [id]: checked })}
            >
              {label}
            </DropdownMenuCheckboxItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export function ExportMenu({ onExport, busy }: { onExport: (format: "csv" | "xlsx") => void; busy?: boolean }) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="secondary" loading={busy}>
          <Download /> <span className="max-sm:hidden">Export</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent>
        <DropdownMenuLabel>Export current view</DropdownMenuLabel>
        <DropdownMenuItem onSelect={() => onExport("csv")}>CSV (.csv)</DropdownMenuItem>
        <DropdownMenuItem onSelect={() => onExport("xlsx")}>Excel (.xlsx)</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

/** Persists a per-viewer table preference (density, visible columns). Storage failures fall back to defaults. */
export function useTablePreference<T>(key: string, initial: T): [T, (value: T) => void] {
  const storageKey = `ss.table.${key}`;
  const [value, setValue] = React.useState<T>(initial);
  React.useEffect(() => {
    try {
      const raw = window.localStorage.getItem(storageKey);
      // eslint-disable-next-line react-hooks/set-state-in-effect
      if (raw) setValue(JSON.parse(raw) as T);
    } catch {
      /* storage unavailable: keep defaults */
    }
  }, [storageKey]);
  const update = React.useCallback(
    (next: T) => {
      setValue(next);
      try {
        window.localStorage.setItem(storageKey, JSON.stringify(next));
      } catch {
        /* ignore */
      }
    },
    [storageKey],
  );
  return [value, update];
}
