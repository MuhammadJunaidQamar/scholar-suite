"use client";

import * as React from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { leaveKindLabel, useCreateStudentLeave, type StudentLeaveKind } from "@/lib/api/attendance";
import { todayIso } from "@/lib/format";
import { Field, FieldRow } from "@/components/ui/field";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Switch } from "@/components/ui/toggles";
import { toast } from "@/components/ui/toaster";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";
import { StudentPicker, type PickedStudent } from "./student-picker";

const kinds = Object.keys(leaveKindLabel) as StudentLeaveKind[];

const schema = z.object({
  studentId: z.string().min(1, "Choose the student."),
  fromDate: z.string().min(1, "Choose the first day."),
  toDate: z.string().min(1, "Choose the last day."),
  kind: z.enum(kinds as [StudentLeaveKind, ...StudentLeaveKind[]]),
  reason: z.string().trim().min(3, "Give the reason for the leave.").max(1000),
  requestedBy: z.string().trim().max(150),
  approve: z.boolean(),
}).refine((v) => !v.fromDate || !v.toDate || v.toDate >= v.fromDate, { path: ["toDate"], message: "The leave must end on or after the day it starts." });
type Values = z.infer<typeof schema>;

/**
 * Records a leave application the office has received (a note, a call). Approving straight away turns absences
 * already recorded on those days into Leave and pre-marks the registers still to come.
 */
export function RecordStudentLeaveDialog({ open, onOpenChange, student }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  student?: PickedStudent | null;
}) {
  const create = useCreateStudentLeave();
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  const today = todayIso();
  const defaults: Values = { studentId: student?.id ?? "", fromDate: today, toDate: today, kind: "Sick", reason: "", requestedBy: "", approve: true };
  // The picked student is shown as a chip; the form keeps only its id. Each opening starts from the given student.
  const [picked, setPicked] = React.useState<PickedStudent | null>(student ?? null);
  const [wasOpen, setWasOpen] = React.useState(open);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) setPicked(student ?? null);
  }

  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} defaults={defaults} size="md"
      title="Record student leave" description="A leave application from the family — by note, phone or in person."
      submitLabel="Record leave"
      onSubmit={async (v) => {
        const result = await create.mutateAsync({
          studentId: v.studentId, fromDate: v.fromDate, toDate: v.toDate, kind: v.kind, reason: v.reason,
          requestedBy: nullIfEmpty(v.requestedBy), approve: v.approve,
        });
        toast.success(v.approve ? "Leave approved" : "Leave recorded, awaiting a decision",
          result.marksChanged > 0 ? `${result.marksChanged} recorded absence${result.marksChanged === 1 ? " was" : "s were"} changed to Leave.` : undefined);
      }}>
      <Field label="Student" htmlFor="sl-student" error={form.formState.errors.studentId?.message}>
        <StudentPicker id="sl-student" value={picked} autoFocus={!student} invalid={!!form.formState.errors.studentId}
          onChange={(s) => { setPicked(s); form.setValue("studentId", s?.id ?? "", { shouldValidate: !!s }); }} />
      </Field>
      <FieldRow cols={3}>
        <Field label="From" htmlFor="sl-from" error={form.formState.errors.fromDate?.message}>
          <Input id="sl-from" type="date" {...form.register("fromDate")} />
        </Field>
        <Field label="To" htmlFor="sl-to" error={form.formState.errors.toDate?.message}>
          <Input id="sl-to" type="date" {...form.register("toDate")} />
        </Field>
        <Field label="Type" htmlFor="sl-kind">
          <NativeSelect id="sl-kind" {...form.register("kind")}>
            {kinds.map((k) => <option key={k} value={k}>{leaveKindLabel[k]}</option>)}
          </NativeSelect>
        </Field>
      </FieldRow>
      <Field label="Reason" htmlFor="sl-reason" error={form.formState.errors.reason?.message}>
        <Textarea id="sl-reason" rows={2} placeholder="e.g. Fever — doctor's note to follow" {...form.register("reason")} />
      </Field>
      <Field label="Applied by" htmlFor="sl-by" optional hint="Who asked, and how (e.g. Father, by phone).">
        <Input id="sl-by" {...form.register("requestedBy")} />
      </Field>
      <Controller control={form.control} name="approve" render={({ field }) => (
        <label className="flex items-start gap-3 rounded-md border border-line bg-sunken/50 px-3 py-2.5">
          <Switch checked={field.value} onCheckedChange={field.onChange} className="mt-0.5" />
          <span className="grid gap-0.5">
            <span className="text-sm font-medium text-ink">Approve now</span>
            <span className="text-sm text-ink-2">Absences already recorded on these days become Leave, and unsent absence alerts are cancelled.</span>
          </span>
        </label>
      )} />
    </FormDialog>
  );
}
