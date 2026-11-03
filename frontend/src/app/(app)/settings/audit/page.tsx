"use client";

import * as React from "react";
import type { ColumnDef } from "@tanstack/react-table";
import { ChevronRight, History } from "lucide-react";
import { cn } from "@/lib/utils";
import { formatDateTime } from "@/lib/format";
import { describeAudit, useAudit, type AuditEntry } from "@/lib/api/audit";
import { Page, PageHeader } from "@/components/shell/page";
import { Avatar } from "@/components/ui/display";
import { EmptyState } from "@/components/ui/states";
import { DataTable } from "@/components/data-table/data-table";
import { ClearFiltersButton, FacetFilter, TableToolbar } from "@/components/data-table/toolbar";
import { useListParams } from "@/components/data-table/use-list-params";
import { Input } from "@/components/ui/input";

type Change = { from?: unknown; to?: unknown };

function ChangeList({ entry }: { entry: AuditEntry }) {
  if (!entry.changes) return <p className="text-sm text-ink-3">No field details recorded.</p>;
  let parsed: Record<string, Change> = {};
  try {
    parsed = JSON.parse(entry.changes) as Record<string, Change>;
  } catch {
    return <pre className="text-xs">{entry.changes}</pre>;
  }
  const show = (v: unknown) => (v === null || v === undefined || v === "" ? <span className="text-ink-3">empty</span> : typeof v === "object" ? JSON.stringify(v) : String(v));
  return (
    <dl className="grid gap-1 text-sm">
      {Object.entries(parsed).map(([field, change]) => (
        <div key={field} className="grid grid-cols-[10rem_1fr] gap-3">
          <dt className="text-ink-2">{field.replace(/([A-Z])/g, " $1").replace(/\./g, " › ").toLowerCase()}</dt>
          <dd className="min-w-0 break-words">
            {"from" in change && <><span className="text-ink-2 line-through decoration-ink-3">{show(change.from)}</span><span className="mx-1.5 text-ink-3">→</span></>}
            <span className="text-ink">{show(change.to)}</span>
          </dd>
        </div>
      ))}
    </dl>
  );
}

export default function AuditPage() {
  const { params, update, clearFilters, apiQuery, activeFilterCount } = useListParams({ pageSize: 50 });
  const audit = useAudit({ ...apiQuery, changesOnly: params.filters.kind?.[0] === "changes" ? true : undefined, kind: undefined });
  const [expanded, setExpanded] = React.useState<string | null>(null);

  const columns: ColumnDef<AuditEntry, unknown>[] = [
    {
      id: "when", header: "When", meta: { width: "12rem" },
      cell: ({ row }) => <span className="text-ink-2 tabular">{formatDateTime(row.original.occurredAt)}</span>,
    },
    {
      id: "what", header: "What happened", meta: { pinned: true },
      cell: ({ row }) => (
        <div className="grid gap-2 py-1">
          <span className="flex items-center gap-2.5">
            <ChevronRight className={cn("size-3.5 shrink-0 text-ink-3 transition-transform", expanded === row.original.id && "rotate-90")} />
            <Avatar name={row.original.userName ?? "System"} size={22} />
            <span className="text-ink">{describeAudit(row.original)}</span>
          </span>
          {expanded === row.original.id && <div className="ml-6 rounded-md bg-sunken/70 px-3 py-2.5"><ChangeList entry={row.original} /></div>}
        </div>
      ),
    },
    { id: "ip", header: "From", meta: { hideBelow: "lg", width: "9rem" }, cell: ({ row }) => <span className="text-ink-3 tabular">{row.original.ipAddress ?? "—"}</span> },
  ];

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Audit log" }]} title="Audit log"
        description="Every change to school records and every sign-in event, with who, when and what changed. Entries can't be edited or removed." />
      <div className="rounded-lg border border-line bg-surface">
        <TableToolbar>
          <FacetFilter title="Show" single selected={params.filters.kind ?? []}
            options={[{ value: "changes", label: "Record changes only" }]} onChange={(v) => update({ filters: { kind: v } })} />
          <label className="flex items-center gap-2 text-sm text-ink-2">From <Input type="date" inputSize="sm" className="w-36" value={params.filters.from?.[0] ?? ""} onChange={(e) => update({ filters: { from: e.target.value ? [e.target.value] : [] } })} /></label>
          <label className="flex items-center gap-2 text-sm text-ink-2">to <Input type="date" inputSize="sm" className="w-36" value={params.filters.to?.[0] ?? ""} onChange={(e) => update({ filters: { to: e.target.value ? [e.target.value] : [] } })} /></label>
          <ClearFiltersButton count={activeFilterCount} onClear={clearFilters} />
        </TableToolbar>
        <DataTable caption="Audit log" columns={columns} rows={audit.data?.items} total={audit.data?.total ?? 0} params={params}
          onParamsChange={(patch) => update(patch, false)} getRowId={(r) => r.id} loading={audit.isPending} fetching={audit.isFetching}
          error={audit.error} onRetry={() => void audit.refetch()} filtered={activeFilterCount > 0} density="compact"
          onRowClick={(r) => setExpanded((current) => (current === r.id ? null : r.id))}
          empty={<EmptyState icon={<History />} title="Nothing recorded yet" />} />
      </div>
    </Page>
  );
}
