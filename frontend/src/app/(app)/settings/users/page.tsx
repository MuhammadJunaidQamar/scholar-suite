"use client";

import * as React from "react";
import type { ColumnDef, VisibilityState } from "@tanstack/react-table";
import { Ban, RotateCcw, ShieldCheck, UserPlus, Users } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { formatRelative } from "@/lib/format";
import { useMembers, useRoles, useSetMemberSuspended, type Member } from "@/lib/api/access";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Avatar, Badge, StatusBadge } from "@/components/ui/display";
import { EmptyState } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { DropdownMenuItem, DropdownMenuSeparator } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { DataTable, type Density } from "@/components/data-table/data-table";
import { ClearFiltersButton, FacetFilter, TableSearch, TableToolbar, ViewMenu, useTablePreference } from "@/components/data-table/toolbar";
import { useListParams } from "@/components/data-table/use-list-params";
import { EditRolesDialog, InviteMemberDialog } from "./member-dialogs";

function memberStatus(member: Member) {
  if (member.membershipStatus === "Suspended") return <StatusBadge tone="neutral">Suspended</StatusBadge>;
  if (member.status === "Invited") return <StatusBadge tone="info">Invited</StatusBadge>;
  if (member.status === "Disabled") return <StatusBadge tone="danger">Disabled</StatusBadge>;
  return <StatusBadge tone="success">Active</StatusBadge>;
}

const columns: ColumnDef<Member, unknown>[] = [
  {
    id: "name",
    header: "Name",
    meta: { sortKey: "name", pinned: true },
    cell: ({ row }) => (
      <div className="flex min-w-0 items-center gap-2.5 py-1">
        <Avatar name={row.original.fullName} size={28} />
        <div className="grid min-w-0 leading-tight">
          <span className="truncate font-medium text-ink">{row.original.fullName}</span>
          <span className="truncate text-xs text-ink-2">{row.original.email ?? row.original.userName}</span>
        </div>
      </div>
    ),
  },
  {
    id: "roles",
    header: "Roles",
    meta: { label: "Roles" },
    cell: ({ row }) =>
      row.original.roles.length === 0 ? (
        <span className="text-ink-3">No roles</span>
      ) : (
        <div className="flex flex-wrap gap-1">
          {row.original.roles.map((role) => (
            <Badge key={`${role.roleId}-${role.campusId}`}>
              {role.roleName}
              {role.campusName && <span className="ml-1 text-ink-3">· {role.campusName}</span>}
            </Badge>
          ))}
        </div>
      ),
  },
  { id: "status", header: "Status", meta: { label: "Status" }, cell: ({ row }) => memberStatus(row.original) },
  {
    id: "lastLogin",
    header: "Last signed in",
    meta: { sortKey: "lastLogin", hideBelow: "lg", label: "Last signed in" },
    cell: ({ row }) => (
      <span className="text-ink-2">{row.original.lastLoginAt ? formatRelative(row.original.lastLoginAt) : "Never"}</span>
    ),
  },
];

export default function UsersPage() {
  const { can, me } = useSession();
  const canManage = can(P.Users.Manage);
  const { params, update, clearFilters, apiQuery, activeFilterCount } = useListParams({ sort: "name", dir: "asc" });
  const members = useMembers(apiQuery);
  const roles = useRoles();
  const suspend = useSetMemberSuspended();
  const [inviteOpen, setInviteOpen] = React.useState(false);
  const [editing, setEditing] = React.useState<Member | null>(null);
  const [confirm, setConfirm] = React.useState<{ member: Member; suspend: boolean } | null>(null);
  const [density, setDensity] = useTablePreference<Density>("users.density", "comfortable");
  const [visibility, setVisibility] = useTablePreference<VisibilityState>("users.columns", {});

  const roleOptions = (roles.data ?? []).map((r) => ({ value: r.id, label: r.name, hint: String(r.memberCount) }));

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader
        breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Users" }]}
        title="Users"
        description={`Everyone who can sign in to ${me.school?.shortName ?? me.school?.name ?? "the school"}, and what they can do.`}
        actions={
          canManage && (
            <Button variant="primary" onClick={() => setInviteOpen(true)}>
              <UserPlus /> Invite someone
            </Button>
          )
        }
      />

      <div className="rounded-lg border border-line bg-surface">
        <TableToolbar>
          <TableSearch value={params.search} onChange={(search) => update({ search })} placeholder="Search name or email" />
          <FacetFilter title="Role" options={roleOptions} single selected={params.filters.roleId ?? []}
            onChange={(v) => update({ filters: { roleId: v } })} />
          <FacetFilter title="Status" single selected={params.filters.status ?? []}
            options={[{ value: "Active", label: "Active" }, { value: "Suspended", label: "Suspended" }]}
            onChange={(v) => update({ filters: { status: v } })} />
          <ClearFiltersButton count={activeFilterCount} onClear={clearFilters} />
          <div className="ml-auto">
            <ViewMenu columns={columns} visibility={visibility} onVisibilityChange={setVisibility} density={density} onDensityChange={setDensity} />
          </div>
        </TableToolbar>
        <DataTable
          caption="Users"
          columns={columns}
          rows={members.data?.items}
          total={members.data?.total ?? 0}
          params={params}
          onParamsChange={(patch) => update(patch, false)}
          getRowId={(m) => m.userId}
          loading={members.isPending}
          fetching={members.isFetching}
          error={members.error}
          onRetry={() => void members.refetch()}
          filtered={activeFilterCount > 0}
          density={density}
          columnVisibility={visibility}
          onColumnVisibilityChange={setVisibility}
          onRowClick={canManage ? setEditing : undefined}
          empty={<EmptyState icon={<Users />} title="No users yet" description="Invite the people who run the school." />}
          rowActions={
            canManage
              ? (member) => (
                  <>
                    <DropdownMenuItem onSelect={() => setEditing(member)}>
                      <ShieldCheck /> Edit roles
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                    {member.membershipStatus === "Suspended" ? (
                      <DropdownMenuItem onSelect={() => setConfirm({ member, suspend: false })}>
                        <RotateCcw /> Restore access
                      </DropdownMenuItem>
                    ) : (
                      <DropdownMenuItem tone="danger" disabled={member.userId === me.user.id}
                        onSelect={() => setConfirm({ member, suspend: true })}>
                        <Ban /> Suspend access
                      </DropdownMenuItem>
                    )}
                  </>
                )
              : undefined
          }
        />
      </div>

      <InviteMemberDialog open={inviteOpen} onOpenChange={setInviteOpen} />
      <EditRolesDialog member={editing} onOpenChange={(open) => !open && setEditing(null)} />
      <ConfirmDialog
        open={confirm !== null}
        onOpenChange={(open) => !open && setConfirm(null)}
        tone={confirm?.suspend ? "danger" : "primary"}
        title={confirm?.suspend ? `Suspend ${confirm.member.fullName}?` : `Restore access for ${confirm?.member.fullName}?`}
        description={
          confirm?.suspend
            ? "They will be signed out everywhere and can't sign in to this school until access is restored. Their records stay intact."
            : "They will be able to sign in again with their existing roles."
        }
        confirmLabel={confirm?.suspend ? "Suspend access" : "Restore access"}
        loading={suspend.isPending}
        onConfirm={async () => {
          if (!confirm) return;
          try {
            await suspend.mutateAsync({ userId: confirm.member.userId, suspended: confirm.suspend });
            toast.success(confirm.suspend ? "Access suspended" : "Access restored");
            setConfirm(null);
          } catch (error) {
            toast.error(error);
          }
        }}
      />
    </Page>
  );
}
