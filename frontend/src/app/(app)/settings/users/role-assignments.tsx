"use client";

import { Plus, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { NativeSelect } from "@/components/ui/input";
import { useCampuses, useRoles } from "@/lib/api/access";

export type AssignmentDraft = { roleId: string; campusId: string | null };

/** Rows of "role at campus". Campus-limited roles only see that campus's data. */
export function RoleAssignmentsEditor({ value, onChange, invalid }: {
  value: AssignmentDraft[];
  onChange: (value: AssignmentDraft[]) => void;
  invalid?: boolean;
}) {
  const roles = useRoles();
  const campuses = useCampuses();
  const multiCampus = (campuses.data?.length ?? 0) > 1;
  const portals = ["Admin", "Teacher", "Family", "Student"] as const;
  const portalNames = { Admin: "Administration", Teacher: "Teaching", Family: "Families", Student: "Students" } as const;

  const update = (index: number, patch: Partial<AssignmentDraft>) =>
    onChange(value.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  return (
    <div className="grid gap-2">
      {value.map((row, index) => (
        <div key={index} className="flex items-center gap-2">
          <NativeSelect
            aria-label="Role"
            aria-invalid={invalid && !row.roleId}
            className="flex-1"
            value={row.roleId}
            onChange={(e) => update(index, { roleId: e.target.value })}
          >
            <option value="">Choose a role…</option>
            {portals.map((portal) => {
              const inPortal = roles.data?.filter((r) => r.portal === portal) ?? [];
              if (inPortal.length === 0) return null;
              return (
                <optgroup key={portal} label={portalNames[portal]}>
                  {inPortal.map((role) => (
                    <option key={role.id} value={role.id}>
                      {role.name}
                    </option>
                  ))}
                </optgroup>
              );
            })}
          </NativeSelect>
          {multiCampus && (
            <NativeSelect
              aria-label="Campus"
              className="w-44"
              value={row.campusId ?? ""}
              onChange={(e) => update(index, { campusId: e.target.value || null })}
            >
              <option value="">All campuses</option>
              {campuses.data?.map((campus) => (
                <option key={campus.id} value={campus.id}>
                  {campus.name}
                </option>
              ))}
            </NativeSelect>
          )}
          <Button type="button" variant="ghost" size="icon-sm" aria-label="Remove role" disabled={value.length === 1}
            onClick={() => onChange(value.filter((_, i) => i !== index))}>
            <X />
          </Button>
        </div>
      ))}
      <div>
        <Button type="button" variant="ghost" size="sm" onClick={() => onChange([...value, { roleId: "", campusId: null }])}>
          <Plus /> Add another role
        </Button>
      </div>
    </div>
  );
}
