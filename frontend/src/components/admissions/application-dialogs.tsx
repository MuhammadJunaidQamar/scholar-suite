"use client";

import * as React from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { CircleAlert } from "lucide-react";
import { ApiError } from "@/lib/api/client";
import { todayIso } from "@/lib/format";
import { useCampuses } from "@/lib/api/access";
import { useGrades } from "@/lib/api/org";
import {
  applicantFrom,
  applicantInput,
  applicantSchema,
  ApplicantAddressFields,
  ApplicantChildFields,
  ApplicantNotesField,
  ApplicantParentFields,
  ApplicantSchoolingFields,
  useApplicantFormAdapter,
} from "./applicant-form";
import {
  useApplicationAction,
  useDecision,
  useEnrollApplicant,
  useRecordAssessment,
  useScheduleAssessment,
  useUpdateApplication,
  type ApplicationDetail,
  type Assessment,
  type AssessmentOutcome,
  type AssessmentType,
} from "@/lib/api/admissions";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Checkbox, RadioGroup, RadioItem } from "@/components/ui/toggles";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";
import { FieldGroupHeading } from "@/components/students/student-form";
import { SectionSelect } from "@/components/students/section-picker";

type DialogProps = { application: ApplicationDetail; open: boolean; onOpenChange: (open: boolean) => void };

// ───────────────────────────── Decision ─────────────────────────────

export type Decision = "offer" | "waitlist" | "reject";

const decisionSchema = z.object({
  decision: z.enum(["offer", "waitlist", "reject"]),
  note: z.string().trim().max(1000),
  offerExpiresOn: z.string(),
  waitlistRank: z.string().regex(/^\d{0,3}$/, "Use a whole number."),
});
type DecisionValues = z.infer<typeof decisionSchema>;

const decisionCopy: Record<Decision, { label: string; description: string; submit: string; success: string }> = {
  offer: { label: "Offer a place", description: "The family is told a place is available and has until the reply date to accept.", submit: "Offer place", success: "Place offered" },
  waitlist: { label: "Waiting list", description: "A good applicant without a seat yet, kept in order.", submit: "Add to waiting list", success: "Added to the waiting list" },
  reject: { label: "Not offered", description: "Closes the application. The reason is kept on record.", submit: "Record decision", success: "Decision recorded" },
};

export function DecisionDialog({ application, open, onOpenChange, initial }: DialogProps & { initial: Decision }) {
  const decide = useDecision(application.id);
  const form = useForm<DecisionValues>({ resolver: zodResolver(decisionSchema) });
  const decision = useWatch({ control: form.control, name: "decision" }) ?? initial;
  const allowed = (["offer", "waitlist", "reject"] as const).filter((d) =>
    application.allowedTransitions.includes(d === "offer" ? "Offered" : d === "waitlist" ? "Waitlisted" : "Rejected"));
  const copy = decisionCopy[decision];
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="md"
      defaults={{ decision: initial, note: "", offerExpiresOn: "", waitlistRank: "" }}
      title={`Decide · ${application.applicantName}`} description={`${application.gradeName}, ${application.academicYearName}`}
      submitLabel={copy.submit} successMessage={copy.success}
      onSubmit={async (v) => {
        if (v.decision === "reject" && !v.note) {
          form.setError("note", { message: "Record why a place wasn't offered." });
          return false;
        }
        await decide.mutateAsync({
          decision: v.decision, note: nullIfEmpty(v.note),
          offerExpiresOn: v.decision === "offer" ? nullIfEmpty(v.offerExpiresOn) : null,
          waitlistRank: v.decision === "waitlist" && v.waitlistRank ? Number(v.waitlistRank) : null,
        });
      }}>
      <RadioGroup value={decision} onValueChange={(v) => form.setValue("decision", v as Decision)} className="grid gap-2" aria-label="Decision">
        {allowed.map((d) => (
          <label key={d} className="flex cursor-pointer items-start gap-3 rounded-md border border-line px-3 py-2.5 has-[[data-state=checked]]:border-ink/40 has-[[data-state=checked]]:bg-sunken/70">
            <RadioItem value={d} className="mt-0.5" />
            <span className="grid gap-0.5">
              <span className="text-sm font-medium text-ink">{decisionCopy[d].label}</span>
              <span className="text-sm text-ink-2">{decisionCopy[d].description}</span>
            </span>
          </label>
        ))}
      </RadioGroup>
      {decision === "offer" && (
        <FieldRow>
          <Field label="Reply by" htmlFor="dc-expiry" optional hint="Leave blank for the school's standard offer period.">
            <Input type="date" min={todayIso()} {...form.register("offerExpiresOn")} />
          </Field>
        </FieldRow>
      )}
      {decision === "waitlist" && (
        <FieldRow>
          <Field label="Position" htmlFor="dc-rank" optional hint="Blank adds them at the end of this class's list." error={form.formState.errors.waitlistRank?.message}>
            <Input inputMode="numeric" className="w-28 tabular" {...form.register("waitlistRank")} />
          </Field>
        </FieldRow>
      )}
      <Field label={decision === "reject" ? "Reason" : "Note"} htmlFor="dc-note" optional={decision !== "reject"} error={form.formState.errors.note?.message}
        hint={decision === "reject" ? "Kept on the application. Be factual: it may be shared with the family." : undefined}>
        <Textarea rows={2} {...form.register("note")} />
      </Field>
    </FormDialog>
  );
}

// ───────────────────────────── Assessments ─────────────────────────────

const scheduleSchema = z.object({
  type: z.enum(["EntranceTest", "Interview"]),
  date: z.string().min(1, "Choose the date."),
  time: z.string().min(1, "Choose the time."),
  location: z.string().trim().max(200),
});

export function ScheduleAssessmentDialog({ application, open, onOpenChange }: DialogProps) {
  const schedule = useScheduleAssessment(application.id);
  const form = useForm<z.infer<typeof scheduleSchema>>({ resolver: zodResolver(scheduleSchema) });
  const errors = form.formState.errors;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="md"
      defaults={{ type: "EntranceTest" as const, date: "", time: "09:00", location: "" }}
      title="Book an assessment" description={`${application.applicantName} · ${application.gradeName}`}
      submitLabel="Book" successMessage="Assessment booked"
      onSubmit={(v) => schedule.mutateAsync({ type: v.type as AssessmentType, scheduledAt: new Date(`${v.date}T${v.time}`).toISOString(), location: nullIfEmpty(v.location) })}>
      <Field label="Kind" htmlFor="as-type">
        <NativeSelect {...form.register("type")}>
          <option value="EntranceTest">Entrance test</option>
          <option value="Interview">Interview</option>
        </NativeSelect>
      </Field>
      <FieldRow>
        <Field label="Date" htmlFor="as-date" error={errors.date?.message}><Input type="date" min={todayIso()} {...form.register("date")} /></Field>
        <Field label="Time" htmlFor="as-time" error={errors.time?.message}><Input type="time" {...form.register("time")} /></Field>
      </FieldRow>
      <Field label="Where" htmlFor="as-where" optional><Input placeholder="e.g. Examination Hall, Gulberg Campus" {...form.register("location")} /></Field>
    </FormDialog>
  );
}

const resultSchema = z.object({
  outcome: z.enum(["Passed", "Failed", "NoShow"]),
  score: z.string().regex(/^\d{0,4}(\.\d{1,2})?$/, "Use a number."),
  maxScore: z.string().regex(/^\d{0,4}(\.\d{1,2})?$/, "Use a number."),
  notes: z.string().trim().max(1000),
}).refine((v) => !v.score || !v.maxScore || Number(v.score) <= Number(v.maxScore), { path: ["score"], message: "The score can't be more than the total." });

export function RecordResultDialog({ application, assessment, open, onOpenChange }: DialogProps & { assessment: Assessment | null }) {
  const record = useRecordAssessment(application.id);
  const form = useForm<z.infer<typeof resultSchema>>({ resolver: zodResolver(resultSchema) });
  const outcome = useWatch({ control: form.control, name: "outcome" });
  const errors = form.formState.errors;
  return (
    <FormDialog open={open && !!assessment} onOpenChange={onOpenChange} form={form} size="md"
      defaults={{ outcome: "Passed" as const, score: assessment?.score?.toString() ?? "", maxScore: assessment?.maxScore?.toString() ?? (assessment?.type === "EntranceTest" ? "50" : ""), notes: assessment?.notes ?? "" }}
      title="Record the result" description={`${application.applicantName} · ${assessment?.type === "Interview" ? "Interview" : "Entrance test"}`}
      submitLabel="Save result" successMessage="Result recorded"
      onSubmit={(v) => record.mutateAsync({
        assessmentId: assessment!.id, outcome: v.outcome as AssessmentOutcome,
        score: v.outcome === "NoShow" || !v.score ? null : Number(v.score), maxScore: v.outcome === "NoShow" || !v.maxScore ? null : Number(v.maxScore),
        notes: nullIfEmpty(v.notes),
      })}>
      <Field label="Outcome" htmlFor="rs-outcome">
        <NativeSelect {...form.register("outcome")}>
          <option value="Passed">Passed</option>
          <option value="Failed">Did not pass</option>
          <option value="NoShow">Did not attend</option>
        </NativeSelect>
      </Field>
      {outcome !== "NoShow" && (
        <FieldRow>
          <Field label="Marks" htmlFor="rs-score" optional error={errors.score?.message}><Input inputMode="decimal" className="tabular" {...form.register("score")} /></Field>
          <Field label="Out of" htmlFor="rs-max" optional error={errors.maxScore?.message}><Input inputMode="decimal" className="tabular" {...form.register("maxScore")} /></Field>
        </FieldRow>
      )}
      <Field label="Notes" htmlFor="rs-notes" optional><Textarea rows={2} {...form.register("notes")} /></Field>
    </FormDialog>
  );
}

// ───────────────────────────── Withdraw ─────────────────────────────

export function WithdrawDialog({ application, open, onOpenChange }: DialogProps) {
  const act = useApplicationAction(application.id);
  const form = useForm<{ reason: string }>();
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="sm" defaults={{ reason: "" }}
      title="Withdraw the application?" description="For when the family decides not to go ahead. The application is kept, closed."
      submitLabel="Withdraw" successMessage="Application withdrawn"
      onSubmit={(v) => act.mutateAsync({ action: "withdraw", reason: nullIfEmpty(v.reason) })}>
      <Field label="Reason" htmlFor="wd-reason" optional><Input placeholder="e.g. Chose another school" {...form.register("reason")} /></Field>
    </FormDialog>
  );
}

// ───────────────────────────── Enrol ─────────────────────────────

const enrollSchema = z.object({
  sectionId: z.string().min(1, "Choose the section."),
  admissionDate: z.string().min(1, "Choose the date."),
  rollNumber: z.string().trim().max(20),
});

export function EnrollDialog({ application, open, onOpenChange, onEnrolled, defaultDate }: DialogProps & {
  onEnrolled: (studentId: string) => void;
  defaultDate: string;
}) {
  const enroll = useEnrollApplicant(application.id);
  const form = useForm<z.infer<typeof enrollSchema>>({ resolver: zodResolver(enrollSchema) });
  const sectionId = useWatch({ control: form.control, name: "sectionId" });
  const [full, setFull] = React.useState<string | null>(null);
  const [override, setOverride] = React.useState(false);
  const handleOpenChange = (next: boolean) => {
    if (!next) {
      setFull(null);
      setOverride(false);
    }
    onOpenChange(next);
  };
  return (
    <FormDialog open={open} onOpenChange={handleOpenChange} form={form} size="md"
      defaults={{ sectionId: "", admissionDate: defaultDate, rollNumber: "" }}
      title={`Enrol ${application.applicantName}`}
      description="Creates the student record with an admission number, the parent's record (or links the existing one with the same mobile) and the placement, all at once. Documents move to the student."
      submitLabel={override ? "Enrol over capacity" : "Enrol"}
      onSubmit={async (v) => {
        try {
          const result = await enroll.mutateAsync({ sectionId: v.sectionId, admissionDate: v.admissionDate, rollNumber: nullIfEmpty(v.rollNumber), overrideCapacity: override });
          if (result.studentId) onEnrolled(result.studentId);
        } catch (error) {
          if (error instanceof ApiError && error.code === "section.full") {
            setFull(error.message);
            return false;
          }
          throw error;
        }
      }}>
      <Field label={`Section of ${application.gradeName}`} htmlFor="en-section" error={form.formState.errors.sectionId?.message}>
        <SectionSelect value={sectionId ?? ""} academicYearId={application.academicYearId} gradeId={application.gradeId}
          invalid={!!form.formState.errors.sectionId}
          onChange={(id) => {
            form.setValue("sectionId", id, { shouldValidate: form.formState.isSubmitted });
            setFull(null);
            setOverride(false);
          }} />
      </Field>
      <FieldRow>
        <Field label="Admission date" htmlFor="en-date" error={form.formState.errors.admissionDate?.message}><Input type="date" {...form.register("admissionDate")} /></Field>
        <Field label="Roll number" htmlFor="en-roll" optional><Input {...form.register("rollNumber")} /></Field>
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
    </FormDialog>
  );
}

// ───────────────────────────── Edit ─────────────────────────────

const editSchema = applicantSchema.extend({ gradeId: z.string().min(1), campusId: z.string().min(1) });
type EditValues = z.infer<typeof editSchema>;

export function EditApplicationDialog({ application, open, onOpenChange }: DialogProps) {
  const update = useUpdateApplication(application.id);
  const grades = useGrades();
  const campuses = useCampuses();
  const form = useForm<EditValues>({ resolver: zodResolver(editSchema) });
  const applicant = useApplicantFormAdapter(form);
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="xl"
      defaults={{ ...applicantFrom(application), gradeId: application.gradeId, campusId: application.campusId }}
      title="Edit application" description={`${application.applicationNumber} · ${application.academicYearName}`}
      submitLabel="Save changes" successMessage="Application saved"
      onSubmit={(v) => update.mutateAsync({ campusId: v.campusId, gradeId: v.gradeId, applicant: applicantInput(v), version: application.version })}>
      <FieldGroupHeading>Applying for</FieldGroupHeading>
      <FieldRow>
        <Field label="Class" htmlFor="ea-grade">
          <NativeSelect {...form.register("gradeId")}>{(grades.data ?? []).filter((g) => g.isActive).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}</NativeSelect>
        </Field>
        <Field label="Campus" htmlFor="ea-campus">
          <NativeSelect {...form.register("campusId")}>{(campuses.data ?? []).filter((c) => c.isActive).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</NativeSelect>
        </Field>
      </FieldRow>
      <FieldGroupHeading>Child</FieldGroupHeading>
      <ApplicantChildFields form={applicant} idPrefix="ea" />
      <ApplicantSchoolingFields form={applicant} idPrefix="ea" />
      <FieldGroupHeading>Parent</FieldGroupHeading>
      <ApplicantParentFields form={applicant} idPrefix="ea" />
      <ApplicantAddressFields form={applicant} idPrefix="ea" />
      <ApplicantNotesField form={applicant} idPrefix="ea" label="Notes" />
    </FormDialog>
  );
}
