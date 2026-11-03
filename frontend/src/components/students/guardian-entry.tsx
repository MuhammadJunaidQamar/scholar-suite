"use client";

import * as React from "react";
import { Check, UsersRound } from "lucide-react";
import { z } from "zod";
import { cn } from "@/lib/utils";
import {
  formatPhone,
  relationships,
  useGuardianLookup,
  type GuardianLinkInput,
  type GuardianMatch,
  type GuardianRelationship,
} from "@/lib/api/students";
import { nullIfEmpty } from "@/components/forms/form-dialog";
import { mobileField, nationalIdField } from "./student-form";

const text = (max: number) => z.string().trim().max(max);

export const guardianEntrySchema = z
  .object({
    phone: mobileField,
    /** Set when an existing guardian (same mobile number) is reused. */
    guardianId: z.string(),
    firstName: text(100),
    lastName: text(100),
    relationship: z.enum(relationships as [GuardianRelationship, ...GuardianRelationship[]]),
    email: z.union([z.literal(""), z.email("Enter a valid email address.")]),
    nationalId: nationalIdField,
    occupation: text(100),
    isPrimary: z.boolean(),
    isFeePayer: z.boolean(),
    isEmergencyContact: z.boolean(),
    livesWithStudent: z.boolean(),
    canPickUp: z.boolean(),
  })
  .superRefine((v, ctx) => {
    if (!v.guardianId && !v.firstName) ctx.addIssue({ code: "custom", path: ["firstName"], message: "Enter the first name." });
  });

export type GuardianEntry = z.infer<typeof guardianEntrySchema>;

export function emptyGuardianEntry(relationship: GuardianRelationship, primary: boolean, lastName = ""): GuardianEntry {
  return {
    phone: "", guardianId: "", firstName: "", lastName, relationship, email: "", nationalId: "", occupation: "",
    isPrimary: primary, isFeePayer: primary, isEmergencyContact: true, livesWithStudent: true, canPickUp: true,
  };
}

export function guardianLinkInput(entry: GuardianEntry): GuardianLinkInput {
  return {
    guardianId: entry.guardianId || null,
    newGuardian: entry.guardianId
      ? null
      : {
          firstName: entry.firstName, lastName: entry.lastName, phone: entry.phone, email: nullIfEmpty(entry.email),
          nationalId: nullIfEmpty(entry.nationalId), occupation: nullIfEmpty(entry.occupation), employer: null, alternatePhone: null, address: null,
        },
    relationship: entry.relationship,
    isPrimary: entry.isPrimary,
    isFeePayer: entry.isFeePayer,
    isEmergencyContact: entry.isEmergencyContact,
    livesWithStudent: entry.livesWithStudent,
    canPickUp: entry.canPickUp,
    portalAccess: true,
    canViewAttendance: true,
    canViewResults: true,
    canViewFees: entry.isFeePayer,
    receivesNotifications: true,
  };
}

/**
 * Parents already on record with the typed mobile number. Choosing one links the existing record (siblings share
 * guardians, so the family portal and sibling discounts see one family).
 */
export function GuardianMatches({ phone, selectedId, onSelect }: {
  phone: string;
  selectedId: string;
  onSelect: (match: GuardianMatch | null) => void;
}) {
  const lookup = useGuardianLookup(phone);
  const matches = lookup.data ?? [];
  if (matches.length === 0) return null;
  return (
    <div className="grid gap-2 rounded-md border border-line bg-sunken/50 p-3">
      <p className="flex items-center gap-2 text-sm font-medium text-ink">
        <UsersRound className="size-4 text-ink-3" aria-hidden />
        {matches.length === 1 ? "A parent with this number is already on record" : "Parents with this number are already on record"}
      </p>
      <div className="grid gap-1.5">
        {matches.map((m) => {
          const selected = m.id === selectedId;
          return (
            <button key={m.id} type="button" aria-pressed={selected} onClick={() => onSelect(selected ? null : m)}
              className={cn(
                "flex items-center gap-3 rounded-md border bg-surface px-3 py-2 text-left transition-colors",
                selected ? "border-accent ring-1 ring-accent" : "border-line hover:border-line-strong",
              )}>
              <span className={cn("grid size-4 shrink-0 place-items-center rounded-full border", selected ? "border-accent bg-accent text-white" : "border-line-strong")}>
                {selected && <Check className="size-3" strokeWidth={3} />}
              </span>
              <span className="grid min-w-0 gap-0.5">
                <span className="text-sm font-medium text-ink">{m.fullName} <span className="font-normal text-ink-2">· {formatPhone(m.phone)}</span></span>
                <span className="truncate text-xs text-ink-2">
                  {m.children.length > 0 ? `Parent of ${m.children.join(", ")}` : "No children linked yet"}
                </span>
              </span>
              <span className="ml-auto shrink-0 text-xs font-medium text-accent">{selected ? "Linked" : "Use this person"}</span>
            </button>
          );
        })}
      </div>
      {!selectedId && <p className="text-xs text-ink-2">Or carry on below to add a different person with the same number.</p>}
    </div>
  );
}
