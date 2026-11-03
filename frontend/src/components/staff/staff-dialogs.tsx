"use client";

import * as React from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useRoles, type AccountHandover } from "@/lib/api/access";
import { leavingStatuses, staffStatusLabel, useChangeStaffStatus, useGrantStaffAccess, type StaffDetail, type StaffStatus } from "@/lib/api/staff";
import { todayIso } from "@/lib/format";
import { Field } from "@/components/ui/field";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Dialog, DialogBody, DialogContent, DialogFooter, DialogHeader } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toaster";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";
import { AccountHandoverNotice } from "@/components/records/account-handover";

const statuses = Object.keys(staffStatusLabel) as StaffStatus[];

const statusSchema = z.object({
  status: z.enum(statuses as [StaffStatus, ...StaffStatus[]]),
  effectiveDate: z.string().min(1, "Choose the date."),
  reason: z.string().trim().max(500),
}).refine((v) => !leavingStatuses.includes(v.status) || v.reason.length >= 3, { path: ["reason"], message: "Record why the person is leaving." });
type StatusValues = z.infer<typeof statusSchema>;

/** Status changes; leaving releases the person's classes and subjects so the registers have a teacher. */
export function ChangeStaffStatusDialog({ staff, open, onOpenChange }: { staff: StaffDetail; open: boolean; onOpenChange: (open: boolean) => void }) {
  const change = useChangeStaffStatus();
  const form = useForm<StatusValues>({ resolver: zodResolver(statusSchema) });
  const status = useWatch({ control: form.control, name: "status" });
  const leaving = status ? leavingStatuses.includes(status) : false;
  const assignments = staff.classTeacherOf.length + staff.teaches.length;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="sm" title={`Change ${staff.fullName}'s status`}
      defaults={{ status: (staff.status === "Active" ? "Resigned" : "Active") as StaffStatus, effectiveDate: todayIso(), reason: "" }}
      submitLabel="Change status"
      onSubmit={async (v) => {
        const result = await change.mutateAsync({ id: staff.id, body: { status: v.status, effectiveDate: v.effectiveDate, reason: nullIfEmpty(v.reason) } });
        toast.success("Status changed", result.removedAssignments > 0
          ? `${result.removedAssignments} class and subject assignment${result.removedAssignments === 1 ? " was" : "s were"} released.` : undefined);
      }}>
      <Field label="New status" htmlFor="ss-status">
        <NativeSelect id="ss-status" {...form.register("status")}>
          {statuses.filter((s) => s !== staff.status).map((s) => <option key={s} value={s}>{staffStatusLabel[s]}</option>)}
        </NativeSelect>
      </Field>
      <Field label={leaving ? "Last working day" : "From"} htmlFor="ss-date" error={form.formState.errors.effectiveDate?.message}>
        <Input id="ss-date" type="date" {...form.register("effectiveDate")} />
      </Field>
      <Field label="Reason" htmlFor="ss-reason" optional={!leaving} error={form.formState.errors.reason?.message}>
        <Textarea id="ss-reason" rows={2} {...form.register("reason")} />
      </Field>
      {leaving && assignments > 0 && (
        <p className="rounded-md bg-warning-soft px-3 py-2 text-sm text-ink">
          {staff.fullName} is class teacher or subject teacher in {assignments} place{assignments === 1 ? "" : "s"}. Those will be released —
          assign someone else in Settings → Teaching assignments.
        </p>
      )}
      {leaving && staff.account && !staff.account.accessSuspended && (
        <p className="text-sm text-ink-2">They can still sign in. Suspend their access from Settings → Users if they should not.</p>
      )}
    </FormDialog>
  );
}

/** Gives a staff member a sign-in account (their email) with one role; shows the one-time password when nothing could be emailed. */
export function GrantAccessDialog({ staff, open, onOpenChange }: { staff: StaffDetail; open: boolean; onOpenChange: (open: boolean) => void }) {
  const roles = useRoles();
  const grant = useGrantStaffAccess();
  const [roleId, setRoleId] = React.useState("");
  const [handover, setHandover] = React.useState<AccountHandover | null>(null);
  const [wasOpen, setWasOpen] = React.useState(open);
  if (open !== wasOpen) {
    setWasOpen(open);
    if (open) { setHandover(null); setRoleId(""); }
  }
  const staffRoles = (roles.data ?? []).filter((r) => r.portal === "Admin" || r.portal === "Teacher");
  const suggested = staffRoles.find((r) => r.key === (staff.classTeacherOf.length > 0 ? "class_teacher" : staff.staffType === "Teaching" ? "teacher" : ""))?.id;
  const chosen = roleId || suggested || "";

  const submit = async () => {
    try {
      const result = await grant.mutateAsync({ id: staff.id, roles: [{ roleId: chosen, campusId: null }] });
      if (result.handover) setHandover(result.handover);
      else { toast.success("Sign-in access given", "They already had an account and can sign in with it."); onOpenChange(false); }
    } catch (error) {
      toast.error(error, "Access couldn't be given");
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent size="sm">
        <DialogHeader title={handover ? "Access ready" : `Give ${staff.fullName} sign-in access`}
          description={handover ? undefined : `They sign in with ${staff.email ?? "their email"}. Teachers take their registers and request leave from their own page.`} />
        <DialogBody>
          {handover ? (
            <AccountHandoverNotice handover={handover} name={staff.fullName} loginName={staff.email ?? ""} />
          ) : !staff.email ? (
            <p className="text-sm text-ink-2">Add an email address to {staff.fullName}&apos;s record first — it becomes their sign-in name.</p>
          ) : (
            <Field label="Role" htmlFor="ga-role" hint="Campus limits and extra roles can be set later in Settings → Users.">
              <NativeSelect id="ga-role" value={chosen} onChange={(e) => setRoleId(e.target.value)}>
                <option value="">Choose a role</option>
                {staffRoles.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
              </NativeSelect>
            </Field>
          )}
        </DialogBody>
        <DialogFooter>
          {handover ? <Button variant="primary" onClick={() => onOpenChange(false)}>Done</Button> : (
            <>
              <Button variant="ghost" onClick={() => onOpenChange(false)}>Cancel</Button>
              <Button variant="primary" disabled={!staff.email || !chosen} loading={grant.isPending} onClick={() => void submit()}>Give access</Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
