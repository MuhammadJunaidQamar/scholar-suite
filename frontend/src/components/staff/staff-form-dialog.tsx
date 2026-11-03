"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useCampuses } from "@/lib/api/access";
import { useDepartments, useDesignations } from "@/lib/api/org";
import { formatNationalId } from "@/lib/api/students";
import { employmentTypeLabel, staffTypeLabel, useSaveStaff, type EmploymentType, type StaffDetail, type StaffType } from "@/lib/api/staff";
import { todayIso } from "@/lib/format";
import { Field, FieldRow } from "@/components/ui/field";
import { Input, NativeSelect } from "@/components/ui/input";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";
import { FieldGroupHeading, nationalIdField, provinces } from "@/components/students/student-form";

const text = (max: number) => z.string().trim().max(max);
const staffTypes = Object.keys(staffTypeLabel) as StaffType[];
const employmentTypes = Object.keys(employmentTypeLabel) as EmploymentType[];

const schema = z.object({
  firstName: text(100).min(1, "Enter the first name."),
  lastName: text(100),
  gender: z.enum(["Female", "Male", "Other"]),
  dateOfBirth: z.string(),
  nationalId: nationalIdField,
  phone: z.string().trim().refine((v) => !v || v.replace(/\D/g, "").length >= 10, "Enter a mobile number, e.g. 0300 1234567."),
  email: z.union([z.literal(""), z.email("Enter a valid email address.")]),
  campusId: z.string().min(1, "Choose the campus."),
  departmentId: z.string(),
  designationId: z.string(),
  staffType: z.enum(staffTypes as [StaffType, ...StaffType[]]),
  employmentType: z.enum(employmentTypes as [EmploymentType, ...EmploymentType[]]),
  joinDate: z.string().min(1, "Enter the joining date."),
  employeeNumber: text(40),
  line1: text(200),
  city: text(100),
  province: text(100),
});
type Values = z.infer<typeof schema>;

function valuesFrom(s: StaffDetail | null, campusId: string): Values {
  return {
    firstName: s?.firstName ?? "", lastName: s?.lastName ?? "", gender: s?.gender ?? "Female", dateOfBirth: s?.dateOfBirth ?? "",
    nationalId: formatNationalId(s?.nationalId), phone: s?.phone ?? "", email: s?.email ?? "", campusId: s?.campusId ?? campusId,
    departmentId: s?.departmentId ?? "", designationId: s?.designationId ?? "", staffType: s?.staffType ?? "Teaching",
    employmentType: s?.employmentType ?? "Permanent", joinDate: s?.joinDate ?? todayIso(), employeeNumber: "",
    line1: s?.address.line1 ?? "", city: s?.address.city ?? "", province: s?.address.province ?? "Punjab",
  };
}

/** Add a staff member, or edit one. The employee number comes from the school's numbering unless one is typed. */
export function StaffFormDialog({ staff, open, onOpenChange, onSaved }: {
  staff: StaffDetail | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved?: (saved: StaffDetail) => void;
}) {
  const save = useSaveStaff();
  const campuses = useCampuses();
  const departments = useDepartments();
  const designations = useDesignations();
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  const active = (campuses.data ?? []).filter((c) => c.isActive);
  const main = active.find((c) => c.isMain)?.id ?? active[0]?.id ?? "";
  const e = form.formState.errors;

  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} defaults={valuesFrom(staff, main)} size="lg"
      title={staff ? `Edit ${staff.fullName}` : "Add a staff member"}
      description={staff ? undefined : "Teachers, office staff and support staff. Sign-in access is given separately from their profile."}
      submitLabel={staff ? "Save changes" : "Add staff member"} successMessage={staff ? "Staff record updated" : "Staff member added"}
      onSubmit={async (v) => {
        const saved = await save.mutateAsync({
          id: staff?.id,
          body: {
            firstName: v.firstName, lastName: v.lastName, gender: v.gender, dateOfBirth: nullIfEmpty(v.dateOfBirth), nationalId: nullIfEmpty(v.nationalId),
            phone: nullIfEmpty(v.phone), email: nullIfEmpty(v.email), campusId: v.campusId, departmentId: nullIfEmpty(v.departmentId),
            designationId: nullIfEmpty(v.designationId), staffType: v.staffType, employmentType: v.employmentType, joinDate: v.joinDate,
            employeeNumber: staff ? undefined : nullIfEmpty(v.employeeNumber), version: staff?.version,
            address: { line1: nullIfEmpty(v.line1), line2: null, city: nullIfEmpty(v.city), province: nullIfEmpty(v.province), postalCode: null, countryCode: "PK" },
          },
        });
        onSaved?.(saved);
      }}>
      <FieldGroupHeading>Person</FieldGroupHeading>
      <FieldRow cols={3}>
        <Field label="First name" htmlFor="sf-first" error={e.firstName?.message}><Input id="sf-first" autoFocus {...form.register("firstName")} /></Field>
        <Field label="Last name" htmlFor="sf-last" error={e.lastName?.message}><Input id="sf-last" {...form.register("lastName")} /></Field>
        <Field label="Gender" htmlFor="sf-gender">
          <NativeSelect id="sf-gender" {...form.register("gender")}>
            <option value="Female">Female</option><option value="Male">Male</option><option value="Other">Other</option>
          </NativeSelect>
        </Field>
      </FieldRow>
      <FieldRow cols={3}>
        <Field label="Mobile" htmlFor="sf-phone" optional error={e.phone?.message}><Input id="sf-phone" inputMode="tel" placeholder="0300 1234567" {...form.register("phone")} /></Field>
        <Field label="Email" htmlFor="sf-email" optional error={e.email?.message} hint="Becomes their sign-in name.">
          <Input id="sf-email" type="email" autoComplete="off" {...form.register("email")} />
        </Field>
        <Field label="CNIC" htmlFor="sf-cnic" optional error={e.nationalId?.message}><Input id="sf-cnic" placeholder="35202-1234567-8" {...form.register("nationalId")} /></Field>
      </FieldRow>
      <FieldRow cols={3}>
        <Field label="Date of birth" htmlFor="sf-dob" optional><Input id="sf-dob" type="date" {...form.register("dateOfBirth")} /></Field>
        <Field label="Address" htmlFor="sf-line1" optional className="sm:col-span-2"><Input id="sf-line1" {...form.register("line1")} /></Field>
      </FieldRow>
      <FieldRow cols={2}>
        <Field label="City" htmlFor="sf-city" optional><Input id="sf-city" {...form.register("city")} /></Field>
        <Field label="Province" htmlFor="sf-province" optional>
          <NativeSelect id="sf-province" {...form.register("province")}>{provinces.map((p) => <option key={p}>{p}</option>)}</NativeSelect>
        </Field>
      </FieldRow>

      <FieldGroupHeading>Employment</FieldGroupHeading>
      <FieldRow cols={3}>
        <Field label="Campus" htmlFor="sf-campus" error={e.campusId?.message}>
          <NativeSelect id="sf-campus" {...form.register("campusId")}>{active.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</NativeSelect>
        </Field>
        <Field label="Staff type" htmlFor="sf-type">
          <NativeSelect id="sf-type" {...form.register("staffType")}>{staffTypes.map((t) => <option key={t} value={t}>{staffTypeLabel[t]}</option>)}</NativeSelect>
        </Field>
        <Field label="Employment" htmlFor="sf-employment">
          <NativeSelect id="sf-employment" {...form.register("employmentType")}>{employmentTypes.map((t) => <option key={t} value={t}>{employmentTypeLabel[t]}</option>)}</NativeSelect>
        </Field>
      </FieldRow>
      <FieldRow cols={3}>
        <Field label="Designation" htmlFor="sf-designation" optional>
          <NativeSelect id="sf-designation" {...form.register("designationId")}>
            <option value="">—</option>
            {(designations.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </NativeSelect>
        </Field>
        <Field label="Department" htmlFor="sf-department" optional>
          <NativeSelect id="sf-department" {...form.register("departmentId")}>
            <option value="">—</option>
            {(departments.data ?? []).map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
          </NativeSelect>
        </Field>
        <Field label="Joined" htmlFor="sf-join" error={e.joinDate?.message}><Input id="sf-join" type="date" {...form.register("joinDate")} /></Field>
      </FieldRow>
      {!staff && (
        <Field label="Employee number" htmlFor="sf-number" optional hint="Leave blank to use the next number in the school's sequence.">
          <Input id="sf-number" className="max-w-48 font-mono" {...form.register("employeeNumber")} />
        </Field>
      )}
    </FormDialog>
  );
}
