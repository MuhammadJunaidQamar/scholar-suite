"use client";

import * as React from "react";
import Link from "next/link";
import { CalendarClock, Plus } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import {
  leaveStatusLabel,
  leaveStatusTone,
  useCancelStaffLeave,
  useDecideStaffLeave,
  useStaffLeave,
  useTeacherToday,
  type LeaveStatus,
  type StaffLeave,
  formatDays,
} from "@/lib/api/attendance";
import { formatDate, formatDateTime } from "@/lib/format";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Badge, Skeleton, StatusBadge } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Segmented } from "@/components/ui/toggles";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/toaster";
import { TableSearch } from "@/components/data-table/toolbar";
import { AttendanceNav } from "@/components/attendance/attendance-nav";
import { StaffLeaveDialog } from "@/components/attendance/staff-leave-dialog";

export default function StaffLeavePage() {
  const { can } = useSession();
  const approver = can(P.Hr.ApproveLeave);
  const me = useTeacherToday();
  const [status, setStatus] = React.useState<LeaveStatus | "all">("Pending");
  const [term, setTerm] = React.useState<string | undefined>();
  const [page, setPage] = React.useState(1);
  const [requesting, setRequesting] = React.useState(false);
  const leave = useStaffLeave({ status: status === "all" ? undefined : status, search: term, page, pageSize: 25 });
  const data = leave.data;
  const myStaffId = me.data?.staff?.staffId ?? null;

  return (
    <Page>
      <PageHeader
        title="Staff leave"
        description="Leave requests from staff and leave recorded for them. Nobody decides their own; approved days show on the staff register."
        actions={(approver || myStaffId) && <Button variant="primary" onClick={() => setRequesting(true)}><Plus /> {approver ? "Request or record leave" : "Request leave"}</Button>}
      />
      <AttendanceNav className="mb-6" />
      <div className="rounded-lg border border-line bg-surface">
        <div className="flex flex-wrap items-center gap-3 border-b border-line px-4 py-2.5">
          <Segmented size="sm" aria-label="Show" value={status} onChange={(v) => { setStatus(v); setPage(1); }}
            options={[{ value: "Pending", label: "Awaiting decision" }, { value: "Approved", label: "Approved" }, { value: "Rejected", label: "Rejected" }, { value: "all", label: "All" }]} />
          <TableSearch value={term} onChange={(v) => { setTerm(v); setPage(1); }} placeholder="Name or employee no." />
          {data && <span className="ml-auto text-sm text-ink-2 tabular">{data.total} {data.total === 1 ? "request" : "requests"}</span>}
        </div>
        {leave.isPending ? (
          <div className="grid gap-2 p-4">{[0, 1, 2].map((i) => <Skeleton key={i} className="h-14" />)}</div>
        ) : leave.error ? (
          <ErrorState error={leave.error} onRetry={() => void leave.refetch()} />
        ) : data!.items.length === 0 ? (
          <EmptyState compact icon={<CalendarClock />} title={status === "Pending" ? "Nothing waiting for a decision" : "No leave here"} />
        ) : (
          <ul className="divide-y divide-line">{data!.items.map((l) => <StaffLeaveRow key={l.id} leave={l} />)}</ul>
        )}
        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-end gap-2 border-t border-line px-4 py-2.5 text-sm text-ink-2">
            <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
            <span className="tabular">Page {page} of {data.totalPages}</span>
            <Button size="sm" variant="secondary" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
          </div>
        )}
      </div>
      <StaffLeaveDialog open={requesting} onOpenChange={setRequesting} myStaffId={myStaffId} canRecordForOthers={approver} />
    </Page>
  );
}

function StaffLeaveRow({ leave: l }: { leave: StaffLeave }) {
  const decide = useDecideStaffLeave();
  const cancel = useCancelStaffLeave();
  const [dialog, setDialog] = React.useState<"reject" | "cancel" | null>(null);
  const [note, setNote] = React.useState("");
  const range = l.halfDay ? `${formatDate(l.fromDate, { weekday: true })} (half day)`
    : l.fromDate === l.toDate ? formatDate(l.fromDate, { weekday: true }) : `${formatDate(l.fromDate)} – ${formatDate(l.toDate)}`;

  const run = async (action: () => Promise<unknown>, done: string) => {
    try {
      await action();
      toast.success(done);
      setDialog(null);
      setNote("");
    } catch (error) {
      toast.error(error);
    }
  };

  return (
    <li className="grid items-center gap-x-4 gap-y-2 px-4 py-3 md:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)_minmax(0,1.4fr)_auto]">
      <div className="grid min-w-0 leading-tight">
        <Link href={`/staff/${l.staffId}`} className="truncate font-medium text-ink hover:underline">{l.staffName}</Link>
        <span className="truncate text-xs text-ink-2">{l.designationName ?? "—"} · <span className="font-mono">{l.employeeNumber}</span></span>
      </div>
      <div className="grid leading-tight">
        <span className="text-sm text-ink tabular">{range}</span>
        <span className="text-xs text-ink-2">{formatDays(l.days)} · {l.leaveTypeName}</span>
      </div>
      <div className="grid min-w-0 gap-0.5 leading-tight">
        <span className="flex min-w-0 items-center gap-1.5">
          <Badge>{l.leaveTypeCode}</Badge>
          <span className="truncate text-sm text-ink">{l.reason}</span>
        </span>
        <span className="truncate text-xs text-ink-3">
          {l.requestedBySelf ? "Requested by themselves" : `Recorded by ${l.requestedByName ?? "—"}`} · {formatDateTime(l.requestedAt)}
          {l.decidedByName && ` · decided by ${l.decidedByName}`}
        </span>
        {l.decisionNote && <span className="truncate text-xs text-ink-2">“{l.decisionNote}”</span>}
      </div>
      <div className="flex items-center justify-end gap-2">
        {l.canDecide ? (
          <>
            <Button size="sm" variant="ghost" onClick={() => setDialog("reject")}>Reject…</Button>
            <Button size="sm" variant="primary" loading={decide.isPending && dialog === null}
              onClick={() => void run(() => decide.mutateAsync({ id: l.id, approve: true }), "Leave approved")}>Approve</Button>
          </>
        ) : (
          <StatusBadge tone={leaveStatusTone[l.status]}>{leaveStatusLabel[l.status]}</StatusBadge>
        )}
        {l.canCancel && <Button size="sm" variant="ghost" onClick={() => setDialog("cancel")}>{l.status === "Pending" ? "Withdraw…" : "Cancel…"}</Button>}
      </div>
      <ConfirmDialog open={dialog !== null} onOpenChange={(o) => !o && setDialog(null)} tone="danger"
        title={dialog === "reject" ? `Reject ${l.staffName}'s leave?` : l.status === "Pending" ? "Withdraw this request?" : `Cancel ${l.staffName}'s leave?`}
        description={dialog === "reject" ? "Your note is shown to them." : l.status === "Approved" ? "Days already marked On leave go back to Absent." : undefined}
        confirmLabel={dialog === "reject" ? "Reject leave" : l.status === "Pending" ? "Withdraw request" : "Cancel leave"}
        loading={decide.isPending || cancel.isPending}
        onConfirm={() => run(() => (dialog === "reject" ? decide.mutateAsync({ id: l.id, approve: false, note }) : cancel.mutateAsync({ id: l.id, note: note || null })),
          dialog === "reject" ? "Leave rejected" : "Leave cancelled")}>
        <Input autoFocus value={note} onChange={(e) => setNote(e.target.value)} className="mt-2" aria-label="Reason"
          placeholder={dialog === "reject" ? "Reason (required)" : "Reason (optional)"} />
      </ConfirmDialog>
    </li>
  );
}
