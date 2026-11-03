"use client";

import * as React from "react";
import { useWatch, type FieldErrors, type UseFormReturn } from "react-hook-form";
import { z } from "zod";
import type { Address } from "@/lib/api/org";
import { bloodGroups, formatNationalId, type Gender, type StudentDetail, type StudentDetailsInput } from "@/lib/api/students";
import { Input, NativeSelect } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Segmented } from "@/components/ui/toggles";
import { nullIfEmpty } from "@/components/forms/form-dialog";

export const provinces = ["Punjab", "Sindh", "Khyber Pakhtunkhwa", "Balochistan", "Islamabad Capital Territory", "Gilgit-Baltistan", "Azad Jammu and Kashmir"];
const religions = ["Islam", "Christianity", "Hinduism", "Sikhism", "Other"];
const languages = ["Urdu", "Punjabi", "Pashto", "Sindhi", "Saraiki", "Balochi", "English", "Other"];

const text = (max: number) => z.string().trim().max(max);

/** 13 digits however they are typed (35202-1234567-8, 3520212345678). */
export const nationalIdField = text(20).refine((v) => !v || v.replace(/\D/g, "").length === 13, "B-Form and CNIC numbers have 13 digits.");

/** A mobile number: at least 10 digits (0300 1234567, +92 300 1234567). */
export const mobileField = z.string().trim().min(1, "Enter a mobile number.").refine((v) => v.replace(/\D/g, "").length >= 10, "Enter a mobile number, e.g. 0300 1234567.");

export const studentDetailsShape = {
  firstName: text(100).min(1, "Enter the first name."),
  middleName: text(100),
  lastName: text(100).min(1, "Enter the family name."),
  gender: z.enum(["Female", "Male", "Other"]),
  dateOfBirth: z.string().min(1, "Enter the date of birth.").refine((v) => {
    const d = new Date(v);
    const now = new Date();
    return d <= new Date(now.getFullYear() - 2, now.getMonth(), now.getDate()) && d >= new Date(now.getFullYear() - 30, now.getMonth(), now.getDate());
  }, "Check the date of birth — students are 2 to 30 years old."),
  placeOfBirth: text(100),
  nationalId: nationalIdField,
  nationality: text(60),
  religion: text(60),
  bloodGroup: z.string(),
  motherTongue: text(60),
  email: z.union([z.literal(""), z.email("Enter a valid email address.")]),
  phone: text(40),
  line1: text(200),
  line2: text(200),
  city: text(100),
  province: text(100),
  previousSchoolName: text(200),
  previousSchoolClass: text(60),
  medicalAlert: text(300),
  registrationNumber: text(60),
};

export const studentDetailsSchema = z.object(studentDetailsShape);
export type StudentDetailsValues = z.infer<typeof studentDetailsSchema>;

export const emptyStudentDetails: StudentDetailsValues = {
  firstName: "", middleName: "", lastName: "", gender: "Female", dateOfBirth: "", placeOfBirth: "", nationalId: "",
  nationality: "Pakistani", religion: "Islam", bloodGroup: "", motherTongue: "", email: "", phone: "", line1: "", line2: "",
  city: "", province: "", previousSchoolName: "", previousSchoolClass: "", medicalAlert: "", registrationNumber: "",
};

export function studentDetailsFrom(s: StudentDetail): StudentDetailsValues {
  return {
    firstName: s.firstName, middleName: s.middleName ?? "", lastName: s.lastName, gender: s.gender, dateOfBirth: s.dateOfBirth,
    placeOfBirth: s.placeOfBirth ?? "", nationalId: formatNationalId(s.nationalId), nationality: s.nationality ?? "",
    religion: s.religion ?? "", bloodGroup: s.bloodGroup ?? "", motherTongue: s.motherTongue ?? "", email: s.email ?? "",
    phone: s.phone ?? "", line1: s.address.line1 ?? "", line2: s.address.line2 ?? "", city: s.address.city ?? "",
    province: s.address.province ?? "", previousSchoolName: s.previousSchoolName ?? "", previousSchoolClass: s.previousSchoolClass ?? "",
    medicalAlert: s.medicalAlert ?? "", registrationNumber: s.registrationNumber ?? "",
  };
}

export function addressFrom(v: { line1?: string; line2?: string; city?: string; province?: string }): Address {
  return { line1: nullIfEmpty(v.line1), line2: nullIfEmpty(v.line2), city: nullIfEmpty(v.city), province: nullIfEmpty(v.province), postalCode: null, countryCode: "PK" };
}

export function studentDetailsInput(v: StudentDetailsValues): StudentDetailsInput {
  return {
    firstName: v.firstName.trim(), middleName: nullIfEmpty(v.middleName), lastName: v.lastName.trim(), gender: v.gender,
    dateOfBirth: v.dateOfBirth, placeOfBirth: nullIfEmpty(v.placeOfBirth), nationalId: nullIfEmpty(v.nationalId),
    nationality: nullIfEmpty(v.nationality), religion: nullIfEmpty(v.religion), bloodGroup: nullIfEmpty(v.bloodGroup),
    motherTongue: nullIfEmpty(v.motherTongue), email: nullIfEmpty(v.email), phone: nullIfEmpty(v.phone), address: addressFrom(v),
    previousSchoolName: nullIfEmpty(v.previousSchoolName), previousSchoolClass: nullIfEmpty(v.previousSchoolClass),
    medicalAlert: nullIfEmpty(v.medicalAlert), registrationNumber: nullIfEmpty(v.registrationNumber),
  };
}

type Form = UseFormReturn<StudentDetailsValues>;
// The fields are shared by forms whose values extend StudentDetailsValues.
type Errors = FieldErrors<StudentDetailsValues>;

export function StudentIdentityFields({ form }: { form: Form }) {
  const errors: Errors = form.formState.errors;
  const gender = useWatch({ control: form.control, name: "gender" });
  return (
    <>
      <FieldRow cols={3}>
        <Field label="First name" htmlFor="sd-first" error={errors.firstName?.message}><Input autoComplete="off" {...form.register("firstName")} /></Field>
        <Field label="Middle name" htmlFor="sd-middle" optional><Input autoComplete="off" {...form.register("middleName")} /></Field>
        <Field label="Family name" htmlFor="sd-last" error={errors.lastName?.message}><Input autoComplete="off" {...form.register("lastName")} /></Field>
      </FieldRow>
      <FieldRow cols={3}>
        <Field label="Gender" htmlFor="sd-gender">
          <Segmented<Gender> aria-label="Gender" value={gender ?? "Female"} className="w-fit"
            onChange={(v) => form.setValue("gender", v, { shouldDirty: true })}
            options={[{ value: "Female", label: "Girl" }, { value: "Male", label: "Boy" }, { value: "Other", label: "Other" }]} />
        </Field>
        <Field label="Date of birth" htmlFor="sd-dob" error={errors.dateOfBirth?.message}><Input type="date" {...form.register("dateOfBirth")} /></Field>
        <Field label="B-Form number" htmlFor="sd-bform" optional error={errors.nationalId?.message} hint="13 digits, from NADRA">
          <Input inputMode="numeric" placeholder="35202-1234567-8" className="tabular" {...form.register("nationalId")} />
        </Field>
      </FieldRow>
    </>
  );
}

export function StudentBackgroundFields({ form }: { form: Form }) {
  return (
    <>
      <FieldRow cols={4}>
        <Field label="Place of birth" htmlFor="sd-pob" optional><Input {...form.register("placeOfBirth")} /></Field>
        <Field label="Nationality" htmlFor="sd-nationality" optional><Input {...form.register("nationality")} /></Field>
        <Field label="Religion" htmlFor="sd-religion" optional>
          <Input list="sd-religions" {...form.register("religion")} />
        </Field>
        <Field label="Mother tongue" htmlFor="sd-tongue" optional>
          <Input list="sd-languages" {...form.register("motherTongue")} />
        </Field>
      </FieldRow>
      <datalist id="sd-religions">{religions.map((r) => <option key={r} value={r} />)}</datalist>
      <datalist id="sd-languages">{languages.map((l) => <option key={l} value={l} />)}</datalist>
    </>
  );
}

export function StudentHealthFields({ form }: { form: Form }) {
  const errors: Errors = form.formState.errors;
  return (
    <FieldRow cols={3}>
      <Field label="Blood group" htmlFor="sd-blood" optional>
        <NativeSelect {...form.register("bloodGroup")}>
          <option value="">Not known</option>
          {bloodGroups.map((b) => <option key={b} value={b}>{b}</option>)}
        </NativeSelect>
      </Field>
      <div className="sm:col-span-2">
        <Field label="Medical alert" htmlFor="sd-alert" optional error={errors.medicalAlert?.message}
          hint="One line every teacher should see, e.g. “Asthma — inhaler in school bag”. Full medical details are kept separately.">
          <Input {...form.register("medicalAlert")} />
        </Field>
      </div>
    </FieldRow>
  );
}

export function StudentContactFields({ form }: { form: Form }) {
  const errors: Errors = form.formState.errors;
  return (
    <>
      <Field label="Home address" htmlFor="sd-line1" optional><Input placeholder="House, street" {...form.register("line1")} /></Field>
      <FieldRow cols={3}>
        <Field label="Area" htmlFor="sd-line2" optional><Input placeholder="e.g. Gulberg III" {...form.register("line2")} /></Field>
        <Field label="City" htmlFor="sd-city" optional><Input {...form.register("city")} /></Field>
        <Field label="Province" htmlFor="sd-province" optional><Input list="sd-provinces" {...form.register("province")} /></Field>
      </FieldRow>
      <datalist id="sd-provinces">{provinces.map((p) => <option key={p} value={p} />)}</datalist>
      <FieldRow>
        <Field label="Student's own mobile" htmlFor="sd-phone" optional hint="Older students only; parents are added separately.">
          <Input type="tel" {...form.register("phone")} />
        </Field>
        <Field label="Student's email" htmlFor="sd-email" optional error={errors.email?.message}><Input type="email" {...form.register("email")} /></Field>
      </FieldRow>
    </>
  );
}

export function StudentPreviousSchoolFields({ form }: { form: Form }) {
  return (
    <FieldRow cols={3}>
      <div className="sm:col-span-2">
        <Field label="Previous school" htmlFor="sd-prev" optional><Input {...form.register("previousSchoolName")} /></Field>
      </div>
      <Field label="Class completed there" htmlFor="sd-prev-class" optional><Input placeholder="e.g. Class 4" {...form.register("previousSchoolClass")} /></Field>
    </FieldRow>
  );
}

/** Small heading used to group fields inside dialogs (pages use FormSection). */
export function FieldGroupHeading({ children }: { children: React.ReactNode }) {
  return <h3 className="border-t border-line pt-4 text-sm font-semibold text-ink first:border-t-0 first:pt-0">{children}</h3>;
}

export function StudentRegistrationField({ form }: { form: Form }) {
  return (
    <Field label="Board / registration number" htmlFor="sd-reg" optional hint="e.g. BISE registration, once issued.">
      <Input {...form.register("registrationNumber")} />
    </Field>
  );
}
