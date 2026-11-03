"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { CalendarClock, Plus } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import {
  leaveKindLabel,
  leaveStatusLabel,
  leaveStatusTone,
  useCancelStudentLeave,
  useDecideStudentLeave,
  useStudentLeave,
  type LeaveStatus,
  type StudentLeave,
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
import { RecordAvatar } from "@/components/records/record-avatar";
import { AttendanceNav } from "@/components/attendance/attendance-nav";
import { RecordStudentLeaveDialog } from "@/components/attendance/student-leave-dialogs";

type Filter = LeaveStatus | "all";

export default function StudentLeavePage() {
  const { can } = useSession();
  const search = useSearchParams();
  const router = useRouter();
  const status = (search.get("status") as Filter | null) ?? "Pending";
  const [term, setTerm] = React.useState<string | undefined>(undefined);
  const [page, setPage] = React.useState(1);
  const [recording, setRecording] = React.useState(false);
  const leave = useStudentLeave({ status: status === "all" ? undefined : status, search: term, page, pageSize: 25 });
  const manage = can(P.Attendance.ManageLeave);
  const data = leave.data;

  return (
    <Page>
      <PageHeader
        title="Student leave"
        description="Leave applications from families. Approving leave excuses the days it covers — including absences already recorded."
        actions={manage && <Button variant="primary" onClick={() => setRecording(true)}><Plus /> Record leave</Button>}
      />
      <AttendanceNav className="mb-6" />
      <div className="rounded-lg border border-line bg-surface">
        <div className="flex flex-wrap items-center gap-3 border-b border-line px-4 py-2.5">
          <Segmented size="sm" aria-label="Show" value={status}
            onChange={(v) => { setPage(1); router.replace(`/attendance/leave?status=${v}`); }}
            options={[{ value: "Pending", label: "Awaiting decision" }, { value: "Approved", label: "Approved" }, { value: "Rejected", label: "Rejected" }, { value: "all", label: "All" }]} />
          <TableSearch value={term} onChange={(v) => { setTerm(v); setPage(1); }} placeholder="Student name or admission no." />
          {data && <span className="ml-auto text-sm text-ink-2 tabular">{data.total} {data.total === 1 ? "application" : "applications"}</span>}
        </div>
        {leave.isPending ? (
          <div className="grid gap-2 p-4">{[0, 1, 2].map((i) => <Skeleton key={i} className="h-14" />)}</div>
        ) : leave.error ? (
          <ErrorState error={leave.error} onRetry={() => void leave.refetch()} />
        ) : data!.items.length === 0 ? (
          <EmptyState compact icon={<CalendarClock />} title={status === "Pending" ? "Nothing waiting for a decision" : "No leave here"}
            description="Leave the office records appears here; approved leave shows on the registers it covers."
            action={manage && <Button onClick={() => setRecording(true)}><Plus /> Record leave</Button>} />
        ) : (
          <ul className="divide-y divide-line">{data!.items.map((l) => <LeaveRow key={l.id} leave={l} manage={manage} />)}</ul>
        )}
        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-end gap-2 border-t border-line px-4 py-2.5 text-sm text-ink-2">
            <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
            <span className="tabular">Page {page} of {data.totalPages}</span>
            <Button size="sm" variant="secondary" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
          </div>
        )}
      </div>
      <RecordStudentLeaveDialog open={recording} onOpenChange={setRecording} />
    </Page>
  );
}

function LeaveRow({ leave: l, manage }: { leave: StudentLeave; manage: boolean }) {
  const decide = useDecideStudentLeave();
  const cancel = useCancelStudentLeave();
  const [dialog, setDialog] = React.useState<"reject" | "cancel" | null>(null);
  const [note, setNote] = React.useState("");
  const range = l.fromDate === l.toDate ? formatDate(l.fromDate, { weekday: true }) : `${formatDate(l.fromDate, { weekday: true })} – ${formatDate(l.toDate, { weekday: true })}`;

  const approve = async () => {
    try {
      const result = await decide.mutateAsync({ id: l.id, approve: true });
      toast.success("Leave approved", result.marksChanged > 0 ? `${result.marksChanged} recorded absence${result.marksChanged === 1 ? " was" : "s were"} changed to Leave.` : undefined);
    } catch (error) {
      toast.error(error, "The leave couldn't be approved");
    }
  };

  const confirm = async () => {
    try {
      if (dialog === "reject") {
        await decide.mutateAsync({ id: l.id, approve: false, note });
        toast.success("Leave rejected");
      } else {
        const result = await cancel.mutateAsync({ id: l.id, note: note || null });
        toast.success("Leave cancelled", result.marksChanged > 0 ? `${result.marksChanged} day${result.marksChanged === 1 ? "" : "s"} went back to Absent.` : undefined);
      }
      setDialog(null);
      setNote("");
    } catch (error) {
      toast.error(error);
    }
  };

  return (
    <li className="grid items-center gap-x-4 gap-y-2 px-4 py-3 md:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)_minmax(0,1.3fr)_auto]">
      <div className="flex min-w-0 items-center gap-3">
        <RecordAvatar name={l.studentName} fileId={l.photoFileId} size={32} />
        <div className="grid min-w-0 leading-tight">
          <Link href={`/students/${l.studentId}?tab=attendance`} className="truncate font-medium text-ink hover:underline">{l.studentName}</Link>
          <span className="truncate text-xs text-ink-2">{l.className ?? "—"} · <span className="font-mono">{l.admissionNumber}</span></span>
        </div>
      </div>
      <div className="grid leading-tight">
        <span className="text-sm text-ink tabular">{range}</span>
        <span className="text-xs text-ink-2">{l.schoolDays} school day{l.schoolDays === 1 ? "" : "s"}</span>
      </div>
      <div className="grid min-w-0 gap-0.5 leading-tight">
        <span className="flex min-w-0 items-center gap-1.5">
          <Badge>{leaveKindLabel[l.kind]}</Badge>
          <span className="truncate text-sm text-ink">{l.reason}</span>
        </span>
        <span className="truncate text-xs text-ink-3">
          {l.requestedBy ? `${l.requestedBy} · ` : ""}recorded {formatDateTime(l.recordedAt)}{l.recordedByName ? ` by ${l.recordedByName}` : ""}
        </span>
        {l.decisionNote && <span className="truncate text-xs text-ink-2">“{l.decisionNote}”</span>}
      </div>
      <div className="flex items-center justify-end gap-2">
        {l.status === "Pending" && manage ? (
          <>
            <Button size="sm" variant="ghost" onClick={() => setDialog("reject")}>Reject…</Button>
            <Button size="sm" variant="primary" loading={decide.isPending} onClick={() => void approve()}>Approve</Button>
          </>
        ) : (
          <>
            <StatusBadge tone={leaveStatusTone[l.status]}>{leaveStatusLabel[l.status]}</StatusBadge>
            {l.status === "Approved" && manage && <Button size="sm" variant="ghost" onClick={() => setDialog("cancel")}>Cancel…</Button>}
          </>
        )}
      </div>
      <ConfirmDialog open={dialog !== null} onOpenChange={(o) => !o && setDialog(null)} tone="danger"
        title={dialog === "reject" ? `Reject ${l.studentName}'s leave?` : `Cancel ${l.studentName}'s leave?`}
        description={dialog === "reject" ? "The days will count as absences. The family is not told automatically." : "Days the register marked as Leave because of it go back to Absent."}
        confirmLabel={dialog === "reject" ? "Reject leave" : "Cancel leave"} loading={decide.isPending || cancel.isPending} onConfirm={confirm}>
        <Input autoFocus value={note} onChange={(e) => setNote(e.target.value)} className="mt-2"
          placeholder={dialog === "reject" ? "Reason (required)" : "Reason (optional)"} aria-label="Reason" />
      </ConfirmDialog>
    </li>
  );
}
