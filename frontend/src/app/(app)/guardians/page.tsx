"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ColumnDef, VisibilityState } from "@tanstack/react-table";
import { Plus, UsersRound } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { formatPhone, useGuardians, type GuardianListItem } from "@/lib/api/students";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/display";
import { EmptyState } from "@/components/ui/states";
import { DataTable, type Density } from "@/components/data-table/data-table";
import { ClearFiltersButton, FacetFilter, TableSearch, TableToolbar, ViewMenu, useTablePreference } from "@/components/data-table/toolbar";
import { useListParams } from "@/components/data-table/use-list-params";
import { RecordAvatar } from "@/components/records/record-avatar";
import { GuardianFormDialog } from "@/components/students/guardian-form-dialog";

const columns: ColumnDef<GuardianListItem, unknown>[] = [
  {
    id: "name",
    header: "Guardian",
    meta: { sortKey: "name", pinned: true },
    cell: ({ row }) => (
      <div className="flex min-w-0 items-center gap-2.5 py-1">
        <RecordAvatar name={row.original.fullName} fileId={null} size={30} />
        <div className="grid min-w-0 leading-tight">
          <Link href={`/guardians/${row.original.id}`} className="truncate font-medium text-ink hover:underline" onClick={(e) => e.stopPropagation()}>
            {row.original.fullName}
          </Link>
          <span className="truncate text-xs text-ink-2">{row.original.occupation ?? "—"}</span>
        </div>
      </div>
    ),
  },
  {
    id: "phone",
    header: "Mobile",
    meta: { label: "Mobile" },
    cell: ({ row }) => <span className="text-ink tabular">{formatPhone(row.original.phone)}</span>,
  },
  {
    id: "email",
    header: "Email",
    meta: { label: "Email", hideBelow: "lg" },
    cell: ({ row }) => row.original.email ? <span className="truncate text-ink-2">{row.original.email}</span> : <span className="text-ink-3">—</span>,
  },
  {
    id: "children",
    header: "Children",
    meta: { label: "Children", sortKey: "children" },
    cell: ({ row }) => {
      const names = row.original.children;
      if (names.length === 0) return <span className="text-ink-3">None linked</span>;
      const shown = names.slice(0, 3).join(", ");
      return <span className="text-ink">{shown}{names.length > 3 && <span className="text-ink-2"> +{names.length - 3}</span>}</span>;
    },
  },
  {
    id: "portal",
    header: "Family portal",
    meta: { label: "Family portal", hideBelow: "md" },
    cell: ({ row }) => row.original.hasLogin ? <StatusBadge tone="success">Has login</StatusBadge> : <span className="text-ink-2">No login</span>,
  },
];

export default function GuardiansPage() {
  const { can } = useSession();
  const router = useRouter();
  const { params, update, clearFilters, apiQuery, activeFilterCount } = useListParams({ sort: "name", dir: "asc" });
  const guardians = useGuardians(apiQuery);
  const [density, setDensity] = useTablePreference<Density>("guardians.density", "comfortable");
  const [visibility, setVisibility] = useTablePreference<VisibilityState>("guardians.columns", {});
  const [adding, setAdding] = React.useState(false);
  const total = guardians.data?.total;

  return (
    <Page>
      <PageHeader title="Guardians" description="Parents and guardians on record, with the children they're linked to."
        actions={can(P.Guardians.Create) && <Button variant="secondary" onClick={() => setAdding(true)}><Plus /> Add guardian</Button>} />
      <div className="rounded-lg border border-line bg-surface">
        <TableToolbar>
          <TableSearch value={params.search} onChange={(search) => update({ search })} placeholder="Name, mobile, CNIC or child's name" />
          <FacetFilter title="Family portal" single selected={params.filters.hasPortalAccount ?? []}
            options={[{ value: "true", label: "Has a login" }, { value: "false", label: "No login yet" }]}
            onChange={(v) => update({ filters: { hasPortalAccount: v } })} />
          <ClearFiltersButton count={activeFilterCount} onClear={clearFilters} />
          <div className="ml-auto flex items-center gap-2">
            {total !== undefined && <span className="text-sm text-ink-2 tabular max-md:hidden">{total.toLocaleString()} {total === 1 ? "guardian" : "guardians"}</span>}
            <ViewMenu columns={columns} visibility={visibility} onVisibilityChange={setVisibility} density={density} onDensityChange={setDensity} />
          </div>
        </TableToolbar>
        <DataTable caption="Guardians" columns={columns} rows={guardians.data?.items} total={total ?? 0} params={params}
          onParamsChange={(patch) => update(patch, false)} getRowId={(g) => g.id} loading={guardians.isPending} fetching={guardians.isFetching}
          error={guardians.error} onRetry={() => void guardians.refetch()} filtered={activeFilterCount > 0} density={density}
          columnVisibility={visibility} onColumnVisibilityChange={setVisibility} rowHref={(g) => `/guardians/${g.id}`}
          empty={<EmptyState icon={<UsersRound />} title="No guardians yet" description="Parents are added with their children, from Students or Admissions." />} />
      </div>
      <GuardianFormDialog guardian={null} open={adding} onOpenChange={setAdding} onSaved={(g) => router.push(`/guardians/${g.id}`)} />
    </Page>
  );
}
