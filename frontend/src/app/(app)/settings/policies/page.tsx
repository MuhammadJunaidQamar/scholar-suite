"use client";

import * as React from "react";
import { Controller, useFieldArray, useForm } from "react-hook-form";
import { Plus, X } from "lucide-react";
import { cn } from "@/lib/utils";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { currencySymbol } from "@/lib/format";
import { useSchoolProfile, useUpdatePolicies, type DayOfWeek, type SchoolSettings } from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Field, FieldRow, FormSection } from "@/components/ui/field";
import { Switch } from "@/components/ui/toggles";
import { Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { toast } from "@/components/ui/toaster";

const days: { value: DayOfWeek; label: string }[] = [
  { value: "Monday", label: "Mon" }, { value: "Tuesday", label: "Tue" }, { value: "Wednesday", label: "Wed" },
  { value: "Thursday", label: "Thu" }, { value: "Friday", label: "Fri" }, { value: "Saturday", label: "Sat" }, { value: "Sunday", label: "Sun" },
];

type FormValues = Omit<SchoolSettings, "admissions"> & {
  admissions: Omit<SchoolSettings["admissions"], "requiredDocuments"> & { requiredDocuments: { value: string }[] };
};

function toForm(s: SchoolSettings): FormValues {
  return { ...s, admissions: { ...s.admissions, requiredDocuments: s.admissions.requiredDocuments.map((value) => ({ value })) } };
}

function toSettings(v: FormValues): SchoolSettings {
  return {
    ...v,
    attendance: { ...v.attendance, lockAfterHours: Number(v.attendance.lockAfterHours), minimumPercentage: Number(v.attendance.minimumPercentage) },
    finance: {
      ...v.finance,
      defaultDueDay: Number(v.finance.defaultDueDay), lateFeeAmount: Number(v.finance.lateFeeAmount),
      lateFeeGraceDays: Number(v.finance.lateFeeGraceDays), challanCopies: Number(v.finance.challanCopies),
      lateFeeCap: v.finance.lateFeeCap === null || String(v.finance.lateFeeCap) === "" ? null : Number(v.finance.lateFeeCap),
    },
    admissions: {
      ...v.admissions, offerValidityDays: Number(v.admissions.offerValidityDays),
      requiredDocuments: v.admissions.requiredDocuments.map((d) => d.value.trim()).filter(Boolean),
    },
    academics: { ...v.academics, passPercentage: Number(v.academics.passPercentage), marksDecimals: Number(v.academics.marksDecimals) },
  };
}

function SwitchRow({ label, hint, checked, onChange, disabled }: { label: string; hint?: string; checked: boolean; onChange: (v: boolean) => void; disabled?: boolean }) {
  const id = React.useId();
  return (
    <div className="flex items-start justify-between gap-6 border-b border-line py-3 last:border-b-0">
      <label htmlFor={id} className="grid gap-0.5">
        <span className="text-base text-ink">{label}</span>
        {hint && <span className="text-sm text-ink-2">{hint}</span>}
      </label>
      <Switch id={id} checked={checked} onCheckedChange={onChange} disabled={disabled} className="mt-0.5" />
    </div>
  );
}

export default function PoliciesPage() {
  const { can, me } = useSession();
  const editable = can(P.School.Manage);
  const profile = useSchoolProfile();
  const update = useUpdatePolicies();
  const form = useForm<FormValues>();
  const documents = useFieldArray({ control: form.control, name: "admissions.requiredDocuments" });

  React.useEffect(() => {
    if (profile.data) form.reset(toForm(profile.data.settings));
  }, [profile.data, form]);

  const submit = form.handleSubmit(async (values) => {
    try {
      await update.mutateAsync(toSettings(values));
      toast.success("Policies saved", "New rules apply from now on; past records are unchanged.");
    } catch (error) {
      toast.error(error, "The policies couldn't be saved");
    }
  });

  if (profile.isPending) return <Page className="px-0 sm:px-0 lg:px-0"><Skeleton className="mt-8 h-8 w-60" /><Skeleton className="mt-8 h-96" /></Page>;
  if (profile.error) return <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />;

  const rs = currencySymbol(me.school?.currencyCode ?? "PKR");
  const lateMode = form.watch("finance.lateFeeMode");
  const dirty = form.formState.isDirty;
  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <form onSubmit={submit} noValidate>
        <PageHeader
          breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Policies" }]}
          title="School policies"
          description="The rules the system applies for you: when the school is open, how attendance and fees behave, how results are decided."
          actions={editable && (
            <>
              <Button type="button" variant="secondary" disabled={!dirty} onClick={() => form.reset(toForm(profile.data.settings))}>Discard</Button>
              <Button type="submit" variant="primary" disabled={!dirty} loading={update.isPending}>Save changes</Button>
            </>
          )}
        />
        <fieldset disabled={!editable} className="contents">
          <FormSection title="Working week" description="Days the school teaches. Other days are excluded from attendance percentages.">
            <Controller control={form.control} name="workingDays" render={({ field }) => (
              <div className="flex flex-wrap gap-1.5" role="group" aria-label="Working days">
                {days.map((day) => {
                  const on = field.value?.includes(day.value);
                  return (
                    <button key={day.value} type="button" aria-pressed={on} disabled={!editable}
                      onClick={() => field.onChange(on ? field.value.filter((d) => d !== day.value) : [...(field.value ?? []), day.value])}
                      className={cn("h-8 w-12 rounded-md border text-sm font-medium transition-colors",
                        on ? "border-accent bg-accent-soft text-accent" : "border-line-strong bg-surface text-ink-2 hover:text-ink")}>
                      {day.label}
                    </button>
                  );
                })}
              </div>
            )} />
          </FormSection>

          <FormSection title="Attendance" description="How registers are kept and when guardians hear about absences.">
            <FieldRow cols={3}>
              <Field label="Registers" htmlFor="mode" hint="The morning roll call is always the official daily record.">
                <NativeSelect {...form.register("attendance.mode")}>
                  <option value="Daily">Daily roll call</option>
                  <option value="PeriodWise">Roll call and every period</option>
                </NativeSelect>
              </Field>
              <Field label="Lock registers after" htmlFor="lock" hint="Later changes need an approved correction.">
                <Input type="number" min={0} trailing={<span className="pr-1 text-sm">hours</span>} {...form.register("attendance.lockAfterHours")} />
              </Field>
              <Field label="Minimum attendance" htmlFor="minimum" hint="Students below this are flagged.">
                <Input type="number" min={0} max={100} step="0.5" trailing={<span className="pr-1 text-sm">%</span>} {...form.register("attendance.minimumPercentage")} />
              </Field>
            </FieldRow>
            <div>
              <Controller control={form.control} name="attendance.notifyGuardiansOnAbsence" render={({ field }) => (
                <SwitchRow label="Tell guardians about absences" hint="One message per guardian per day, from that day's roll call. Messages are only sent once an SMS or WhatsApp provider is connected; until then they are recorded as not sent." checked={field.value} onChange={field.onChange} disabled={!editable} />
              )} />
              <Controller control={form.control} name="attendance.notifyGuardiansOnLate" render={({ field }) => (
                <SwitchRow label="Tell guardians about late arrival" checked={field.value} onChange={field.onChange} disabled={!editable} />
              )} />
              <Controller control={form.control} name="attendance.excusedCountsAsPresent" render={({ field }) => (
                <SwitchRow label="Count approved leave as present" hint="Otherwise days on leave are left out of attendance percentages." checked={field.value} onChange={field.onChange} disabled={!editable} />
              )} />
            </div>
          </FormSection>

          <FormSection title="Fees" description="Defaults for challans and collections. Every fee change stays on the ledger.">
            <FieldRow cols={3}>
              <Field label="Default due day" htmlFor="due" hint="Day of the month challans fall due.">
                <Input type="number" min={1} max={28} {...form.register("finance.defaultDueDay")} />
              </Field>
              <Field label="Challan copies" htmlFor="copies" hint="Bank, school and student copies.">
                <NativeSelect {...form.register("finance.challanCopies")}>
                  {[1, 2, 3, 4].map((n) => <option key={n} value={n}>{n}</option>)}
                </NativeSelect>
              </Field>
              <Field label="Late fee" htmlFor="lateMode">
                <NativeSelect {...form.register("finance.lateFeeMode")}>
                  <option value="None">No late fee</option>
                  <option value="FixedOnce">Fixed amount, once</option>
                  <option value="PerDay">Amount per day late</option>
                </NativeSelect>
              </Field>
            </FieldRow>
            {lateMode !== "None" && (
              <FieldRow cols={3}>
                <Field label={lateMode === "PerDay" ? "Per day" : "Amount"} htmlFor="lateAmount">
                  <Input type="number" min={0} leading={<span className="text-sm">{rs}</span>} {...form.register("finance.lateFeeAmount")} />
                </Field>
                <Field label="Grace period" htmlFor="grace"><Input type="number" min={0} trailing={<span className="pr-1 text-sm">days</span>} {...form.register("finance.lateFeeGraceDays")} /></Field>
                <Field label="Maximum late fee" htmlFor="cap" optional>
                  <Input type="number" min={0} leading={<span className="text-sm">{rs}</span>} {...form.register("finance.lateFeeCap")} />
                </Field>
              </FieldRow>
            )}
            <div>
              <Controller control={form.control} name="finance.allowPartialPayments" render={({ field }) => (
                <SwitchRow label="Accept partial payments" checked={field.value} onChange={field.onChange} disabled={!editable} />
              )} />
              <Controller control={form.control} name="finance.concessionsRequireApproval" render={({ field }) => (
                <SwitchRow label="Concessions need approval" hint="A second person approves scholarships and discounts." checked={field.value} onChange={field.onChange} disabled={!editable} />
              )} />
              <Controller control={form.control} name="finance.refundsRequireApproval" render={({ field }) => (
                <SwitchRow label="Refunds need approval" checked={field.value} onChange={field.onChange} disabled={!editable} />
              )} />
            </div>
            <Field label="Instructions printed on challans" htmlFor="instructions" optional>
              <Textarea rows={2} {...form.register("finance.challanInstructions")} />
            </Field>
          </FormSection>

          <FormSection title="Admissions" description="Which steps every applicant goes through before enrolment.">
            <div>
              <Controller control={form.control} name="admissions.onlineApplicationsEnabled" render={({ field }) => (
                <SwitchRow label="Accept online applications" hint="Families can apply through the school's public form." checked={field.value} onChange={field.onChange} disabled={!editable} />
              )} />
              <Controller control={form.control} name="admissions.requireEntranceTest" render={({ field }) => (
                <SwitchRow label="Entrance test" checked={field.value} onChange={field.onChange} disabled={!editable} />
              )} />
              <Controller control={form.control} name="admissions.requireInterview" render={({ field }) => (
                <SwitchRow label="Interview" checked={field.value} onChange={field.onChange} disabled={!editable} />
              )} />
              <Controller control={form.control} name="admissions.requireAdmissionFeeBeforeEnrollment" render={({ field }) => (
                <SwitchRow label="Admission fee before enrolment" hint="The student is enrolled once the admission challan is paid." checked={field.value} onChange={field.onChange} disabled={!editable} />
              )} />
            </div>
            <FieldRow cols={3}>
              <Field label="Offers stay open for" htmlFor="validity">
                <Input type="number" min={1} max={120} trailing={<span className="pr-1 text-sm">days</span>} {...form.register("admissions.offerValidityDays")} />
              </Field>
            </FieldRow>
            <Field label="Documents to collect">
              <div className="grid gap-2">
                {documents.fields.map((doc, index) => (
                  <div key={doc.id} className="flex gap-2">
                    <Input {...form.register(`admissions.requiredDocuments.${index}.value` as const)} />
                    <Button type="button" variant="ghost" size="icon" aria-label="Remove document" onClick={() => documents.remove(index)}><X /></Button>
                  </div>
                ))}
                {editable && (
                  <div><Button type="button" variant="ghost" size="sm" onClick={() => documents.append({ value: "" })}><Plus /> Add document</Button></div>
                )}
              </div>
            </Field>
          </FormSection>

          <FormSection title="Results" description="Defaults for computing examination results.">
            <FieldRow cols={3}>
              <Field label="Pass mark" htmlFor="pass" hint="Per subject and overall.">
                <Input type="number" min={0} max={100} trailing={<span className="pr-1 text-sm">%</span>} {...form.register("academics.passPercentage")} />
              </Field>
              <Field label="Marks precision" htmlFor="decimals">
                <NativeSelect {...form.register("academics.marksDecimals")}>
                  <option value={0}>Whole numbers</option>
                  <option value={1}>One decimal</option>
                  <option value={2}>Two decimals</option>
                </NativeSelect>
              </Field>
            </FieldRow>
            <Controller control={form.control} name="academics.showPositions" render={({ field }) => (
              <SwitchRow label="Show class positions" hint="Rank on report cards (1st, 2nd…)." checked={field.value} onChange={field.onChange} disabled={!editable} />
            )} />
          </FormSection>
        </fieldset>
      </form>
    </Page>
  );
}
