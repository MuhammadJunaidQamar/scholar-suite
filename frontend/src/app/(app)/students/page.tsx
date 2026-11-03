"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ColumnDef, RowSelectionState, VisibilityState } from "@tanstack/react-table";
import { ArrowRightLeft, Download, FileUp, GraduationCap, HeartPulse, Plus, UserRoundCog } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { formatDate } from "@/lib/format";
import { useCampuses } from "@/lib/api/access";
import { useGrades, useSections } from "@/lib/api/org";
import {
  ageFrom,
  exportStudents,
  formatPhone,
  studentStatusLabel,
  studentStatusTone,
  useStudents,
  type StudentListItem,
  type StudentScope,
  type StudentStatus,
} from "@/lib/api/students";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/display";
import { Segmented } from "@/components/ui/toggles";
import { EmptyState } from "@/components/ui/states";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger, Tooltip } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { DataTable, type Density } from "@/components/data-table/data-table";
import { ClearFiltersButton, ExportMenu, FacetFilter, TableSearch, TableToolbar, ViewMenu, useTablePreference } from "@/components/data-table/toolbar";
import { useListParams } from "@/components/data-table/use-list-params";
import { RecordAvatar } from "@/components/records/record-avatar";
import { ChangeStatusDialog, ImportStudentsDialog, MoveSectionDialog, type MovableStudent } from "@/components/students/student-dialogs";

const leftStatuses: StudentStatus[] = ["Withdrawn", "TransferredOut", "Graduated", "Alumni", "Inactive"];

function buildColumns(scope: StudentScope, multiCampus: boolean): ColumnDef<StudentListItem, unknown>[] {
  return [
    {
      id: "name",
      header: "Student",
      meta: { sortKey: "name", pinned: true },
      cell: ({ row }) => {
        const s = row.original;
        return (
          <div className="flex min-w-0 items-center gap-2.5 py-1">
            <RecordAvatar name={s.fullName} fileId={s.photoFileId} size={30} />
            <div className="grid min-w-0 leading-tight">
              <span className="flex min-w-0 items-center gap-1.5">
                <Link href={`/students/${s.id}`} className="truncate font-medium text-ink hover:underline" onClick={(e) => e.stopPropagation()}>
                  {s.fullName}
                </Link>
                {s.hasMedicalAlert && (
                  <Tooltip content="Medical alert on file">
                    <HeartPulse className="size-3.5 shrink-0 text-danger" aria-label="Medical alert" />
                  </Tooltip>
                )}
                {s.status === "Suspended" && <StatusBadge tone="warning" variant="soft">Suspended</StatusBadge>}
              </span>
              <span className="truncate font-mono text-xs text-ink-2">{s.admissionNumber}</span>
            </div>
          </div>
        );
      },
    },
    {
      id: "class",
      header: scope === "left" ? "Last class" : "Class",
      meta: { sortKey: "class", label: "Class" },
      cell: ({ row }) =>
        row.original.sectionId ? (
          row.original.className
        ) : row.original.className ? (
          <span className="text-ink-2">{row.original.className}</span>
        ) : (
          <span className="text-ink-3">Not placed</span>
        ),
    },
    ...(multiCampus
      ? [{
          id: "campus",
          header: "Campus",
          meta: { label: "Campus", hideBelow: "lg" as const },
          cell: ({ row }: { row: { original: StudentListItem } }) => <span className="text-ink-2">{row.original.campusName}</span>,
        }]
      : []),
    {
      id: "guardian",
      header: "Parent / guardian",
      meta: { label: "Parent / guardian", hideBelow: "md" },
      cell: ({ row }) =>
        row.original.guardianName ? (
          <div className="grid leading-tight">
            <span className="truncate text-ink">{row.original.guardianName}</span>
            <span className="truncate text-xs text-ink-2 tabular">{formatPhone(row.original.guardianPhone)}</span>
          </div>
        ) : (
          <span className="text-ink-3">None recorded</span>
        ),
    },
    {
      id: "age",
      header: "Age",
      meta: { label: "Age", hideBelow: "lg", align: "right" },
      cell: ({ row }) => (
        <span className="text-ink-2 tabular" title={`Born ${formatDate(row.original.dateOfBirth)}`}>{ageFrom(row.original.dateOfBirth)}</span>
      ),
    },
    {
      id: "admitted",
      header: "Admitted",
      meta: { sortKey: "admissionDate", label: "Admitted", hideBelow: "lg" },
      cell: ({ row }) => <span className="text-ink-2 tabular">{formatDate(row.original.admissionDate)}</span>,
    },
    ...(scope !== "current"
      ? [{
          id: "status",
          header: "Status",
          meta: { label: "Status" },
          cell: ({ row }: { row: { original: StudentListItem } }) => (
            <div className="grid leading-tight">
              <StatusBadge tone={studentStatusTone[row.original.status]}>{studentStatusLabel[row.original.status]}</StatusBadge>
              {row.original.status !== "Active" && row.original.statusChangedOn && (
                <span className="pl-3 text-xs text-ink-2 tabular">{formatDate(row.original.statusChangedOn)}</span>
              )}
            </div>
          ),
        }]
      : []),
  ];
}

export default function StudentsPage() {
  const { can } = useSession();
  const router = useRouter();
  const { params, update, clearFilters, apiQuery, activeFilterCount } = useListParams({ sort: "name", dir: "asc" });
  const scope = ((params.filters.scope?.[0] as StudentScope | undefined) ?? "current");
  const students = useStudents(apiQuery);
  const grades = useGrades();
  const sections = useSections({});
  const campuses = useCampuses();
  const [density, setDensity] = useTablePreference<Density>("students.density", "comfortable");
  const [visibility, setVisibility] = useTablePreference<VisibilityState>("students.columns", {});
  const [selection, setSelection] = React.useState<RowSelectionState>({});
  const [moving, setMoving] = React.useState<MovableStudent[] | null>(null);
  const [statusFor, setStatusFor] = React.useState<StudentListItem | null>(null);
  const [importOpen, setImportOpen] = React.useState(false);
  const [exporting, setExporting] = React.useState(false);

  const multiCampus = (campuses.data?.filter((c) => c.isActive).length ?? 0) > 1;
  const columns = React.useMemo(() => buildColumns(scope, multiCampus), [scope, multiCampus]);
  const canUpdate = can(P.Students.Update);
  const canStatus = can(P.Students.ManageStatus);
  const rows = students.data?.items;

  // Selection is per list: changing scope or filters starts afresh.
  const filterKey = JSON.stringify(params.filters) + (params.search ?? "");
  const [selectionKey, setSelectionKey] = React.useState(filterKey);
  if (selectionKey !== filterKey) {
    setSelectionKey(filterKey);
    setSelection({});
  }

  const gradeOptions = (grades.data ?? []).filter((g) => g.isActive).map((g) => ({ value: g.id, label: g.name }));
  const chosenGrades = params.filters.gradeId ?? [];
  const sectionOptions = (sections.data ?? [])
    .filter((s) => chosenGrades.length === 0 || chosenGrades.includes(s.gradeId))
    .map((s) => ({ value: s.id, label: multiCampus ? `${s.displayName} · ${s.campusName}` : s.displayName, hint: String(s.enrolled) }));

  const runExport = async (format: "csv" | "xlsx", ids?: string[]) => {
    setExporting(true);
    try {
      await exportStudents(ids ? { ids } : apiQuery, format);
    } catch (error) {
      toast.error(error, "The export couldn't be created");
    } finally {
      setExporting(false);
    }
  };

  const total = students.data?.total;
  const description =
    scope === "current"
      ? "Everyone currently enrolled, with their class and the family contact."
      : scope === "left"
        ? "Students who have withdrawn, transferred, graduated or become alumni. Their records are kept."
        : "Every student record, current and past.";

  return (
    <Page>
      <PageHeader
        title="Students"
        description={description}
        actions={
          <>
            {can(P.Students.Import) && (
              <Button variant="secondary" onClick={() => setImportOpen(true)}>
                <FileUp /> <span className="max-sm:hidden">Import</span>
              </Button>
            )}
            {can(P.Students.Export) && <ExportMenu busy={exporting} onExport={(f) => void runExport(f)} />}
            {can(P.Students.Create) && (
              <Button variant="primary" asChild>
                <Link href="/students/new"><Plus /> Add student</Link>
              </Button>
            )}
          </>
        }
      />

      <div className="rounded-lg border border-line bg-surface">
        <TableToolbar>
          <Segmented<StudentScope>
            aria-label="Which students"
            value={scope}
            onChange={(v) => update({ filters: { scope: v === "current" ? undefined : [v], status: undefined } })}
            options={[
              { value: "current", label: "Current" },
              { value: "left", label: "Left" },
              { value: "all", label: "All" },
            ]}
          />
          <TableSearch value={params.search} onChange={(search) => update({ search })} placeholder="Name, admission no. or mobile" />
          <FacetFilter title="Class" options={gradeOptions} selected={chosenGrades} searchable
            onChange={(v) => update({ filters: { gradeId: v, sectionId: undefined } })} />
          <FacetFilter title="Section" options={sectionOptions} single searchable selected={params.filters.sectionId ?? []}
            onChange={(v) => update({ filters: { sectionId: v } })} />
          {multiCampus && (
            <FacetFilter title="Campus" single selected={params.filters.campusId ?? []}
              options={(campuses.data ?? []).filter((c) => c.isActive).map((c) => ({ value: c.id, label: c.name }))}
              onChange={(v) => update({ filters: { campusId: v } })} />
          )}
          <FacetFilter title="Gender" single selected={params.filters.gender ?? []}
            options={[{ value: "Female", label: "Girls" }, { value: "Male", label: "Boys" }]}
            onChange={(v) => update({ filters: { gender: v } })} />
          {scope !== "current" && (
            <FacetFilter title="Status" single selected={params.filters.status ?? []}
              options={(scope === "left" ? leftStatuses : (["Active", "Suspended", ...leftStatuses] as StudentStatus[])).map((s) => ({ value: s, label: studentStatusLabel[s] }))}
              onChange={(v) => update({ filters: { status: v } })} />
          )}
          <ClearFiltersButton count={activeFilterCount - (params.filters.scope ? 1 : 0)}
            onClear={() => (scope === "current" ? clearFilters() : update({ search: undefined, filters: { gradeId: undefined, sectionId: undefined, campusId: undefined, gender: undefined, status: undefined } }))} />
          <div className="ml-auto flex items-center gap-2">
            {total !== undefined && <span className="text-sm text-ink-2 tabular max-md:hidden">{total.toLocaleString()} {total === 1 ? "student" : "students"}</span>}
            <ViewMenu columns={columns} visibility={visibility} onVisibilityChange={setVisibility} density={density} onDensityChange={setDensity} />
          </div>
        </TableToolbar>
        <DataTable
          caption="Students"
          columns={columns}
          rows={rows}
          total={total ?? 0}
          params={params}
          onParamsChange={(patch) => update(patch, false)}
          getRowId={(s) => s.id}
          loading={students.isPending}
          fetching={students.isFetching}
          error={students.error}
          onRetry={() => void students.refetch()}
          filtered={activeFilterCount - (params.filters.scope ? 1 : 0) > 0}
          density={density}
          columnVisibility={visibility}
          onColumnVisibilityChange={setVisibility}
          rowHref={(s) => `/students/${s.id}`}
          selectable={canUpdate || can(P.Students.Export)}
          selection={selection}
          onSelectionChange={setSelection}
          bulkActions={(ids, clear) => (
            <>
              {canUpdate && scope !== "left" && (
                <Button size="sm" variant="ghost" onClick={() => {
                  const picked = (rows ?? []).filter((r) => ids.includes(r.id));
                  setMoving(picked.map((r) => ({ id: r.id, fullName: r.fullName, sectionId: r.sectionId })));
                }}>
                  <ArrowRightLeft /> Move to section
                </Button>
              )}
              {can(P.Students.Export) && (
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button size="sm" variant="ghost"><Download /> Export</Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="center" side="top">
                    <DropdownMenuLabel>Export selected</DropdownMenuLabel>
                    <DropdownMenuItem onSelect={() => void runExport("csv", ids).then(clear)}>CSV (.csv)</DropdownMenuItem>
                    <DropdownMenuItem onSelect={() => void runExport("xlsx", ids).then(clear)}>Excel (.xlsx)</DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              )}
            </>
          )}
          empty={
            scope === "current" ? (
              <EmptyState icon={<GraduationCap />} title="No students yet"
                description="Add students one at a time, import a spreadsheet, or enrol them from admissions."
                action={can(P.Students.Create) && <Button variant="primary" asChild><Link href="/students/new"><Plus /> Add student</Link></Button>} />
            ) : (
              <EmptyState icon={<GraduationCap />} title="No students here" description="Students who leave the school appear in this list." />
            )
          }
          rowActions={(s) => (
            <>
              <DropdownMenuItem onSelect={() => router.push(`/students/${s.id}`)}>Open profile</DropdownMenuItem>
              {(canUpdate || canStatus) && <DropdownMenuSeparator />}
              {canUpdate && (s.status === "Active" || s.status === "Suspended") && (
                <DropdownMenuItem onSelect={() => setMoving([{ id: s.id, fullName: s.fullName, sectionId: s.sectionId }])}>
                  <ArrowRightLeft /> Move to section…
                </DropdownMenuItem>
              )}
              {canStatus && s.status !== "Alumni" && (
                <DropdownMenuItem onSelect={() => setStatusFor(s)}><UserRoundCog /> Change status…</DropdownMenuItem>
              )}
            </>
          )}
        />
      </div>

      <MoveSectionDialog students={moving ?? []} open={moving !== null} onOpenChange={(o) => !o && setMoving(null)} onDone={() => setSelection({})} />
      <ChangeStatusDialog student={statusFor} open={statusFor !== null} onOpenChange={(o) => !o && setStatusFor(null)} />
      <ImportStudentsDialog open={importOpen} onOpenChange={setImportOpen} />
    </Page>
  );
}
