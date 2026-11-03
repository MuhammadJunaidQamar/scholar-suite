"use client";

import * as React from "react";
import { Search, X } from "lucide-react";
import { useStudents, type StudentListItem } from "@/lib/api/students";
import { cn } from "@/lib/utils";
import { Popover, PopoverAnchor, PopoverContent } from "@/components/ui/menu";
import { Button } from "@/components/ui/button";
import { RecordAvatar } from "@/components/records/record-avatar";

export type PickedStudent = Pick<StudentListItem, "id" | "fullName" | "admissionNumber" | "className" | "photoFileId">;

/** Find a current student by name or admission number (for leave and other per-student actions). */
export function StudentPicker({ value, onChange, id, invalid, autoFocus }: {
  value: PickedStudent | null;
  onChange: (student: PickedStudent | null) => void;
  id?: string;
  invalid?: boolean;
  autoFocus?: boolean;
}) {
  const [term, setTerm] = React.useState("");
  const [open, setOpen] = React.useState(false);
  const [active, setActive] = React.useState(0);
  const listId = React.useId();
  const query = term.trim();
  const results = useStudents({ search: query || undefined, pageSize: 8, sort: "name" });
  const items = query.length >= 2 ? results.data?.items ?? [] : [];

  if (value) {
    return (
      <div className="flex h-10 items-center gap-2.5 rounded-md border border-line bg-surface px-2.5">
        <RecordAvatar name={value.fullName} fileId={value.photoFileId} size={24} />
        <span className="min-w-0 flex-1 truncate text-base text-ink">
          {value.fullName} <span className="text-sm text-ink-2">· {value.className ?? "—"} · <span className="font-mono">{value.admissionNumber}</span></span>
        </span>
        <Button type="button" variant="ghost" size="icon-xs" aria-label="Choose another student" onClick={() => onChange(null)}><X /></Button>
      </div>
    );
  }

  const pick = (s: StudentListItem) => {
    onChange({ id: s.id, fullName: s.fullName, admissionNumber: s.admissionNumber, className: s.className, photoFileId: s.photoFileId });
    setTerm("");
    setOpen(false);
  };

  return (
    <Popover open={open && items.length > 0} onOpenChange={setOpen}>
      <PopoverAnchor asChild>
        <div className={cn("flex h-8 items-center gap-2 rounded-md border border-line-strong bg-surface px-2.5 focus-within:border-accent focus-within:shadow-[0_0_0_3px_var(--focus)]",
          invalid && "border-danger")}>
          <Search className="size-3.5 shrink-0 text-ink-3" />
          <input id={id} autoFocus={autoFocus} value={term} placeholder="Search by name or admission number" autoComplete="off"
            role="combobox" aria-controls={listId} aria-expanded={open && items.length > 0} aria-autocomplete="list" aria-invalid={invalid || undefined}
            className="h-full min-w-0 flex-1 bg-transparent text-base text-ink outline-none placeholder:text-ink-3"
            onChange={(e) => { setTerm(e.target.value); setOpen(true); setActive(0); }}
            onKeyDown={(e) => {
              if (e.key === "ArrowDown") { e.preventDefault(); setActive((a) => Math.min(items.length - 1, a + 1)); }
              if (e.key === "ArrowUp") { e.preventDefault(); setActive((a) => Math.max(0, a - 1)); }
              if (e.key === "Enter" && items[active]) { e.preventDefault(); pick(items[active]); }
            }} />
        </div>
      </PopoverAnchor>
      <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-1" onOpenAutoFocus={(e) => e.preventDefault()}>
        <ul id={listId} role="listbox" aria-label="Students">
          {items.map((s, index) => (
            <li key={s.id} role="option" aria-selected={index === active}>
              <button type="button" onMouseEnter={() => setActive(index)} onClick={() => pick(s)}
                className={cn("flex w-full items-center gap-2.5 rounded-md px-2 py-1.5 text-left", index === active && "bg-sunken")}>
                <RecordAvatar name={s.fullName} fileId={s.photoFileId} size={24} />
                <span className="grid min-w-0 leading-tight">
                  <span className="truncate text-sm font-medium text-ink">{s.fullName}</span>
                  <span className="truncate text-xs text-ink-2">{s.className ?? "Not placed"} · <span className="font-mono">{s.admissionNumber}</span></span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      </PopoverContent>
    </Popover>
  );
}
