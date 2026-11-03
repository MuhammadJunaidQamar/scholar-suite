"use client";

import * as React from "react";
import { useSections, type Section } from "@/lib/api/org";
import { NativeSelect } from "@/components/ui/input";

/** "27 of 30" / "Full" / "2 over" — how a section's seats stand. */
export function seatsLabel(section: Pick<Section, "enrolled" | "capacity">) {
  const left = section.capacity - section.enrolled;
  if (left > 0) return `${left} ${left === 1 ? "seat" : "seats"} left`;
  return left === 0 ? "Full" : `${-left} over capacity`;
}

/**
 * Native select of sections, grouped by class, with each section's campus and seats. Sections are those of the
 * current academic year unless another year is given.
 */
export function SectionSelect({
  id,
  value,
  onChange,
  academicYearId,
  gradeId,
  campusId,
  excludeId,
  placeholder = "Choose a section",
  disabled,
  invalid,
}: {
  id?: string;
  value: string;
  onChange: (sectionId: string, section: Section | undefined) => void;
  academicYearId?: string;
  gradeId?: string;
  campusId?: string;
  excludeId?: string | null;
  placeholder?: string;
  disabled?: boolean;
  invalid?: boolean;
}) {
  const sections = useSections({ academicYearId, gradeId, campusId });
  const options = (sections.data ?? []).filter((s) => s.isActive && s.id !== excludeId);
  const campuses = new Set(options.map((s) => s.campusId));
  const groups = Object.values(
    options.reduce<Record<string, { name: string; order: number; items: Section[] }>>((acc, s) => {
      (acc[s.gradeId] ??= { name: s.gradeName, order: s.gradeOrder, items: [] }).items.push(s);
      return acc;
    }, {}),
  ).sort((a, b) => a.order - b.order);

  return (
    <NativeSelect id={id} value={value} disabled={disabled || sections.isPending} aria-invalid={invalid || undefined}
      onChange={(e) => onChange(e.target.value, options.find((s) => s.id === e.target.value))}>
      <option value="">{sections.isPending ? "Loading sections…" : options.length === 0 ? "No sections available" : placeholder}</option>
      {groups.map((group) => (
        <optgroup key={group.name} label={group.name}>
          {group.items.map((s) => (
            <option key={s.id} value={s.id}>
              {s.displayName}
              {campuses.size > 1 ? ` · ${s.campusName}` : ""} · {seatsLabel(s)}
            </option>
          ))}
        </optgroup>
      ))}
    </NativeSelect>
  );
}
