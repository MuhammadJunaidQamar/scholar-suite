"use client";

import * as React from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { formatDays, useCreateStaffLeave, useLeaveBalances, useLeaveTypes } from "@/lib/api/attendance";
import { useStaffOptions } from "@/lib/api/staff";
import { todayIso } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Field, FieldRow } from "@/components/ui/field";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Switch } from "@/components/ui/toggles";
import { FormDialog } from "@/components/forms/form-dialog";

const schema = z.object({
  staffId: z.string(),
  leaveTypeId: z.string().min(1, "Choose the type of leave."),
  fromDate: z.string().min(1, "Choose the first day."),
  toDate: z.string().min(1, "Choose the last day."),
  halfDay: z.boolean(),
  reason: z.string().trim().min(3, "Give the reason.").max(1000),
  approve: z.boolean(),
}).refine((v) => v.toDate >= v.fromDate, { path: ["toDate"], message: "The leave must end on or after the day it starts." })
  .refine((v) => !v.halfDay || v.fromDate === v.toDate, { path: ["halfDay"], message: "A half day covers a single date." });
type Values = z.infer<typeof schema>;

/**
 * Leave for yourself (self-service, goes to an approver) or — for those who approve staff leave — recorded for
 * someone else, optionally approved at once. The chosen type's remaining balance is shown before submitting.
 */
export function StaffLeaveDialog({ open, onOpenChange, myStaffId, canRecordForOthers }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  myStaffId: string | null;
  canRecordForOthers: boolean;
}) {
  const create = useCreateStaffLeave();
  const types = useLeaveTypes();
  const staff = useStaffOptions(null, open && canRecordForOthers);
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  const today = todayIso();
  const defaults: Values = { staffId: myStaffId ?? "", leaveTypeId: "", fromDate: today, toDate: today, halfDay: false, reason: "", approve: false };
  const [staffId, leaveTypeId, halfDay] = useWatch({ control: form.control, name: ["staffId", "leaveTypeId", "halfDay"] });
  const forSomeoneElse = !!staffId && staffId !== myStaffId;
  const balances = useLeaveBalances(open ? staffId || null : null);
  const balance = balances.data?.find((b) => b.leaveTypeId === leaveTypeId);

  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} defaults={defaults}
      title={canRecordForOthers ? "Request or record staff leave" : "Request leave"}
      description={forSomeoneElse ? "Recorded on their behalf. Approve it now, or leave it for another approver." : "Your request goes to whoever approves staff leave."}
      submitLabel={forSomeoneElse && form.getValues("approve") ? "Record and approve" : "Send request"}
      successMessage={(v) => (v.approve && forSomeoneElse ? "Leave recorded and approved" : "Leave requested")}
      onSubmit={(v) => create.mutateAsync({
        staffId: v.staffId || null, leaveTypeId: v.leaveTypeId, fromDate: v.fromDate, toDate: v.halfDay ? v.fromDate : v.toDate,
        halfDay: v.halfDay, reason: v.reason, approve: forSomeoneElse && v.approve,
      })}>
      {canRecordForOthers && (
        <Field label="For" htmlFor="lv-staff">
          <NativeSelect id="lv-staff" {...form.register("staffId")}>
            {myStaffId ? <option value={myStaffId}>Myself</option> : <option value="">Choose a person</option>}
            {(staff.data ?? []).filter((s) => s.id !== myStaffId).map((s) => (
              <option key={s.id} value={s.id}>{s.fullName}{s.designationName ? ` · ${s.designationName}` : ""}</option>
            ))}
          </NativeSelect>
        </Field>
      )}
      <Field label="Type" htmlFor="lv-type" error={form.formState.errors.leaveTypeId?.message}
        hint={balance ? (balance.remaining === null ? "No yearly limit." : `${formatDays(balance.remaining)} left this year (${formatDays(balance.taken)} taken, ${formatDays(balance.pending)} awaiting approval).`) : undefined}>
        <NativeSelect id="lv-type" {...form.register("leaveTypeId")}>
          <option value="">Choose…</option>
          {(types.data ?? []).map((t) => <option key={t.id} value={t.id}>{t.name}{t.annualDays !== null ? ` · ${t.annualDays} days a year` : ""}</option>)}
        </NativeSelect>
      </Field>
      <FieldRow cols={2}>
        <Field label={halfDay ? "Date" : "From"} htmlFor="lv-from" error={form.formState.errors.fromDate?.message}>
          <Input id="lv-from" type="date" {...form.register("fromDate")} />
        </Field>
        <Field label="To" htmlFor="lv-to" error={form.formState.errors.toDate?.message} className={cn(halfDay && "opacity-50")}>
          <Input id="lv-to" type="date" disabled={halfDay} {...form.register("toDate")} />
        </Field>
      </FieldRow>
      <Controller control={form.control} name="halfDay" render={({ field }) => (
        <label className="flex items-center gap-2.5 text-sm text-ink">
          <Switch checked={field.value} onCheckedChange={field.onChange} /> Half day
          {form.formState.errors.halfDay && <span className="text-danger">{form.formState.errors.halfDay.message}</span>}
        </label>
      )} />
      <Field label="Reason" htmlFor="lv-reason" error={form.formState.errors.reason?.message}>
        <Textarea id="lv-reason" rows={2} {...form.register("reason")} />
      </Field>
      {forSomeoneElse && (
        <Controller control={form.control} name="approve" render={({ field }) => (
          <label className="flex items-start gap-3 rounded-md border border-line bg-sunken/50 px-3 py-2.5">
            <Switch checked={field.value} onCheckedChange={field.onChange} className="mt-0.5" />
            <span className="grid gap-0.5">
              <span className="text-sm font-medium text-ink">Approve now</span>
              <span className="text-sm text-ink-2">Days already passed are marked On leave on the staff register.</span>
            </span>
          </label>
        )} />
      )}
    </FormDialog>
  );
}
