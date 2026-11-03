"use client";

import * as React from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Dialog, DialogBody, DialogContent, DialogFooter, DialogHeader } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toaster";
import { ApiError } from "@/lib/api/client";
import { useInviteMember, useUpdateMemberRoles, type Member } from "@/lib/api/access";
import { RoleAssignmentsEditor } from "./role-assignments";

const assignments = z
  .array(z.object({ roleId: z.string(), campusId: z.string().nullable() }))
  .refine((rows) => rows.length > 0 && rows.every((r) => r.roleId), "Choose a role for every row.");

const inviteSchema = z.object({
  firstName: z.string().trim().min(1, "Enter a first name.").max(100),
  lastName: z.string().trim().min(1, "Enter a last name.").max(100),
  email: z.email("Enter a valid email address."),
  roles: assignments,
});

type InviteValues = z.infer<typeof inviteSchema>;

/** Applies server field errors (problem details) to the form's fields. */
export function applyServerErrors<T extends Record<string, unknown>>(
  error: unknown,
  setError: (name: keyof T & string, error: { message: string }) => void,
): boolean {
  if (!(error instanceof ApiError) || !error.isValidation) return false;
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    setError(field as keyof T & string, { message: messages[0] ?? "Invalid value" });
  }
  return true;
}

export function InviteMemberDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const invite = useInviteMember();
  const form = useForm<InviteValues>({
    resolver: zodResolver(inviteSchema),
    defaultValues: { firstName: "", lastName: "", email: "", roles: [{ roleId: "", campusId: null }] },
  });

  React.useEffect(() => {
    if (!open) form.reset();
  }, [open, form]);

  const submit = form.handleSubmit(async (values) => {
    if (invite.isPending) return;
    try {
      const member = await invite.mutateAsync(values);
      toast.success(`Invitation sent to ${member.fullName}`, "They'll receive an email to choose a password.");
      onOpenChange(false);
    } catch (error) {
      if (!applyServerErrors<InviteValues>(error, form.setError)) toast.error(error, "The invitation couldn't be sent");
    }
  });

  const errors = form.formState.errors;
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent size="md">
        <DialogHeader title="Invite someone" description="They'll get an email with a link to set their password." />
        <form onSubmit={submit} noValidate className="contents">
          <DialogBody className="grid gap-4">
            <FieldRow>
              <Field label="First name" htmlFor="invite-first" error={errors.firstName?.message}>
                <Input autoFocus autoComplete="off" {...form.register("firstName")} />
              </Field>
              <Field label="Last name" htmlFor="invite-last" error={errors.lastName?.message}>
                <Input autoComplete="off" {...form.register("lastName")} />
              </Field>
            </FieldRow>
            <Field label="Email" htmlFor="invite-email" error={errors.email?.message}>
              <Input type="email" autoComplete="off" placeholder="name@school.edu.pk" {...form.register("email")} />
            </Field>
            <Field label="Roles" error={errors.roles?.root?.message ?? errors.roles?.message}
              hint="A role limited to one campus only sees that campus's records.">
              <Controller
                control={form.control}
                name="roles"
                render={({ field }) => (
                  <RoleAssignmentsEditor value={field.value} onChange={field.onChange} invalid={!!errors.roles} />
                )}
              />
            </Field>
          </DialogBody>
          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" variant="primary" loading={invite.isPending}>Send invitation</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

export function EditRolesDialog({ member, onOpenChange }: { member: Member | null; onOpenChange: (open: boolean) => void }) {
  const update = useUpdateMemberRoles();
  const [rows, setRows] = React.useState<{ roleId: string; campusId: string | null }[]>([]);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (member) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setRows(member.roles.length > 0 ? member.roles.map((r) => ({ roleId: r.roleId, campusId: r.campusId })) : [{ roleId: "", campusId: null }]);
      setError(null);
    }
  }, [member]);

  const save = async () => {
    if (rows.some((r) => !r.roleId)) {
      setError("Choose a role for every row.");
      return;
    }
    try {
      await update.mutateAsync({ userId: member!.userId, roles: rows });
      toast.success(`Roles updated for ${member!.fullName}`, "Changes apply immediately, including in open sessions.");
      onOpenChange(false);
    } catch (e) {
      toast.error(e, "Roles couldn't be updated");
    }
  };

  return (
    <Dialog open={member !== null} onOpenChange={onOpenChange}>
      <DialogContent size="md">
        <DialogHeader title={`Roles for ${member?.fullName ?? ""}`} description="What they can see and do in this school." />
        <DialogBody>
          <Field error={error}>
            <RoleAssignmentsEditor value={rows} onChange={setRows} invalid={!!error} />
          </Field>
        </DialogBody>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button variant="primary" loading={update.isPending} onClick={save}>Save roles</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
