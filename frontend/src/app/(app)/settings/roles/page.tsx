"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ChevronRight, Lock, Plus } from "lucide-react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useRoles, useSaveRole, type RoleListItem } from "@/lib/api/access";
import type { Portal } from "@/lib/auth/types";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input, Textarea, NativeSelect } from "@/components/ui/input";
import { Field } from "@/components/ui/field";
import { Badge, Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { Dialog, DialogBody, DialogContent, DialogFooter, DialogHeader } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toaster";
import { pluralize } from "@/lib/utils";

const portalOrder: Portal[] = ["Admin", "Teacher", "Family", "Student"];
const portalTitle: Record<Portal, { title: string; description: string }> = {
  Admin: { title: "Administration", description: "Office, leadership and specialist staff." },
  Teacher: { title: "Teaching", description: "Use the teacher portal for their own classes." },
  Family: { title: "Families", description: "Parents and guardians, limited to their own children." },
  Student: { title: "Students", description: "Limited to their own timetable, work and results." },
};

const createSchema = z.object({
  name: z.string().trim().min(2, "Give the role a name.").max(100),
  description: z.string().trim().max(500).optional(),
  portal: z.enum(["Admin", "Teacher", "Family", "Student"]),
});

function CreateRoleDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const save = useSaveRole();
  const router = useRouter();
  const form = useForm<z.infer<typeof createSchema>>({ resolver: zodResolver(createSchema), defaultValues: { name: "", description: "", portal: "Admin" } });
  React.useEffect(() => {
    if (!open) form.reset();
  }, [open, form]);

  const submit = form.handleSubmit(async (values) => {
    if (save.isPending) return;
    try {
      const role = await save.mutateAsync({ ...values, permissions: [] });
      toast.success(`${role.name} created`, "Now choose what it can do.");
      onOpenChange(false);
      router.push(`/settings/roles/${role.id}`);
    } catch (error) {
      toast.error(error, "The role couldn't be created");
    }
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent size="sm">
        <DialogHeader title="New role" description="Start empty, then grant exactly the permissions it needs." />
        <form onSubmit={submit} noValidate className="contents">
          <DialogBody className="grid gap-4">
            <Field label="Name" htmlFor="role-name" error={form.formState.errors.name?.message}>
              <Input autoFocus placeholder="e.g. Front desk" {...form.register("name")} />
            </Field>
            <Field label="Description" htmlFor="role-description" optional>
              <Textarea rows={2} placeholder="Who holds this role and why" {...form.register("description")} />
            </Field>
            <Field label="Lands in" htmlFor="role-portal" hint="The experience people with this role see after signing in.">
              <NativeSelect {...form.register("portal")}>
                {portalOrder.map((p) => (
                  <option key={p} value={p}>{portalTitle[p].title}</option>
                ))}
              </NativeSelect>
            </Field>
          </DialogBody>
          <DialogFooter>
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" variant="primary" loading={save.isPending}>Create role</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function RoleRow({ role }: { role: RoleListItem }) {
  return (
    <li>
      <Link href={`/settings/roles/${role.id}`} className="group grid grid-cols-[1fr_auto] items-center gap-4 px-4 py-3 transition-colors hover:bg-sunken/60 sm:grid-cols-[minmax(0,1fr)_8rem_7rem_1rem]">
        <span className="grid min-w-0 gap-0.5">
          <span className="flex items-center gap-2">
            <span className="truncate text-base font-medium text-ink">{role.name}</span>
            {role.isLocked && <Lock className="size-3.5 text-ink-3" aria-label="Locked" />}
            {!role.isSystem && <Badge tone="accent">Custom</Badge>}
          </span>
          {role.description && <span className="truncate text-sm text-ink-2">{role.description}</span>}
        </span>
        <span className="hidden text-sm text-ink-2 tabular sm:block">{pluralize(role.permissionCount, "permission")}</span>
        <span className="text-sm text-ink-2 tabular">{pluralize(role.memberCount, "person", "people")}</span>
        <ChevronRight className="hidden size-4 text-ink-3 sm:block" />
      </Link>
    </li>
  );
}

export default function RolesPage() {
  const roles = useRoles();
  const { can } = useSession();
  const [creating, setCreating] = React.useState(false);

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader
        breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Roles & permissions" }]}
        title="Roles & permissions"
        description="A role is a set of permissions. People can hold several roles, optionally limited to a campus."
        actions={can(P.Roles.Manage) && (
          <Button variant="primary" onClick={() => setCreating(true)}><Plus /> New role</Button>
        )}
      />
      {roles.isPending ? (
        <div className="grid gap-2">{Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-14" />)}</div>
      ) : roles.error ? (
        <ErrorState error={roles.error} onRetry={() => void roles.refetch()} />
      ) : (
        <div className="grid gap-8">
          {portalOrder.map((portal) => {
            const items = roles.data.filter((r) => r.portal === portal);
            if (items.length === 0) return null;
            return (
              <section key={portal} className="grid gap-2">
                <div className="flex items-baseline justify-between">
                  <h2 className="text-md font-semibold">{portalTitle[portal].title}</h2>
                  <p className="text-sm text-ink-2">{portalTitle[portal].description}</p>
                </div>
                <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
                  {items.map((role) => <RoleRow key={role.id} role={role} />)}
                </ul>
              </section>
            );
          })}
        </div>
      )}
      <CreateRoleDialog open={creating} onOpenChange={setCreating} />
    </Page>
  );
}
