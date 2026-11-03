"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Check } from "lucide-react";
import { cn } from "@/lib/utils";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useSchoolProfile, useUpdateSchoolProfile, type SchoolProfile } from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input, NativeSelect } from "@/components/ui/input";
import { Field, FieldRow, FormSection } from "@/components/ui/field";
import { Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { toast } from "@/components/ui/toaster";
import { applyFieldErrors, nullIfEmpty } from "@/components/forms/form-dialog";
import { SchoolCrest } from "@/components/shell/sidebar";

const provinces = ["Punjab", "Sindh", "Khyber Pakhtunkhwa", "Balochistan", "Islamabad Capital Territory", "Gilgit-Baltistan", "Azad Jammu and Kashmir"];
const timezones = ["Asia/Karachi", "Asia/Dubai", "Asia/Riyadh", "Asia/Qatar", "Asia/Kolkata", "Asia/Dhaka", "Europe/London", "Europe/Berlin", "America/New_York", "UTC"];
const currencies = [["PKR", "Pakistani rupee"], ["AED", "UAE dirham"], ["SAR", "Saudi riyal"], ["QAR", "Qatari riyal"], ["USD", "US dollar"], ["GBP", "Pound sterling"], ["EUR", "Euro"]];
const locales = [["en-PK", "English (Pakistan)"], ["en-GB", "English (UK)"], ["en-US", "English (US)"], ["en-AE", "English (UAE)"], ["ur-PK", "Urdu (Pakistan)"]];

/** Curated accents: deep enough for white text, distinct from status colours. */
const accents = [
  { name: "Pine", value: "#1F5F4A" },
  { name: "Ink", value: "#2E3F8F" },
  { name: "Maroon", value: "#7A2533" },
  { name: "Teal", value: "#1D6470" },
  { name: "Navy", value: "#1E3557" },
  { name: "Plum", value: "#5B3069" },
  { name: "Ochre", value: "#8A5A12" },
  { name: "Slate", value: "#3F4B5B" },
];

const optionalText = (max: number) => z.string().trim().max(max).optional().or(z.literal(""));

const schema = z.object({
  name: z.string().trim().min(2, "Enter the school's name.").max(200),
  shortName: optionalText(60),
  legalName: optionalText(250),
  motto: optionalText(200),
  registrationNumber: optionalText(80),
  email: z.union([z.literal(""), z.email("Enter a valid email address.")]),
  phone: optionalText(40),
  website: z.union([z.literal(""), z.url({ message: "Enter a full address, e.g. https://school.edu.pk" })]),
  line1: optionalText(200),
  line2: optionalText(200),
  city: optionalText(100),
  province: optionalText(100),
  postalCode: optionalText(20),
  countryCode: z.string().trim().length(2, "Use the 2-letter country code.").or(z.literal("")),
  timezone: z.string().min(1),
  currencyCode: z.string().length(3),
  locale: z.string().min(2),
  brandColor: z.string().regex(/^#[0-9a-fA-F]{6}$/, "Use a hex colour such as #1F5F4A."),
});

type Values = z.infer<typeof schema>;

function toValues(p: SchoolProfile): Values {
  return {
    name: p.name, shortName: p.shortName ?? "", legalName: p.legalName ?? "", motto: p.motto ?? "",
    registrationNumber: p.registrationNumber ?? "", email: p.email ?? "", phone: p.phone ?? "", website: p.website ?? "",
    line1: p.address.line1 ?? "", line2: p.address.line2 ?? "", city: p.address.city ?? "", province: p.address.province ?? "",
    postalCode: p.address.postalCode ?? "", countryCode: p.address.countryCode ?? "", timezone: p.timezone,
    currencyCode: p.currencyCode, locale: p.locale, brandColor: p.brandColor ?? "#1F5F4A",
  };
}

function AccentPicker({ value, onChange, disabled }: { value: string; onChange: (v: string) => void; disabled?: boolean }) {
  const custom = !accents.some((a) => a.value.toLowerCase() === value.toLowerCase());
  return (
    <div className="grid gap-3">
      <div className="flex flex-wrap gap-2" role="radiogroup" aria-label="Accent colour">
        {accents.map((accent) => {
          const selected = accent.value.toLowerCase() === value.toLowerCase();
          return (
            <button key={accent.value} type="button" role="radio" aria-checked={selected} aria-label={accent.name} title={accent.name}
              disabled={disabled} onClick={() => onChange(accent.value)}
              className={cn("grid size-8 place-items-center rounded-md shadow-[inset_0_0_0_1px_oklch(0_0_0/0.12)] transition-transform hover:scale-105",
                selected && "ring-2 ring-ink ring-offset-2 ring-offset-canvas")}
              style={{ background: accent.value }}>
              {selected && <Check className="size-4 text-white" strokeWidth={3} />}
            </button>
          );
        })}
      </div>
      <div className="flex items-center gap-2">
        <input type="color" aria-label="Custom colour" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value.toUpperCase())}
          className="h-8 w-10 cursor-pointer rounded-md border border-line-strong bg-surface p-0.5" />
        <Input value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)} className="w-32 font-mono text-sm" aria-label="Hex colour" />
        {custom && <span className="text-sm text-ink-2">Custom colour — automatically deepened if too light for text.</span>}
      </div>
    </div>
  );
}

export default function SchoolProfilePage() {
  const { can } = useSession();
  const editable = can(P.School.Manage);
  const profile = useSchoolProfile();
  const update = useUpdateSchoolProfile();
  const form = useForm<Values>({ resolver: zodResolver(schema) });

  React.useEffect(() => {
    if (profile.data) form.reset(toValues(profile.data));
  }, [profile.data, form]);

  const brand = form.watch("brandColor");
  const name = form.watch("shortName") || form.watch("name");

  // Preview the accent live across the interface while choosing; restore on leave.
  React.useEffect(() => {
    if (!brand || !/^#[0-9a-fA-F]{6}$/.test(brand)) return;
    const root = document.documentElement;
    const previous = root.style.getPropertyValue("--brand");
    root.style.setProperty("--brand", brand);
    return () => {
      if (previous) root.style.setProperty("--brand", previous);
      else root.style.removeProperty("--brand");
    };
  }, [brand]);

  const submit = form.handleSubmit(async (v) => {
    try {
      await update.mutateAsync({
        name: v.name, shortName: nullIfEmpty(v.shortName), legalName: nullIfEmpty(v.legalName), motto: nullIfEmpty(v.motto),
        registrationNumber: nullIfEmpty(v.registrationNumber), email: nullIfEmpty(v.email), phone: nullIfEmpty(v.phone),
        website: nullIfEmpty(v.website), timezone: v.timezone, currencyCode: v.currencyCode, locale: v.locale, brandColor: v.brandColor,
        address: { line1: nullIfEmpty(v.line1), line2: nullIfEmpty(v.line2), city: nullIfEmpty(v.city), province: nullIfEmpty(v.province),
          postalCode: nullIfEmpty(v.postalCode), countryCode: nullIfEmpty(v.countryCode) },
      });
      toast.success("School profile saved");
    } catch (error) {
      if (!applyFieldErrors(error, form)) toast.error(error, "The profile couldn't be saved");
    }
  });

  if (profile.isPending) return <Page className="px-0 sm:px-0 lg:px-0"><Skeleton className="mt-8 h-8 w-60" /><Skeleton className="mt-8 h-96" /></Page>;
  if (profile.error) return <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />;

  const errors = form.formState.errors;
  const dirty = form.formState.isDirty;
  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <form onSubmit={submit} noValidate>
        <PageHeader
          breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "School profile" }]}
          title="School profile"
          description="How the school is named and presented on screens, challans, report cards and certificates."
          actions={editable && (
            <>
              <Button type="button" variant="secondary" disabled={!dirty} onClick={() => form.reset(toValues(profile.data))}>Discard</Button>
              <Button type="submit" variant="primary" disabled={!dirty} loading={update.isPending}>Save changes</Button>
            </>
          )}
        />
        <fieldset disabled={!editable} className="contents">
          <FormSection title="Identity" description="The short name appears in the sidebar and on compact documents.">
            <FieldRow>
              <Field label="School name" htmlFor="name" error={errors.name?.message}><Input {...form.register("name")} /></Field>
              <Field label="Short name" htmlFor="shortName" optional><Input placeholder="e.g. Greenfield" {...form.register("shortName")} /></Field>
            </FieldRow>
            <FieldRow>
              <Field label="Legal name" htmlFor="legalName" optional hint="The registered entity, printed on receipts."><Input {...form.register("legalName")} /></Field>
              <Field label="Registration number" htmlFor="registrationNumber" optional hint="e.g. PEIRA or board registration."><Input {...form.register("registrationNumber")} /></Field>
            </FieldRow>
            <Field label="Motto" htmlFor="motto" optional><Input {...form.register("motto")} /></Field>
          </FormSection>

          <FormSection title="Contact & address" description="Shown to families and printed on letterheads.">
            <FieldRow cols={3}>
              <Field label="Email" htmlFor="email" optional error={errors.email?.message}><Input type="email" {...form.register("email")} /></Field>
              <Field label="Phone" htmlFor="phone" optional><Input type="tel" placeholder="+92 42 …" {...form.register("phone")} /></Field>
              <Field label="Website" htmlFor="website" optional error={errors.website?.message}><Input placeholder="https://" {...form.register("website")} /></Field>
            </FieldRow>
            <Field label="Address" htmlFor="line1" optional><Input placeholder="Street and building" {...form.register("line1")} /></Field>
            <Field label="Area" htmlFor="line2" optional><Input placeholder="Area / sector" {...form.register("line2")} /></Field>
            <FieldRow cols={4}>
              <Field label="City" htmlFor="city" optional><Input {...form.register("city")} /></Field>
              <Field label="Province" htmlFor="province" optional>
                <Input list="provinces" {...form.register("province")} />
              </Field>
              <Field label="Postal code" htmlFor="postalCode" optional><Input {...form.register("postalCode")} /></Field>
              <Field label="Country" htmlFor="countryCode" optional error={errors.countryCode?.message} hint="2-letter code"><Input maxLength={2} {...form.register("countryCode")} /></Field>
            </FieldRow>
            <datalist id="provinces">{provinces.map((p) => <option key={p} value={p} />)}</datalist>
          </FormSection>

          <FormSection title="Regional" description="The timezone decides what 'today' means for attendance, due dates and reports.">
            <FieldRow cols={3}>
              <Field label="Timezone" htmlFor="timezone">
                <NativeSelect {...form.register("timezone")}>{timezones.map((t) => <option key={t} value={t}>{t.replace("_", " ")}</option>)}</NativeSelect>
              </Field>
              <Field label="Currency" htmlFor="currencyCode">
                <NativeSelect {...form.register("currencyCode")}>{currencies.map(([c, label]) => <option key={c} value={c}>{c} · {label}</option>)}</NativeSelect>
              </Field>
              <Field label="Language & formats" htmlFor="locale">
                <NativeSelect {...form.register("locale")}>{locales.map(([l, label]) => <option key={l} value={l}>{label}</option>)}</NativeSelect>
              </Field>
            </FieldRow>
          </FormSection>

          <FormSection title="Branding" description="The accent colour runs through buttons, highlights and printed documents."
            aside={
              <div className="mt-4 hidden items-center gap-3 rounded-lg border border-line bg-surface p-3 lg:flex">
                <SchoolCrest name={name || "School"} size={36} />
                <div className="grid gap-1.5">
                  <span className="text-sm font-semibold">{name || "School"}</span>
                  <span className="inline-flex h-6 items-center rounded-md bg-accent px-2 text-xs font-medium text-accent-ink">Primary action</span>
                </div>
              </div>
            }>
            <Field label="Accent colour" error={errors.brandColor?.message}>
              <AccentPicker value={brand ?? "#1F5F4A"} disabled={!editable}
                onChange={(v) => form.setValue("brandColor", v, { shouldDirty: true, shouldValidate: true })} />
            </Field>
          </FormSection>
        </fieldset>
      </form>
    </Page>
  );
}
