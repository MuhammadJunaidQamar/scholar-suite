"use client";

import * as React from "react";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Plus, Trash2 } from "lucide-react";
import {
  relationships,
  useAddGuardianLink,
  useSetEmergencyContacts,
  useUpdateGuardianLink,
  type EmergencyContact,
  type GuardianLink,
  type GuardianPermissions,
  type GuardianRelationship,
} from "@/lib/api/students";
import { Button } from "@/components/ui/button";
import { Input, NativeSelect } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Checkbox, Switch } from "@/components/ui/toggles";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";
import { GuardianMatches, emptyGuardianEntry, guardianEntrySchema, guardianLinkInput, type GuardianEntry } from "./guardian-entry";
import { mobileField } from "./student-form";

/** The person-level fields of a guardian entry, with the existing-parent lookup. Shared by the new-student page. */
export function GuardianEntryFields({ value, onChange, errors, idPrefix }: {
  value: GuardianEntry;
  onChange: (patch: Partial<GuardianEntry>) => void;
  errors?: Partial<Record<keyof GuardianEntry, { message?: string }>>;
  idPrefix: string;
}) {
  const linked = !!value.guardianId;
  return (
    <div className="grid gap-4">
      <FieldRow>
        <Field label="Mobile number" htmlFor={`${idPrefix}-phone`} error={errors?.phone?.message} hint="We check for parents already on record.">
          <Input type="tel" inputMode="tel" placeholder="0300 1234567" value={value.phone}
            onChange={(e) => onChange({ phone: e.target.value, guardianId: "" })} />
        </Field>
        <Field label="Relationship" htmlFor={`${idPrefix}-rel`}>
          <NativeSelect value={value.relationship} onChange={(e) => onChange({ relationship: e.target.value as GuardianRelationship })}>
            {relationships.map((r) => <option key={r} value={r}>{r}</option>)}
          </NativeSelect>
        </Field>
      </FieldRow>
      <GuardianMatches phone={value.phone} selectedId={value.guardianId}
        onSelect={(match) => onChange(match ? { guardianId: match.id } : { guardianId: "" })} />
      {!linked && (
        <>
          <FieldRow>
            <Field label="First name" htmlFor={`${idPrefix}-first`} error={errors?.firstName?.message}>
              <Input value={value.firstName} onChange={(e) => onChange({ firstName: e.target.value })} />
            </Field>
            <Field label="Family name" htmlFor={`${idPrefix}-last`} optional>
              <Input value={value.lastName} onChange={(e) => onChange({ lastName: e.target.value })} />
            </Field>
          </FieldRow>
          <FieldRow cols={3}>
            <Field label="CNIC" htmlFor={`${idPrefix}-cnic`} optional error={errors?.nationalId?.message}>
              <Input inputMode="numeric" placeholder="35202-1234567-8" className="tabular" value={value.nationalId}
                onChange={(e) => onChange({ nationalId: e.target.value })} />
            </Field>
            <Field label="Email" htmlFor={`${idPrefix}-email`} optional error={errors?.email?.message}>
              <Input type="email" value={value.email} onChange={(e) => onChange({ email: e.target.value })} />
            </Field>
            <Field label="Occupation" htmlFor={`${idPrefix}-job`} optional>
              <Input value={value.occupation} onChange={(e) => onChange({ occupation: e.target.value })} />
            </Field>
          </FieldRow>
        </>
      )}
    </div>
  );
}

const roleFlags: { key: keyof Pick<GuardianEntry, "isFeePayer" | "isEmergencyContact" | "canPickUp" | "livesWithStudent">; label: string }[] = [
  { key: "isFeePayer", label: "Pays the fees" },
  { key: "isEmergencyContact", label: "Emergency contact" },
  { key: "canPickUp", label: "May collect the child" },
  { key: "livesWithStudent", label: "Lives with the child" },
];

export function GuardianRoleFlags({ value, onChange }: { value: GuardianEntry; onChange: (patch: Partial<GuardianEntry>) => void }) {
  return (
    <div className="flex flex-wrap gap-x-5 gap-y-2">
      {roleFlags.map((flag) => (
        <label key={flag.key} className="flex items-center gap-2 text-sm text-ink">
          <Checkbox checked={value[flag.key]} onCheckedChange={(v) => onChange({ [flag.key]: v === true })} />
          {flag.label}
        </label>
      ))}
    </div>
  );
}

// ───────────────────────────── Add guardian ─────────────────────────────

const addSchema = z.object({ entry: guardianEntrySchema });

export function AddGuardianDialog({ studentId, familyName, hasPrimary, open, onOpenChange }: {
  studentId: string;
  familyName: string;
  hasPrimary: boolean;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const add = useAddGuardianLink(studentId);
  const form = useForm<{ entry: GuardianEntry }>({ resolver: zodResolver(addSchema) });
  const entry = useWatch({ control: form.control, name: "entry" });
  const errors = form.formState.errors.entry;
  const patch = (p: Partial<GuardianEntry>) => form.setValue("entry", { ...form.getValues("entry"), ...p }, { shouldValidate: form.formState.isSubmitted });

  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="lg"
      defaults={{ entry: emptyGuardianEntry(hasPrimary ? "Mother" : "Father", !hasPrimary, familyName) }}
      title="Add a parent or guardian" submitLabel="Add" successMessage="Guardian added"
      onSubmit={(v) => add.mutateAsync(guardianLinkInput(v.entry))}>
      {entry && (
        <>
          <GuardianEntryFields idPrefix="ag" value={entry} onChange={patch} errors={errors as never} />
          <div className="grid gap-3 border-t border-line pt-4">
            <label className="flex items-center gap-2 text-sm text-ink">
              <Checkbox checked={entry.isPrimary} onCheckedChange={(v) => patch({ isPrimary: v === true })} />
              Primary contact {hasPrimary && <span className="text-ink-2">— replaces the current primary contact</span>}
            </label>
            <GuardianRoleFlags value={entry} onChange={patch} />
          </div>
        </>
      )}
    </FormDialog>
  );
}

// ───────────────────────────── Edit link ─────────────────────────────

type LinkValues = GuardianPermissions & { relationship: GuardianRelationship };

const linkSwitches: { key: keyof GuardianPermissions; label: string; description: string; portal?: boolean }[] = [
  { key: "isPrimary", label: "Primary contact", description: "Called first and shown on the student's record." },
  { key: "isFeePayer", label: "Pays the fees", description: "Challans and receipts are addressed to this person." },
  { key: "isEmergencyContact", label: "Emergency contact", description: "Called if the child is unwell or hurt." },
  { key: "canPickUp", label: "May collect the child", description: "Gate staff can release the child to this person." },
  { key: "livesWithStudent", label: "Lives with the child", description: "Used for transport and correspondence." },
  { key: "portalAccess", label: "Family portal", description: "Can see this child in the family portal.", portal: true },
  { key: "canViewAttendance", label: "Attendance", description: "Daily attendance and leave.", portal: true },
  { key: "canViewResults", label: "Results", description: "Marks and report cards.", portal: true },
  { key: "canViewFees", label: "Fees", description: "Challans, payments and balances.", portal: true },
  { key: "receivesNotifications", label: "Notifications", description: "Absence alerts, notices and reminders.", portal: true },
];

export function EditGuardianLinkDialog({ studentId, link, open, onOpenChange }: {
  studentId: string;
  link: GuardianLink | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const update = useUpdateGuardianLink(studentId);
  const form = useForm<LinkValues>();
  const values = useWatch({ control: form.control });
  const row = (s: (typeof linkSwitches)[number], disabled = false) => (
    <label key={s.key} className="flex items-center justify-between gap-4 py-2.5">
      <span className="grid gap-0.5">
        <span className="text-sm font-medium text-ink">{s.label}</span>
        <span className="text-xs text-ink-2">{s.description}</span>
      </span>
      <Switch checked={!!values[s.key]} disabled={disabled} onCheckedChange={(v) => form.setValue(s.key, v, { shouldDirty: true })} />
    </label>
  );
  return (
    <FormDialog open={open && !!link} onOpenChange={onOpenChange} form={form} size="md"
      defaults={link ? {
        relationship: link.relationship, isPrimary: link.isPrimary, isFeePayer: link.isFeePayer, isEmergencyContact: link.isEmergencyContact,
        livesWithStudent: link.livesWithStudent, canPickUp: link.canPickUp, portalAccess: link.portalAccess, canViewAttendance: link.canViewAttendance,
        canViewResults: link.canViewResults, canViewFees: link.canViewFees, receivesNotifications: link.receivesNotifications,
      } : ({} as LinkValues)}
      title={link?.fullName ?? ""} description="What this person is to the child, and what they may see and do."
      submitLabel="Save" successMessage="Saved"
      onSubmit={(v) => update.mutateAsync({ linkId: link!.linkId, body: v })}>
      <Field label="Relationship" htmlFor="gl-rel">
        <NativeSelect {...form.register("relationship")}>{relationships.map((r) => <option key={r} value={r}>{r}</option>)}</NativeSelect>
      </Field>
      <div className="divide-y divide-line">{linkSwitches.filter((s) => !s.portal).map((s) => row(s))}</div>
      <div className="grid">
        <p className="pb-1 text-xs font-medium text-ink-3">Family portal</p>
        <div className="divide-y divide-line">
          {linkSwitches.filter((s) => s.portal).map((s) => row(s, s.key !== "portalAccess" && !values.portalAccess))}
        </div>
      </div>
    </FormDialog>
  );
}

// ───────────────────────────── Emergency contacts ─────────────────────────────

const contactsSchema = z.object({
  contacts: z.array(z.object({
    name: z.string().trim().min(1, "Enter a name.").max(150),
    relationship: z.string().trim().min(1, "Say who they are.").max(60),
    phone: mobileField,
    alternatePhone: z.string().trim().max(40),
  })).max(5),
});
type ContactsValues = z.infer<typeof contactsSchema>;

export function EmergencyContactsDialog({ studentId, contacts, open, onOpenChange }: {
  studentId: string;
  contacts: EmergencyContact[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const save = useSetEmergencyContacts(studentId);
  const form = useForm<ContactsValues>({ resolver: zodResolver(contactsSchema) });
  const list = useFieldArray({ control: form.control, name: "contacts" });
  const errors = form.formState.errors.contacts;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="lg"
      defaults={{ contacts: contacts.map((c) => ({ name: c.name, relationship: c.relationship, phone: c.phone, alternatePhone: c.alternatePhone ?? "" })) }}
      title="Emergency contacts" description="People other than the parents to call when no parent can be reached, in the order to try them."
      submitLabel="Save contacts" successMessage="Emergency contacts saved"
      onSubmit={(v) => save.mutateAsync(v.contacts.map((c) => ({ ...c, alternatePhone: nullIfEmpty(c.alternatePhone) })))}>
      {list.fields.length === 0 && <p className="text-sm text-ink-2">No emergency contacts besides the parents.</p>}
      {list.fields.map((field, index) => (
        <div key={field.id} className="grid gap-3 border-b border-line pb-4 sm:grid-cols-[1.3fr_1fr_1fr_auto] sm:items-start">
          <Field label={index === 0 ? "Name" : undefined} htmlFor={`ec-name-${index}`} error={errors?.[index]?.name?.message}>
            <Input aria-label="Name" {...form.register(`contacts.${index}.name`)} />
          </Field>
          <Field label={index === 0 ? "Who they are" : undefined} htmlFor={`ec-rel-${index}`} error={errors?.[index]?.relationship?.message}>
            <Input aria-label="Relationship" placeholder="e.g. Uncle" {...form.register(`contacts.${index}.relationship`)} />
          </Field>
          <Field label={index === 0 ? "Mobile" : undefined} htmlFor={`ec-phone-${index}`} error={errors?.[index]?.phone?.message}>
            <Input aria-label="Mobile" type="tel" {...form.register(`contacts.${index}.phone`)} />
          </Field>
          <Button type="button" variant="ghost" size="icon" aria-label="Remove contact" className={index === 0 ? "sm:mt-6" : undefined}
            onClick={() => list.remove(index)}>
            <Trash2 />
          </Button>
        </div>
      ))}
      {list.fields.length < 5 && (
        <Button type="button" variant="secondary" className="w-fit" onClick={() => list.append({ name: "", relationship: "", phone: "", alternatePhone: "" })}>
          <Plus /> Add contact
        </Button>
      )}
    </FormDialog>
  );
}
