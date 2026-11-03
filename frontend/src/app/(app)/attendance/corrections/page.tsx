"use client";

import * as React from "react";
import { ClipboardCheck } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCorrections, type CorrectionStatus } from "@/lib/api/attendance";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Segmented } from "@/components/ui/toggles";
import { AttendanceNav } from "@/components/attendance/attendance-nav";
import { CorrectionCard } from "@/components/attendance/correction-card";

export default function CorrectionsPage() {
  const { can } = useSession();
  const approver = can(P.Attendance.Approve);
  const [status, setStatus] = React.useState<CorrectionStatus | "all">("Pending");
  const [page, setPage] = React.useState(1);
  const corrections = useCorrections({ status: status === "all" ? undefined : status, page, pageSize: 25 });
  const data = corrections.data;

  return (
    <Page>
      <PageHeader
        title="Corrections"
        description={approver
          ? "Changes asked for on registers that have locked. Nothing changes until it is approved; every decision stays on record."
          : "Changes you have asked for on locked registers, and what was decided."}
      />
      <AttendanceNav className="mb-6" />
      <div className="mb-3 flex items-center justify-between gap-3">
        <Segmented size="sm" aria-label="Show" value={status} onChange={(v) => { setStatus(v); setPage(1); }}
          options={[{ value: "Pending", label: "Awaiting approval" }, { value: "Approved", label: "Approved" }, { value: "Rejected", label: "Rejected" }, { value: "all", label: "All" }]} />
        {data && <span className="text-sm text-ink-2 tabular">{data.total} {data.total === 1 ? "correction" : "corrections"}</span>}
      </div>
      {corrections.isPending ? (
        <div className="grid gap-3">{[0, 1, 2].map((i) => <Skeleton key={i} className="h-32" />)}</div>
      ) : corrections.error ? (
        <ErrorState error={corrections.error} onRetry={() => void corrections.refetch()} />
      ) : data!.items.length === 0 ? (
        <div className="rounded-lg border border-line bg-surface">
          <EmptyState compact icon={<ClipboardCheck />} title={status === "Pending" ? "Nothing waiting" : "No corrections"}
            description="Corrections are asked for from a register once it has locked." />
        </div>
      ) : (
        <div className="grid gap-3">
          {data!.items.map((c) => <CorrectionCard key={c.id} correction={c} />)}
          {data!.totalPages > 1 && (
            <div className="flex items-center justify-end gap-2 text-sm text-ink-2">
              <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
              <span className="tabular">Page {page} of {data!.totalPages}</span>
              <Button size="sm" variant="secondary" disabled={page >= data!.totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
            </div>
          )}
        </div>
      )}
    </Page>
  );
}
