"use client";

import * as React from "react";
import { useParams, useRouter } from "next/navigation";
import { Lock, ShieldAlert, Trash2 } from "lucide-react";
import { cn, pluralize } from "@/lib/utils";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useDeleteRole, usePermissionCatalog, useRole, useSaveRole, type PermissionGroup } from "@/lib/api/access";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input, Textarea } from "@/components/ui/input";
import { Field, FormSection } from "@/components/ui/field";
import { Checkbox } from "@/components/ui/toggles";
import { Badge, Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Tooltip } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";

function PermissionGroupBlock({ group, selected, onToggle, onToggleAll, readOnly, held }: {
  group: PermissionGroup;
  selected: Set<string>;
  onToggle: (key: string, on: boolean) => void;
  onToggleAll: (keys: string[], on: boolean) => void;
  readOnly: boolean;
  held: (key: string) => boolean;
}) {
  const keys = group.permissions.map((p) => p.key);
  const count = keys.filter((k) => selected.has(k)).length;
  const all = count === keys.length;
  return (
    <section className="rounded-lg border border-line bg-surface">
      <header className="flex items-center gap-3 border-b border-line px-4 py-2.5">
        <Checkbox
          aria-label={`All ${group.label} permissions`}
          disabled={readOnly}
          checked={all ? true : count > 0 ? "indeterminate" : false}
          onCheckedChange={(v) => onToggleAll(keys.filter(held), v === true)}
        />
        <h3 className="flex-1 text-base font-semibold">{group.label}</h3>
        <span className="text-sm text-ink-3 tabular">{count}/{keys.length}</span>
      </header>
      <ul className="divide-y divide-line">
        {group.permissions.map((permission) => {
          const canGrant = held(permission.key);
          const id = `perm-${permission.key}`;
          return (
            <li key={permission.key} className={cn("flex items-start gap-3 px-4 py-2.5", !readOnly && canGrant && "hover:bg-sunken/50")}>
              <Checkbox
                id={id}
                className="mt-0.5"
                disabled={readOnly || (!canGrant && !selected.has(permission.key))}
                checked={selected.has(permission.key)}
                onCheckedChange={(v) => onToggle(permission.key, v === true)}
              />
              <label htmlFor={id} className="grid flex-1 cursor-pointer gap-0.5">
                <span className="flex items-center gap-2 text-base text-ink">
                  {permission.label}
                  {permission.sensitive && (
                    <Tooltip content="Grants access to sensitive personal or financial data.">
                      <span className="inline-flex"><Badge tone="warning">Sensitive</Badge></span>
                    </Tooltip>
                  )}
                </span>
                <span className="text-sm text-ink-2">{permission.description}</span>
              </label>
              {!readOnly && !canGrant && (
                <Tooltip content="You can't grant a permission you don't hold yourself.">
                  <Lock className="mt-1 size-3.5 text-ink-3" />
                </Tooltip>
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}

export default function RoleDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const role = useRole(id);
  const catalog = usePermissionCatalog();
  const save = useSaveRole(id);
  const remove = useDeleteRole();
  const { can, me, hasModule } = useSession();
  const [name, setName] = React.useState("");
  const [description, setDescription] = React.useState("");
  const [selected, setSelected] = React.useState<Set<string>>(new Set());
  const [confirmDelete, setConfirmDelete] = React.useState(false);

  React.useEffect(() => {
    if (role.data) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setName(role.data.name);
      setDescription(role.data.description ?? "");
      setSelected(new Set(role.data.permissions));
    }
  }, [role.data]);

  if (role.isPending || catalog.isPending) {
    return <Page className="px-0 sm:px-0 lg:px-0"><Skeleton className="mt-8 h-8 w-64" /><Skeleton className="mt-6 h-96" /></Page>;
  }
  if (role.error || catalog.error) {
    return <ErrorState error={role.error ?? catalog.error} onRetry={() => void role.refetch()} />;
  }

  const data = role.data;
  const readOnly = data.isLocked || !can(P.Roles.Manage);
  const mine = new Set(me.access?.permissions ?? []);
  const held = (key: string) => key.startsWith("portal.") || mine.has(key);
  const dirty =
    name !== data.name ||
    description !== (data.description ?? "") ||
    selected.size !== data.permissions.length ||
    data.permissions.some((p) => !selected.has(p));
  const groups = catalog.data.filter((g) => !g.module || hasModule(g.module));
  const sensitiveCount = catalog.data.flatMap((g) => g.permissions).filter((p) => p.sensitive && selected.has(p.key)).length;

  const toggle = (key: string, on: boolean) =>
    setSelected((current) => {
      const next = new Set(current);
      if (on) next.add(key);
      else next.delete(key);
      return next;
    });

  const onSave = async () => {
    try {
      await save.mutateAsync({ name, description: description || null, portal: data.portal, permissions: [...selected] });
      toast.success("Role saved", "Everyone holding it picks up the change straight away.");
    } catch (error) {
      toast.error(error, "The role couldn't be saved");
    }
  };

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader
        breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Roles & permissions", href: "/settings/roles" }, { label: data.name }]}
        title={data.name}
        description={data.isLocked ? "The owner role always holds every permission and cannot be changed." : data.description ?? undefined}
        meta={
          <>
            <span className="tabular">{pluralize(data.isLocked ? catalog.data.flatMap((g) => g.permissions).length : selected.size, "permission")}</span>
            {sensitiveCount > 0 && (
              <span className="flex items-center gap-1.5 text-[oklch(from_var(--warning)_calc(l-0.14)_c_h)] dark:text-warning">
                <ShieldAlert className="size-3.5" /> {pluralize(sensitiveCount, "sensitive permission")}
              </span>
            )}
            {data.isSystem && <span>Built-in role</span>}
          </>
        }
        actions={
          !readOnly && (
            <>
              {!data.isSystem && (
                <Button variant="danger-ghost" onClick={() => setConfirmDelete(true)}><Trash2 /> Delete</Button>
              )}
              <Button variant="secondary" disabled={!dirty} onClick={() => {
                setName(data.name);
                setDescription(data.description ?? "");
                setSelected(new Set(data.permissions));
              }}>Discard</Button>
              <Button variant="primary" disabled={!dirty || name.trim().length < 2} loading={save.isPending} onClick={onSave}>
                Save changes
              </Button>
            </>
          )
        }
      />

      {!readOnly && (
        <FormSection title="Details" description="Shown wherever this role is assigned.">
          <Field label="Name" htmlFor="role-name"><Input value={name} onChange={(e) => setName(e.target.value)} /></Field>
          <Field label="Description" htmlFor="role-description" optional>
            <Textarea rows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
          </Field>
        </FormSection>
      )}

      <FormSection title="Permissions" description="Grouped by area. Viewing permissions never include editing; sensitive areas are marked.">
        <div className="grid gap-4">
          {groups.map((group) => (
            <PermissionGroupBlock
              key={group.key}
              group={group}
              selected={data.isLocked ? new Set(group.permissions.map((p) => p.key)) : selected}
              readOnly={readOnly}
              held={held}
              onToggle={toggle}
              onToggleAll={(keys, on) =>
                setSelected((current) => {
                  const next = new Set(current);
                  keys.forEach((k) => (on ? next.add(k) : next.delete(k)));
                  return next;
                })
              }
            />
          ))}
        </div>
      </FormSection>

      <ConfirmDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        tone="danger"
        title={`Delete ${data.name}?`}
        description="Only roles nobody holds can be deleted. This can't be undone."
        confirmLabel="Delete role"
        loading={remove.isPending}
        onConfirm={async () => {
          try {
            await remove.mutateAsync(data.id);
            toast.success("Role deleted");
            router.replace("/settings/roles");
          } catch (error) {
            toast.error(error, "The role couldn't be deleted");
          }
        }}
      />
    </Page>
  );
}
