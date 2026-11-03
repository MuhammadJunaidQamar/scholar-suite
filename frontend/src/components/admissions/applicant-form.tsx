"use client";

import * as React from "react";
import { useWatch, type FieldErrors, type UseFormReturn } from "react-hook-form";
import { z } from "zod";
import type { ApplicantInput, ApplicationDetail } from "@/lib/api/admissions";
import { formatNationalId, relationships, type Gender, type GuardianRelationship } from "@/lib/api/students";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Segmented } from "@/components/ui/toggles";
import { nullIfEmpty } from "@/components/forms/form-dialog";
import { addressFrom, mobileField, nationalIdField, provinces } from "@/components/students/student-form";

const text = (max: number) => z.string().trim().max(max);

export const applicantShape = {
  firstName: text(100).min(1, "Enter the child's first name."),
  middleName: text(100),
  lastName: text(100).min(1, "Enter the family name."),
  gender: z.enum(["Female", "Male", "Other"]),
  dateOfBirth: z.string().min(1, "Enter the date of birth.").refine((v) => {
    const d = new Date(v);
    const now = new Date();
    return d <= new Date(now.getFullYear() - 2, now.getMonth(), now.getDate()) && d >= new Date(now.getFullYear() - 25, now.getMonth(), now.getDate());
  }, "Check the date of birth."),
  nationalId: nationalIdField,
  previousSchoolName: text(200),
  previousSchoolClass: text(60),
  previousResult: text(60),
  line1: text(200),
  line2: text(200),
  city: text(100),
  province: text(100),
  guardianFirstName: text(100).min(1, "Enter the parent's first name."),
  guardianLastName: text(100),
  guardianRelationship: z.enum(relationships as [GuardianRelationship, ...GuardianRelationship[]]),
  guardianPhone: mobileField,
  guardianEmail: z.union([z.literal(""), z.email("Enter a valid email address.")]),
  guardianNationalId: nationalIdField,
  guardianOccupation: text(100),
  notes: text(2000),
};

export const applicantSchema = z.object(applicantShape);
export type ApplicantValues = z.infer<typeof applicantSchema>;

export const emptyApplicant: ApplicantValues = {
  firstName: "", middleName: "", lastName: "", gender: "Female", dateOfBirth: "", nationalId: "", previousSchoolName: "",
  previousSchoolClass: "", previousResult: "", line1: "", line2: "", city: "", province: "", guardianFirstName: "",
  guardianLastName: "", guardianRelationship: "Father", guardianPhone: "", guardianEmail: "", guardianNationalId: "",
  guardianOccupation: "", notes: "",
};

export function applicantFrom(a: ApplicationDetail): ApplicantValues {
  return {
    firstName: a.firstName, middleName: a.middleName ?? "", lastName: a.lastName, gender: a.gender, dateOfBirth: a.dateOfBirth,
    nationalId: formatNationalId(a.nationalId), previousSchoolName: a.previousSchoolName ?? "", previousSchoolClass: a.previousSchoolClass ?? "",
    previousResult: a.previousResult ?? "", line1: a.address.line1 ?? "", line2: a.address.line2 ?? "", city: a.address.city ?? "",
    province: a.address.province ?? "", guardianFirstName: a.guardianFirstName, guardianLastName: a.guardianLastName,
    guardianRelationship: a.guardianRelationship, guardianPhone: a.guardianPhone, guardianEmail: a.guardianEmail ?? "",
    guardianNationalId: formatNationalId(a.guardianNationalId), guardianOccupation: a.guardianOccupation ?? "", notes: a.notes ?? "",
  };
}

export function applicantInput(v: ApplicantValues): ApplicantInput {
  return {
    firstName: v.firstName, middleName: nullIfEmpty(v.middleName), lastName: v.lastName, gender: v.gender, dateOfBirth: v.dateOfBirth,
    nationalId: nullIfEmpty(v.nationalId), previousSchoolName: nullIfEmpty(v.previousSchoolName), previousSchoolClass: nullIfEmpty(v.previousSchoolClass),
    previousResult: nullIfEmpty(v.previousResult), address: addressFrom(v), guardianFirstName: v.guardianFirstName, guardianLastName: v.guardianLastName,
    guardianRelationship: v.guardianRelationship, guardianPhone: v.guardianPhone, guardianEmail: nullIfEmpty(v.guardianEmail),
    guardianNationalId: nullIfEmpty(v.guardianNationalId), guardianOccupation: nullIfEmpty(v.guardianOccupation), notes: nullIfEmpty(v.notes),
  };
}

type Form = UseFormReturn<ApplicantValues>;

export function ApplicantChildFields({ form, idPrefix = "ap" }: { form: Form; idPrefix?: string }) {
  const errors: FieldErrors<ApplicantValues> = form.formState.errors;
  const gender = useWatch({ control: form.control, name: "gender" });
  return (
    <>
      <FieldRow cols={3}>
        <Field label="First name" htmlFor={`${idPrefix}-first`} error={errors.firstName?.message}><Input autoComplete="off" {...form.register("firstName")} /></Field>
        <Field label="Middle name" htmlFor={`${idPrefix}-middle`} optional><Input autoComplete="off" {...form.register("middleName")} /></Field>
        <Field label="Family name" htmlFor={`${idPrefix}-last`} error={errors.lastName?.message}><Input autoComplete="off" {...form.register("lastName")} /></Field>
      </FieldRow>
      <FieldRow cols={3}>
        <Field label="Gender" htmlFor={`${idPrefix}-gender`}>
          <Segmented<Gender> aria-label="Gender" value={gender ?? "Female"} className="w-fit" onChange={(v) => form.setValue("gender", v, { shouldDirty: true })}
            options={[{ value: "Female", label: "Girl" }, { value: "Male", label: "Boy" }, { value: "Other", label: "Other" }]} />
        </Field>
        <Field label="Date of birth" htmlFor={`${idPrefix}-dob`} error={errors.dateOfBirth?.message}><Input type="date" {...form.register("dateOfBirth")} /></Field>
        <Field label="B-Form number" htmlFor={`${idPrefix}-bform`} optional error={errors.nationalId?.message}>
          <Input inputMode="numeric" placeholder="35202-1234567-8" className="tabular" {...form.register("nationalId")} />
        </Field>
      </FieldRow>
    </>
  );
}

export function ApplicantSchoolingFields({ form, idPrefix = "ap" }: { form: Form; idPrefix?: string }) {
  return (
    <FieldRow cols={3}>
      <Field label="Current or last school" htmlFor={`${idPrefix}-prev`} optional><Input {...form.register("previousSchoolName")} /></Field>
      <Field label="Class there" htmlFor={`${idPrefix}-prev-class`} optional><Input placeholder="e.g. Class 4" {...form.register("previousSchoolClass")} /></Field>
      <Field label="Last result" htmlFor={`${idPrefix}-prev-result`} optional><Input placeholder="e.g. 82% or A grade" {...form.register("previousResult")} /></Field>
    </FieldRow>
  );
}

export function ApplicantParentFields({ form, idPrefix = "ap" }: { form: Form; idPrefix?: string }) {
  const errors: FieldErrors<ApplicantValues> = form.formState.errors;
  return (
    <>
      <FieldRow cols={3}>
        <Field label="Parent's first name" htmlFor={`${idPrefix}-gfirst`} error={errors.guardianFirstName?.message}><Input {...form.register("guardianFirstName")} /></Field>
        <Field label="Family name" htmlFor={`${idPrefix}-glast`} optional><Input {...form.register("guardianLastName")} /></Field>
        <Field label="Relationship" htmlFor={`${idPrefix}-grel`}>
          <NativeSelect {...form.register("guardianRelationship")}>{relationships.map((r) => <option key={r} value={r}>{r}</option>)}</NativeSelect>
        </Field>
      </FieldRow>
      <FieldRow cols={3}>
        <Field label="Mobile number" htmlFor={`${idPrefix}-gphone`} error={errors.guardianPhone?.message}>
          <Input type="tel" inputMode="tel" placeholder="0300 1234567" {...form.register("guardianPhone")} />
        </Field>
        <Field label="Email" htmlFor={`${idPrefix}-gemail`} optional error={errors.guardianEmail?.message}><Input type="email" {...form.register("guardianEmail")} /></Field>
        <Field label="CNIC" htmlFor={`${idPrefix}-gcnic`} optional error={errors.guardianNationalId?.message}>
          <Input inputMode="numeric" placeholder="35202-1234567-8" className="tabular" {...form.register("guardianNationalId")} />
        </Field>
      </FieldRow>
      <Field label="Occupation" htmlFor={`${idPrefix}-gjob`} optional><Input {...form.register("guardianOccupation")} /></Field>
    </>
  );
}

export function ApplicantAddressFields({ form, idPrefix = "ap" }: { form: Form; idPrefix?: string }) {
  return (
    <>
      <Field label="Home address" htmlFor={`${idPrefix}-line1`} optional><Input placeholder="House, street" {...form.register("line1")} /></Field>
      <FieldRow cols={3}>
        <Field label="Area" htmlFor={`${idPrefix}-line2`} optional><Input placeholder="e.g. Johar Town" {...form.register("line2")} /></Field>
        <Field label="City" htmlFor={`${idPrefix}-city`} optional><Input {...form.register("city")} /></Field>
        <Field label="Province" htmlFor={`${idPrefix}-province`} optional><Input list={`${idPrefix}-provinces`} {...form.register("province")} /></Field>
      </FieldRow>
      <datalist id={`${idPrefix}-provinces`}>{provinces.map((p) => <option key={p} value={p} />)}</datalist>
    </>
  );
}

export function ApplicantNotesField({ form, idPrefix = "ap", label = "Anything else we should know" }: { form: Form; idPrefix?: string; label?: string }) {
  return (
    <Field label={label} htmlFor={`${idPrefix}-notes`} optional>
      <Textarea rows={3} {...form.register("notes")} />
    </Field>
  );
}

/** Shows a compact list of the required documents a family should bring. */
export function RequiredDocuments({ documents }: { documents: string[] }) {
  if (documents.length === 0) return null;
  return (
    <ul className="grid gap-1.5 text-sm text-ink">
      {documents.map((d) => (
        <li key={d} className="flex gap-2"><span aria-hidden className="text-ink-3">—</span>{d}</li>
      ))}
    </ul>
  );
}

export function useApplicantFormAdapter<T extends ApplicantValues>(form: UseFormReturn<T>) {
  // The shared field groups are typed for the applicant alone; forms that add placement fields reuse them as-is.
  return React.useMemo(() => form as unknown as UseFormReturn<ApplicantValues>, [form]);
}
