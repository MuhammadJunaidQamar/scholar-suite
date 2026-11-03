"use client";

import * as React from "react";
import Link from "next/link";
import type { ColumnDef, VisibilityState } from "@tanstack/react-table";
import { Globe, Plus, UserPlus } from "lucide-react";
import { cn } from "@/lib/utils";
import { formatDate, formatRelative } from "@/lib/format";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCampuses } from "@/lib/api/access";
import { useAcademicYears, useGrades } from "@/lib/api/org";
import { formatPhone } from "@/lib/api/students";
import {
  applicationStatusLabel,
  applicationStatusTone,
  closedStages,
  openStages,
  useApplications,
  usePipeline,
  type ApplicationListItem,
  type ApplicationStatus,
} from "@/lib/api/admissions";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/display";
import { EmptyState } from "@/components/ui/states";
import { DataTable, type Density } from "@/components/data-table/data-table";
import { ClearFiltersButton, FacetFilter, TableSearch, TableToolbar, ViewMenu, useTablePreference } from "@/components/data-table/toolbar";
import { useListParams } from "@/components/data-table/use-list-params";
import { AdmissionsNav } from "@/components/admissions/admissions-nav";

function statusDetail(a: ApplicationListItem) {
  if (a.status === "Waitlisted" && a.waitlistRank) return `No. ${a.waitlistRank} on the list`;
  if (a.status === "Offered" && a.offerExpiresOn) return `Reply by ${formatDate(a.offerExpiresOn)}`;
  return null;
}

const columns: ColumnDef<ApplicationListItem, unknown>[] = [
  {
    id: "name",
    header: "Applicant",
    meta: { sortKey: "name", pinned: true },
    cell: ({ row }) => (
      <div className="grid min-w-0 py-1 leading-tight">
        <Link href={`/admissions/applications/${row.original.id}`} className="truncate font-medium text-ink hover:underline" onClick={(e) => e.stopPropagation()}>
          {row.original.applicantName}
        </Link>
        <span className="flex items-center gap-1.5 truncate font-mono text-xs text-ink-2">
          {row.original.applicationNumber}
          {row.original.source === "Online" && <Globe className="size-3 text-ink-3" aria-label="Applied online" />}
        </span>
      </div>
    ),
  },
  { id: "grade", header: "Class", meta: { sortKey: "grade", label: "Class" }, cell: ({ row }) => row.original.gradeName },
  { id: "campus", header: "Campus", meta: { label: "Campus", hideBelow: "lg" }, cell: ({ row }) => <span className="text-ink-2">{row.original.campusName}</span> },
  {
    id: "parent",
    header: "Parent",
    meta: { label: "Parent", hideBelow: "md" },
    cell: ({ row }) => (
      <div className="grid leading-tight">
        <span className="truncate text-ink">{row.original.guardianName}</span>
        <span className="truncate text-xs text-ink-2 tabular">{formatPhone(row.original.guardianPhone)}</span>
      </div>
    ),
  },
  {
    id: "submitted",
    header: "Received",
    meta: { sortKey: "submitted", label: "Received", hideBelow: "lg" },
    cell: ({ row }) => row.original.submittedAt
      ? <span className="text-ink-2 tabular" title={formatDate(row.original.submittedAt)}>{formatRelative(row.original.submittedAt)}</span>
      : <span className="text-ink-3">Draft</span>,
  },
  {
    id: "status",
    header: "Stage",
    meta: { sortKey: "status", label: "Stage" },
    cell: ({ row }) => (
      <div className="grid leading-tight">
        <StatusBadge tone={applicationStatusTone[row.original.status]}>{applicationStatusLabel[row.original.status]}</StatusBadge>
        {statusDetail(row.original) && <span className="pl-3 text-xs text-ink-2">{statusDetail(row.original)}</span>}
      </div>
    ),
  },
];

/** Short stage names for the narrow pipeline cells. */
const stageShort: Partial<Record<ApplicationStatus, string>> = { AssessmentScheduled: "Test booked", Offered: "Offered" };

/** Stage counts across the top; choosing one filters the list to it. */
function Pipeline({ yearId, active, onChoose }: { yearId?: string; active?: string; onChoose: (status?: ApplicationStatus) => void }) {
  const pipeline = usePipeline(yearId);
  const count = (s: ApplicationStatus) => pipeline.data?.find((p) => p.status === s)?.count ?? 0;
  const closed = closedStages.reduce((n, s) => n + count(s), 0);
  return (
    <div className="mb-4 grid grid-cols-2 overflow-hidden rounded-lg border border-line bg-surface sm:grid-cols-4 lg:grid-cols-8" role="group" aria-label="Filter by stage">
      {openStages.map((s, i) => (
        <button key={s} type="button" onClick={() => onChoose(active === s ? undefined : s)} aria-pressed={active === s}
          className={cn("grid gap-0.5 border-line px-3.5 py-3 text-left transition-colors hover:bg-sunken/70",
            i > 0 && "border-l max-sm:[&:nth-child(odd)]:border-l-0", "max-lg:border-b", active === s && "bg-sunken")}>
          <span className="truncate text-xs text-ink-2" title={applicationStatusLabel[s]}>{stageShort[s] ?? applicationStatusLabel[s]}</span>
          <span className={cn("font-display text-xl tabular", count(s) === 0 ? "text-ink-3" : "text-ink")}>{pipeline.isPending ? "–" : count(s)}</span>
        </button>
      ))}
      <div className="grid gap-0.5 border-l border-line bg-sunken/40 px-3.5 py-3">
        <span className="truncate text-xs text-ink-2">Closed</span>
        <span className="font-display text-xl text-ink-2 tabular">{pipeline.isPending ? "–" : closed}</span>
      </div>
    </div>
  );
}

export default function AdmissionsPage() {
  const { can } = useSession();
  const { params, update, clearFilters, apiQuery, activeFilterCount } = useListParams({ sort: "updated", dir: "desc" });
  const years = useAcademicYears();
  const grades = useGrades();
  const campuses = useCampuses();
  const [density, setDensity] = useTablePreference<Density>("applications.density", "comfortable");
  const [visibility, setVisibility] = useTablePreference<VisibilityState>("applications.columns", {});

  // Admissions run for the session after the current one when it exists.
  const openYears = (years.data ?? []).filter((y) => y.status !== "Closed").sort((a, b) => a.startDate.localeCompare(b.startDate));
  const defaultYear = openYears.find((y) => y.status === "Planned") ?? openYears.find((y) => y.isCurrent) ?? openYears[0];
  const yearId = params.filters.academicYearId?.[0] ?? defaultYear?.id;
  const query = { ...apiQuery, academicYearId: yearId };
  const applications = useApplications(query);
  const status = params.filters.status?.[0];
  const scope = params.filters.scope?.[0] ?? "open";
  const multiCampus = (campuses.data?.filter((c) => c.isActive).length ?? 0) > 1;

  return (
    <Page>
      <PageHeader title="Admissions"
        description={defaultYear ? `Applications and inquiries for ${yearId === defaultYear.id ? defaultYear.name : openYears.find((y) => y.id === yearId)?.name ?? "the session"}.` : "Applications and inquiries."}
        actions={can(P.Admissions.Create) && (
          <Button variant="primary" asChild><Link href="/admissions/applications/new"><Plus /> New application</Link></Button>
        )}
        meta={<AdmissionsNav />} />

      <Pipeline yearId={yearId} active={status} onChoose={(s) => update({ filters: { status: s ? [s] : undefined, scope: undefined } })} />

      <div className="rounded-lg border border-line bg-surface">
        <TableToolbar>
          <TableSearch value={params.search} onChange={(search) => update({ search })} placeholder="Name, application no. or mobile" />
          {openYears.length > 1 && (
            <FacetFilter title="Session" single selected={yearId ? [yearId] : []}
              options={openYears.map((y) => ({ value: y.id, label: y.name }))}
              onChange={(v) => update({ filters: { academicYearId: v } })} />
          )}
          <FacetFilter title="Stage" single selected={status ? [status] : []}
            options={[...openStages, ...closedStages].map((s) => ({ value: s, label: applicationStatusLabel[s] }))}
            onChange={(v) => update({ filters: { status: v } })} />
          <FacetFilter title="Class" selected={params.filters.gradeId ?? []} searchable
            options={(grades.data ?? []).filter((g) => g.isActive).map((g) => ({ value: g.id, label: g.name }))}
            onChange={(v) => update({ filters: { gradeId: v } })} />
          {multiCampus && (
            <FacetFilter title="Campus" single selected={params.filters.campusId ?? []}
              options={(campuses.data ?? []).filter((c) => c.isActive).map((c) => ({ value: c.id, label: c.name }))}
              onChange={(v) => update({ filters: { campusId: v } })} />
          )}
          <FacetFilter title="Source" single selected={params.filters.source ?? []}
            options={[{ value: "Online", label: "Online form" }, { value: "Office", label: "At the office" }]}
            onChange={(v) => update({ filters: { source: v } })} />
          {!status && (
            <FacetFilter title="Include closed" single selected={scope === "all" ? ["all"] : []}
              options={[{ value: "all", label: "Show enrolled, declined and withdrawn" }]}
              onChange={(v) => update({ filters: { scope: v } })} />
          )}
          <ClearFiltersButton count={activeFilterCount} onClear={clearFilters} />
          <div className="ml-auto">
            <ViewMenu columns={columns} visibility={visibility} onVisibilityChange={setVisibility} density={density} onDensityChange={setDensity} />
          </div>
        </TableToolbar>
        <DataTable caption="Applications" columns={columns} rows={applications.data?.items} total={applications.data?.total ?? 0} params={params}
          onParamsChange={(patch) => update(patch, false)} getRowId={(a) => a.id} loading={applications.isPending} fetching={applications.isFetching}
          error={applications.error} onRetry={() => void applications.refetch()} filtered={activeFilterCount > 0} density={density}
          columnVisibility={visibility} onColumnVisibilityChange={setVisibility} rowHref={(a) => `/admissions/applications/${a.id}`}
          empty={<EmptyState icon={<UserPlus />} title="No open applications"
            description="Applications arrive from the online form or are entered at the office. Inquiries can be turned into applications."
            action={can(P.Admissions.Create) && <Button variant="primary" asChild><Link href="/admissions/applications/new"><Plus /> New application</Link></Button>} />} />
      </div>
    </Page>
  );
}
