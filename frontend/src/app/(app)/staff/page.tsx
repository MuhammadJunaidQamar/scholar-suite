"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ColumnDef, VisibilityState } from "@tanstack/react-table";
import { Briefcase, KeyRound, Plus } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCampuses } from "@/lib/api/access";
import { useDepartments, useDesignations } from "@/lib/api/org";
import { formatPhone } from "@/lib/api/students";
import {
  exportStaff,
  staffStatusLabel,
  staffStatusTone,
  staffTypeLabel,
  useStaffList,
  type StaffListItem,
  type StaffScope,
  type StaffType,
} from "@/lib/api/staff";
import { formatDate } from "@/lib/format";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/display";
import { Segmented } from "@/components/ui/toggles";
import { EmptyState } from "@/components/ui/states";
import { DropdownMenuItem, Tooltip } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { DataTable, type Density } from "@/components/data-table/data-table";
import { ClearFiltersButton, ExportMenu, FacetFilter, TableSearch, TableToolbar, ViewMenu, useTablePreference } from "@/components/data-table/toolbar";
import { useListParams } from "@/components/data-table/use-list-params";
import { RecordAvatar } from "@/components/records/record-avatar";
import { StaffFormDialog } from "@/components/staff/staff-form-dialog";

function buildColumns(scope: StaffScope, multiCampus: boolean): ColumnDef<StaffListItem, unknown>[] {
  return [
    {
      id: "name",
      header: "Name",
      meta: { sortKey: "name", pinned: true },
      cell: ({ row }) => {
        const s = row.original;
        return (
          <div className="flex min-w-0 items-center gap-2.5 py-1">
            <RecordAvatar name={s.fullName} fileId={s.photoFileId} size={30} />
            <div className="grid min-w-0 leading-tight">
              <span className="flex min-w-0 items-center gap-1.5">
                <Link href={`/staff/${s.id}`} className="truncate font-medium text-ink hover:underline" onClick={(e) => e.stopPropagation()}>{s.fullName}</Link>
                {s.hasLogin && (
                  <Tooltip content="Can sign in"><KeyRound className="size-3.5 shrink-0 text-ink-3" aria-label="Can sign in" /></Tooltip>
                )}
              </span>
              <span className="truncate text-xs text-ink-2"><span className="font-mono">{s.employeeNumber}</span>{s.designationName && <span className="sm:hidden"> · {s.designationName}</span>}</span>
            </div>
          </div>
        );
      },
    },
    {
      id: "designation",
      header: "Role",
      meta: { sortKey: "designation", label: "Role", hideBelow: "sm" },
      cell: ({ row }) => (
        <div className="grid leading-tight">
          <span className="truncate text-ink">{row.original.designationName ?? staffTypeLabel[row.original.staffType]}</span>
          {row.original.departmentName && <span className="truncate text-xs text-ink-2">{row.original.departmentName}</span>}
        </div>
      ),
    },
    {
      id: "classes",
      header: "Class teacher of",
      meta: { label: "Class teacher of", hideBelow: "md" },
      cell: ({ row }) => row.original.classTeacherOf ? <span className="text-ink">{row.original.classTeacherOf}</span> : <span className="text-ink-3">—</span>,
    },
    ...(multiCampus ? [{
      id: "campus",
      header: "Campus",
      meta: { label: "Campus", hideBelow: "lg" as const },
      cell: ({ row }: { row: { original: StaffListItem } }) => <span className="text-ink-2">{row.original.campusName}</span>,
    }] : []),
    {
      id: "phone",
      header: "Mobile",
      meta: { label: "Mobile", hideBelow: "lg" },
      cell: ({ row }) => row.original.phone ? <span className="text-ink-2 tabular">{formatPhone(row.original.phone)}</span> : <span className="text-ink-3">—</span>,
    },
    {
      id: "joined",
      header: "Joined",
      meta: { sortKey: "joinDate", label: "Joined", hideBelow: "lg" },
      cell: ({ row }) => <span className="text-ink-2 tabular">{formatDate(row.original.joinDate)}</span>,
    },
    ...(scope !== "current" ? [{
      id: "status",
      header: "Status",
      meta: { label: "Status" },
      cell: ({ row }: { row: { original: StaffListItem } }) => <StatusBadge tone={staffStatusTone[row.original.status]}>{staffStatusLabel[row.original.status]}</StatusBadge>,
    }] : []),
  ];
}

export default function StaffPage() {
  const { can } = useSession();
  const router = useRouter();
  const { params, update, clearFilters, apiQuery, activeFilterCount } = useListParams({ sort: "name", dir: "asc" });
  const scope = (params.filters.scope?.[0] as StaffScope | undefined) ?? "current";
  const staff = useStaffList(apiQuery);
  const campuses = useCampuses();
  const departments = useDepartments();
  const designations = useDesignations();
  const [density, setDensity] = useTablePreference<Density>("staff.density", "comfortable");
  const [visibility, setVisibility] = useTablePreference<VisibilityState>("staff.columns", {});
  const [adding, setAdding] = React.useState(false);
  const [exporting, setExporting] = React.useState(false);
  const multiCampus = (campuses.data?.filter((c) => c.isActive).length ?? 0) > 1;
  const columns = React.useMemo(() => buildColumns(scope, multiCampus), [scope, multiCampus]);
  const total = staff.data?.total;
  const filters = activeFilterCount - (params.filters.scope ? 1 : 0);

  return (
    <Page>
      <PageHeader
        title="Staff"
        description={scope === "current" ? "Teachers, office and support staff, with the classes they look after." : "Staff who have left keep their records."}
        actions={
          <>
            {can(P.Staff.Export) && (
              <ExportMenu busy={exporting} onExport={async (format) => {
                setExporting(true);
                try { await exportStaff(apiQuery, format); } catch (e) { toast.error(e, "The export couldn't be created"); } finally { setExporting(false); }
              }} />
            )}
            {can(P.Staff.Create) && <Button variant="primary" onClick={() => setAdding(true)}><Plus /> Add staff</Button>}
          </>
        }
      />
      <div className="rounded-lg border border-line bg-surface">
        <TableToolbar>
          <Segmented<StaffScope> aria-label="Which staff" value={scope}
            onChange={(v) => update({ filters: { scope: v === "current" ? undefined : [v] } })}
            options={[{ value: "current", label: "Current" }, { value: "left", label: "Left" }, { value: "all", label: "All" }]} />
          <TableSearch value={params.search} onChange={(search) => update({ search })} placeholder="Name, employee no., email or mobile" />
          <FacetFilter title="Type" single selected={params.filters.staffType ?? []}
            options={(Object.keys(staffTypeLabel) as StaffType[]).map((t) => ({ value: t, label: staffTypeLabel[t] }))}
            onChange={(v) => update({ filters: { staffType: v } })} />
          <FacetFilter title="Designation" single searchable selected={params.filters.designationId ?? []}
            options={(designations.data ?? []).map((d) => ({ value: d.id, label: d.name }))} onChange={(v) => update({ filters: { designationId: v } })} />
          <FacetFilter title="Department" single searchable selected={params.filters.departmentId ?? []}
            options={(departments.data ?? []).map((d) => ({ value: d.id, label: d.name }))} onChange={(v) => update({ filters: { departmentId: v } })} />
          {multiCampus && (
            <FacetFilter title="Campus" single selected={params.filters.campusId ?? []}
              options={(campuses.data ?? []).filter((c) => c.isActive).map((c) => ({ value: c.id, label: c.name }))}
              onChange={(v) => update({ filters: { campusId: v } })} />
          )}
          <ClearFiltersButton count={filters} onClear={() => (scope === "current" ? clearFilters() : update({ search: undefined, filters: { staffType: undefined, designationId: undefined, departmentId: undefined, campusId: undefined } }))} />
          <div className="ml-auto flex items-center gap-2">
            {total !== undefined && <span className="text-sm text-ink-2 tabular max-md:hidden">{total.toLocaleString()} {total === 1 ? "person" : "people"}</span>}
            <ViewMenu columns={columns} visibility={visibility} onVisibilityChange={setVisibility} density={density} onDensityChange={setDensity} />
          </div>
        </TableToolbar>
        <DataTable
          caption="Staff"
          columns={columns}
          rows={staff.data?.items}
          total={total ?? 0}
          params={params}
          onParamsChange={(patch) => update(patch, false)}
          getRowId={(s) => s.id}
          loading={staff.isPending}
          fetching={staff.isFetching}
          error={staff.error}
          onRetry={() => void staff.refetch()}
          filtered={filters > 0}
          density={density}
          columnVisibility={visibility}
          onColumnVisibilityChange={setVisibility}
          rowHref={(s) => `/staff/${s.id}`}
          empty={<EmptyState icon={<Briefcase />} title={scope === "current" ? "No staff yet" : "Nobody here"}
            description="Add teachers and staff so classes have class teachers and registers have someone to take them."
            action={can(P.Staff.Create) && <Button variant="primary" onClick={() => setAdding(true)}><Plus /> Add staff</Button>} />}
          rowActions={(s) => <DropdownMenuItem onSelect={() => router.push(`/staff/${s.id}`)}>Open profile</DropdownMenuItem>}
        />
      </div>
      <StaffFormDialog staff={null} open={adding} onOpenChange={setAdding} onSaved={(saved) => router.push(`/staff/${saved.id}`)} />
    </Page>
  );
}
