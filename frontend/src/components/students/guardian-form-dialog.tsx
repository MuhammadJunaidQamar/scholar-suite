"use client";

import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { formatNationalId, useSaveGuardian, type GuardianDetail } from "@/lib/api/students";
import { Input, NativeSelect } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Switch } from "@/components/ui/toggles";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";
import { FieldGroupHeading, addressFrom, mobileField, nationalIdField, provinces } from "./student-form";

const text = (max: number) => z.string().trim().max(max);

const schema = z.object({
  firstName: text(100).min(1, "Enter the first name."),
  lastName: text(100),
  phone: mobileField,
  alternatePhone: text(40),
  email: z.union([z.literal(""), z.email("Enter a valid email address.")]),
  nationalId: nationalIdField,
  occupation: text(100),
  employer: text(150),
  preferredLanguage: z.string(),
  line1: text(200),
  line2: text(200),
  city: text(100),
  province: text(100),
  notifyBySms: z.boolean(),
  notifyByEmail: z.boolean(),
  notifyByWhatsApp: z.boolean(),
});
type Values = z.infer<typeof schema>;

function valuesFrom(g: GuardianDetail | null): Values {
  return {
    firstName: g?.firstName ?? "", lastName: g?.lastName ?? "", phone: g?.phone ?? "", alternatePhone: g?.alternatePhone ?? "",
    email: g?.email ?? "", nationalId: formatNationalId(g?.nationalId), occupation: g?.occupation ?? "", employer: g?.employer ?? "",
    preferredLanguage: g?.preferredLanguage ?? "", line1: g?.address.line1 ?? "", line2: g?.address.line2 ?? "", city: g?.address.city ?? "",
    province: g?.address.province ?? "", notifyBySms: g?.notifyBySms ?? true, notifyByEmail: g?.notifyByEmail ?? false,
    notifyByWhatsApp: g?.notifyByWhatsApp ?? true,
  };
}

const channels: { key: "notifyBySms" | "notifyByWhatsApp" | "notifyByEmail"; label: string; hint: string }[] = [
  { key: "notifyBySms", label: "SMS", hint: "Absence alerts and reminders to the mobile number." },
  { key: "notifyByWhatsApp", label: "WhatsApp", hint: "The same messages on WhatsApp, where the school uses it." },
  { key: "notifyByEmail", label: "Email", hint: "Needs an email address." },
];

/** Create or edit a guardian's own details (their links to children are edited from each student). */
export function GuardianFormDialog({ guardian, open, onOpenChange, onSaved }: {
  guardian: GuardianDetail | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved?: (saved: GuardianDetail) => void;
}) {
  const save = useSaveGuardian();
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  const toggles = useWatch({ control: form.control, name: ["notifyBySms", "notifyByWhatsApp", "notifyByEmail"] });
  const email = useWatch({ control: form.control, name: "email" });
  const errors = form.formState.errors;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="lg" defaults={valuesFrom(guardian)}
      title={guardian ? `Edit ${guardian.fullName}` : "Add a guardian"}
      description={guardian ? "Changes apply to every child this guardian is linked to." : "Usually guardians are added with a student; use this for a parent you'll link later."}
      submitLabel={guardian ? "Save changes" : "Add guardian"} successMessage={guardian ? "Guardian saved" : "Guardian added"}
      onSubmit={async (v) => {
        const saved = await save.mutateAsync({
          id: guardian?.id,
          body: {
            firstName: v.firstName, lastName: v.lastName, phone: v.phone, alternatePhone: nullIfEmpty(v.alternatePhone), email: nullIfEmpty(v.email),
            nationalId: nullIfEmpty(v.nationalId), occupation: nullIfEmpty(v.occupation), employer: nullIfEmpty(v.employer),
            preferredLanguage: nullIfEmpty(v.preferredLanguage), address: addressFrom(v),
            notifyBySms: v.notifyBySms, notifyByEmail: v.notifyByEmail && !!nullIfEmpty(v.email), notifyByWhatsApp: v.notifyByWhatsApp,
          },
        });
        onSaved?.(saved);
      }}>
      <FieldRow>
        <Field label="First name" htmlFor="gf-first" error={errors.firstName?.message}><Input {...form.register("firstName")} /></Field>
        <Field label="Family name" htmlFor="gf-last" optional><Input {...form.register("lastName")} /></Field>
      </FieldRow>
      <FieldRow>
        <Field label="Mobile number" htmlFor="gf-phone" error={errors.phone?.message}><Input type="tel" placeholder="0300 1234567" {...form.register("phone")} /></Field>
        <Field label="Other number" htmlFor="gf-alt" optional><Input type="tel" {...form.register("alternatePhone")} /></Field>
      </FieldRow>
      <FieldRow>
        <Field label="Email" htmlFor="gf-email" optional error={errors.email?.message}><Input type="email" {...form.register("email")} /></Field>
        <Field label="CNIC" htmlFor="gf-cnic" optional error={errors.nationalId?.message}>
          <Input inputMode="numeric" placeholder="35202-1234567-8" className="tabular" {...form.register("nationalId")} />
        </Field>
      </FieldRow>
      <FieldRow cols={3}>
        <Field label="Occupation" htmlFor="gf-job" optional><Input {...form.register("occupation")} /></Field>
        <Field label="Employer" htmlFor="gf-employer" optional><Input {...form.register("employer")} /></Field>
        <Field label="Preferred language" htmlFor="gf-lang" optional>
          <NativeSelect {...form.register("preferredLanguage")}>
            <option value="">No preference</option>
            <option value="ur">Urdu</option>
            <option value="en">English</option>
          </NativeSelect>
        </Field>
      </FieldRow>
      <FieldGroupHeading>Address</FieldGroupHeading>
      <Field label="Home address" htmlFor="gf-line1" optional><Input placeholder="House, street" {...form.register("line1")} /></Field>
      <FieldRow cols={3}>
        <Field label="Area" htmlFor="gf-line2" optional><Input {...form.register("line2")} /></Field>
        <Field label="City" htmlFor="gf-city" optional><Input {...form.register("city")} /></Field>
        <Field label="Province" htmlFor="gf-province" optional><Input list="gf-provinces" {...form.register("province")} /></Field>
      </FieldRow>
      <datalist id="gf-provinces">{provinces.map((p) => <option key={p} value={p} />)}</datalist>
      <FieldGroupHeading>Messages from school</FieldGroupHeading>
      <div className="divide-y divide-line">
        {channels.map((c, i) => (
          <label key={c.key} className="flex items-center justify-between gap-4 py-2.5">
            <span className="grid gap-0.5">
              <span className="text-sm font-medium text-ink">{c.label}</span>
              <span className="text-xs text-ink-2">{c.hint}</span>
            </span>
            <Switch checked={!!toggles[i]} disabled={c.key === "notifyByEmail" && !email}
              onCheckedChange={(v) => form.setValue(c.key, v, { shouldDirty: true })} />
          </label>
        ))}
      </div>
    </FormDialog>
  );
}
