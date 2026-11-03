"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useFieldArray, useForm, useWatch, type UseFormReturn } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { CircleAlert, Plus, Trash2 } from "lucide-react";
import { ApiError } from "@/lib/api/client";
import { todayIso } from "@/lib/format";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCreateStudent, type GuardianRelationship } from "@/lib/api/students";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field, FieldRow, FormSection } from "@/components/ui/field";
import { Checkbox, RadioGroup, RadioItem } from "@/components/ui/toggles";
import { EmptyState } from "@/components/ui/states";
import { toast } from "@/components/ui/toaster";
import { nullIfEmpty } from "@/components/forms/form-dialog";
import { SectionSelect } from "@/components/students/section-picker";
import { GuardianEntryFields, GuardianRoleFlags } from "@/components/students/guardian-dialogs";
import { emptyGuardianEntry, guardianEntrySchema, guardianLinkInput, type GuardianEntry } from "@/components/students/guardian-entry";
import {
  StudentBackgroundFields,
  StudentContactFields,
  StudentHealthFields,
  StudentIdentityFields,
  StudentPreviousSchoolFields,
  emptyStudentDetails,
  mobileField,
  studentDetailsInput,
  studentDetailsShape,
  type StudentDetailsValues,
} from "@/components/students/student-form";

const schema = z
  .object({
    ...studentDetailsShape,
    sectionId: z.string().min(1, "Choose the class and section."),
    admissionDate: z.string().min(1, "Choose the admission date."),
    rollNumber: z.string().trim().max(20),
    manualNumber: z.boolean(),
    admissionNumber: z.string().trim().max(40),
    guardians: z.array(guardianEntrySchema).min(1, "Add at least one parent or guardian."),
    contacts: z.array(z.object({
      name: z.string().trim().min(1, "Enter a name.").max(150),
      relationship: z.string().trim().min(1, "Say who they are.").max(60),
      phone: mobileField,
    })).max(5),
  })
  .superRefine((v, ctx) => {
    if (v.manualNumber && !v.admissionNumber) ctx.addIssue({ code: "custom", path: ["admissionNumber"], message: "Enter the admission number." });
  });
type Values = z.infer<typeof schema>;

const defaults: Values = {
  ...emptyStudentDetails,
  sectionId: "", admissionDate: todayIso(), rollNumber: "", manualNumber: false, admissionNumber: "",
  guardians: [emptyGuardianEntry("Father", true)], contacts: [],
};

export default function NewStudentPage() {
  const router = useRouter();
  const { can } = useSession();
  const create = useCreateStudent();
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: defaults });
  const contacts = useFieldArray({ control: form.control, name: "contacts" });
  const [guardians, sectionId, manualNumber, lastName] = useWatch({ control: form.control, name: ["guardians", "sectionId", "manualNumber", "lastName"] });
  const [full, setFull] = React.useState<string | null>(null);
  const [override, setOverride] = React.useState(false);
  const errors = form.formState.errors;
  // The shared field groups are typed for the details alone; this form adds placement and family on top.
  const details = form as unknown as UseFormReturn<StudentDetailsValues>;

  const patchGuardian = (index: number, patch: Partial<GuardianEntry>) => {
    const list = [...form.getValues("guardians")];
    list[index] = { ...list[index]!, ...patch };
    form.setValue("guardians", list, { shouldValidate: form.formState.isSubmitted, shouldDirty: true });
  };
  const setPrimary = (index: number) =>
    form.setValue("guardians", form.getValues("guardians").map((g, i) => ({ ...g, isPrimary: i === index })), { shouldDirty: true });

  if (!can(P.Students.Create)) {
    return <Page><EmptyState title="You can't add students" description="Ask an administrator for the permission to create student records." /></Page>;
  }

  const submit = form.handleSubmit(async (v) => {
    try {
      const student = await create.mutateAsync({
        student: studentDetailsInput(v),
        placement: { sectionId: v.sectionId, admissionDate: v.admissionDate, rollNumber: nullIfEmpty(v.rollNumber) },
        guardians: v.guardians.map(guardianLinkInput),
        emergencyContacts: v.contacts.length ? v.contacts.map((c) => ({ ...c, alternatePhone: null })) : null,
        admissionNumber: v.manualNumber ? v.admissionNumber : null,
        overrideCapacity: override,
      });
      toast.success(`${student.fullName} added`, `Admission number ${student.admissionNumber}`);
      router.push(`/students/${student.id}`);
    } catch (error) {
      if (error instanceof ApiError && error.code === "section.full") {
        setFull(error.message);
        document.getElementById("ns-placement")?.scrollIntoView({ behavior: "smooth", block: "center" });
        return;
      }
      if (error instanceof ApiError && error.status === 409 && v.manualNumber) {
        form.setError("admissionNumber", { message: error.message });
        return;
      }
      toast.error(error, "The student couldn't be saved");
    }
  }, () => toast.warning("Some details need attention", "Check the highlighted fields."));

  return (
    <Page width="wide">
      <form onSubmit={submit} noValidate>
        <PageHeader breadcrumbs={[{ label: "Students", href: "/students" }, { label: "Add student" }]} title="Add a student"
          description="For a student joining without an application. Applicants are enrolled from Admissions instead." />

        <FormSection title="Student" description="As written on the B-Form or birth certificate.">
          <StudentIdentityFields form={details} />
          <StudentBackgroundFields form={details} />
        </FormSection>

        <FormSection title="Class & admission" description="The section must have a free seat; the admission number is issued when you save.">
          <div id="ns-placement" className="grid gap-4">
            <FieldRow cols={3}>
              <div className="sm:col-span-2">
                <Field label="Class and section" htmlFor="ns-section" error={errors.sectionId?.message}>
                  <SectionSelect id="ns-section" value={sectionId} invalid={!!errors.sectionId}
                    onChange={(id) => {
                      form.setValue("sectionId", id, { shouldValidate: form.formState.isSubmitted, shouldDirty: true });
                      setFull(null);
                      setOverride(false);
                    }} />
                </Field>
              </div>
              <Field label="Admission date" htmlFor="ns-date" error={errors.admissionDate?.message}>
                <Input type="date" {...form.register("admissionDate")} />
              </Field>
            </FieldRow>
            {full && (
              <div className="grid gap-2.5 rounded-md border border-warning/40 bg-warning-soft px-3 py-2.5 text-sm">
                <p className="flex gap-2 text-ink"><CircleAlert className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />{full}</p>
                <label className="flex items-center gap-2 pl-6 text-ink">
                  <Checkbox checked={override} onCheckedChange={(v) => setOverride(v === true)} />
                  Place anyway, over the section&apos;s capacity
                </label>
              </div>
            )}
            <FieldRow cols={3}>
              <Field label="Roll number" htmlFor="ns-roll" optional><Input {...form.register("rollNumber")} /></Field>
              <div className="grid gap-1.5 sm:col-span-2">
                <span className="text-sm font-medium text-ink">Admission number</span>
                <RadioGroup value={manualNumber ? "manual" : "auto"} className="flex flex-wrap gap-x-5 gap-y-2 pt-1.5"
                  onValueChange={(v) => form.setValue("manualNumber", v === "manual")}>
                  <label className="flex items-center gap-2 text-sm text-ink"><RadioItem value="auto" /> Issue the next number</label>
                  <label className="flex items-center gap-2 text-sm text-ink"><RadioItem value="manual" /> Use an existing number</label>
                </RadioGroup>
              </div>
            </FieldRow>
            {manualNumber && (
              <FieldRow cols={3}>
                <Field label="Existing admission number" htmlFor="ns-number" error={errors.admissionNumber?.message}
                  hint="For students moving over from paper registers.">
                  <Input className="font-mono" {...form.register("admissionNumber")} />
                </Field>
              </FieldRow>
            )}
          </div>
        </FormSection>

        <FormSection title="Parents & guardians" description="Enter the mobile number first: if the family already has a child here, link the same parent so the family portal and sibling discounts see one family.">
          {errors.guardians?.root?.message && <p className="text-sm text-danger">{errors.guardians.root.message}</p>}
          <RadioGroup aria-label="Primary contact" className="grid gap-4"
            value={String(Math.max(0, (guardians ?? []).findIndex((g) => g.isPrimary)))}
            onValueChange={(v) => setPrimary(Number(v))}>
          {(guardians ?? []).map((entry, index) => (
            <div key={index} className="grid gap-4 rounded-lg border border-line bg-surface p-4">
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-semibold text-ink">{index === 0 ? "First parent or guardian" : "Another parent or guardian"}</p>
                {index > 0 && (
                  <Button type="button" size="sm" variant="ghost" onClick={() => form.setValue("guardians", form.getValues("guardians").filter((_, i) => i !== index))}>
                    <Trash2 /> Remove
                  </Button>
                )}
              </div>
              <GuardianEntryFields idPrefix={`ng-${index}`} value={entry} onChange={(p) => patchGuardian(index, p)}
                errors={errors.guardians?.[index] as never} />
              <div className="grid gap-3 border-t border-line pt-4">
                <label className="flex w-fit items-center gap-2 text-sm font-medium text-ink">
                  <RadioItem value={String(index)} /> Primary contact
                  <span className="font-normal text-ink-2">— called first</span>
                </label>
                <GuardianRoleFlags value={entry} onChange={(p) => patchGuardian(index, p)} />
              </div>
            </div>
          ))}
          </RadioGroup>
          {(guardians?.length ?? 0) < 4 && (
            <Button type="button" variant="secondary" className="w-fit"
              onClick={() => {
                const taken = new Set((guardians ?? []).map((g) => g.relationship));
                const next: GuardianRelationship = !taken.has("Mother") ? "Mother" : !taken.has("Father") ? "Father" : "Guardian";
                form.setValue("guardians", [...(guardians ?? []), emptyGuardianEntry(next, false, lastName ?? "")]);
              }}>
              <Plus /> Add another parent or guardian
            </Button>
          )}
        </FormSection>

        <FormSection title="Health" description="Only the one-line alert is shown to every teacher. Fuller medical notes are added on the profile by authorised staff.">
          <StudentHealthFields form={details} />
        </FormSection>

        <FormSection title="Home & contact">
          <StudentContactFields form={details} />
        </FormSection>

        <FormSection title="Previous school" description="Leave blank for a child starting school here.">
          <StudentPreviousSchoolFields form={details} />
        </FormSection>

        <FormSection title="Emergency contacts" description="Optional: people other than the parents to call, in order.">
          {contacts.fields.map((field, index) => (
            <div key={field.id} className="grid gap-3 sm:grid-cols-[1.3fr_1fr_1fr_auto] sm:items-start">
              <Field label="Name" htmlFor={`nc-name-${index}`} error={errors.contacts?.[index]?.name?.message}><Input {...form.register(`contacts.${index}.name`)} /></Field>
              <Field label="Who they are" htmlFor={`nc-rel-${index}`} error={errors.contacts?.[index]?.relationship?.message}>
                <Input placeholder="e.g. Uncle" {...form.register(`contacts.${index}.relationship`)} />
              </Field>
              <Field label="Mobile" htmlFor={`nc-phone-${index}`} error={errors.contacts?.[index]?.phone?.message}><Input type="tel" {...form.register(`contacts.${index}.phone`)} /></Field>
              <Button type="button" variant="ghost" size="icon" aria-label="Remove contact" className="sm:mt-6" onClick={() => contacts.remove(index)}><Trash2 /></Button>
            </div>
          ))}
          {contacts.fields.length < 5 && (
            <Button type="button" variant="secondary" className="w-fit" onClick={() => contacts.append({ name: "", relationship: "", phone: "" })}>
              <Plus /> Add emergency contact
            </Button>
          )}
        </FormSection>

        <div className="sticky bottom-0 z-10 -mx-4 mt-4 flex items-center justify-end gap-2 border-t border-line bg-canvas/95 px-4 py-3 backdrop-blur sm:-mx-6 sm:px-6 lg:-mx-8 lg:px-8">
          <Button type="button" variant="secondary" asChild><Link href="/students">Cancel</Link></Button>
          <Button type="submit" variant="primary" loading={create.isPending}>{override ? "Save over capacity" : "Save student"}</Button>
        </div>
      </form>
    </Page>
  );
}
