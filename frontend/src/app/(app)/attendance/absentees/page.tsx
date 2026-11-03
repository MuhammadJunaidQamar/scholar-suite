"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { MessageSquareOff, Phone, UserRoundCheck } from "lucide-react";
import {
  attendanceLabel,
  attendanceTone,
  channelLabel,
  formatClock,
  messageStatusLabel,
  messageStatusTone,
  noProviderPhrase,
  useAbsentees,
  type Absentee,
} from "@/lib/api/attendance";
import { formatPhone } from "@/lib/api/students";
import { todayIso } from "@/lib/format";
import { Page, PageHeader } from "@/components/shell/page";
import { Skeleton, StatusBadge } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Segmented } from "@/components/ui/toggles";
import { Tooltip } from "@/components/ui/menu";
import { RecordAvatar } from "@/components/records/record-avatar";
import { AttendanceNav } from "@/components/attendance/attendance-nav";
import { DayStepper } from "@/components/attendance/day-stepper";

/** The office's call list: who is absent or late, the family's number, and what happened to the automatic alert. */
export default function AbsenteesPage() {
  const search = useSearchParams();
  const router = useRouter();
  const date = search.get("date") ?? todayIso();
  const campusId = search.get("campus") ?? undefined;
  const absentees = useAbsentees({ date, campusId });
  const [show, setShow] = React.useState<"Absent" | "Late" | "all">("Absent");

  const list = (absentees.data ?? []).filter((a) => show === "all" || a.status === show);
  const unsent = (absentees.data ?? []).flatMap((a) => a.alerts).filter((m) => m.status === "NotSent");
  const absentCount = (absentees.data ?? []).filter((a) => a.status === "Absent").length;
  const lateCount = (absentees.data ?? []).filter((a) => a.status === "Late").length;

  return (
    <Page>
      <PageHeader
        breadcrumbs={[{ label: "Attendance", href: "/attendance" }, { label: "Call list" }]}
        title="Call list"
        description="Students marked absent or late, with the family's number and what happened to the automatic alert."
        actions={<DayStepper value={date} onChange={(d) => router.replace(`/attendance/absentees?date=${d}${campusId ? `&campus=${campusId}` : ""}`)} />}
      />
      <AttendanceNav className="mb-6" />
      {unsent.length > 0 && (
        <p className="mb-4 flex items-start gap-2 rounded-lg border border-line bg-surface px-4 py-3 text-sm text-ink-2">
          <MessageSquareOff className="mt-0.5 size-4 shrink-0 text-ink-3" />
          <span>
            {unsent.length} alert{unsent.length === 1 ? " was" : "s were"} recorded but not sent — {noProviderPhrase(unsent.map((m) => m.channel))}. Families on this
            list have not been told automatically; call the ones who need to know.
          </span>
        </p>
      )}
      <div className="rounded-lg border border-line bg-surface">
        <div className="flex flex-wrap items-center gap-3 border-b border-line px-4 py-2.5">
          <Segmented size="sm" aria-label="Show" value={show} onChange={setShow}
            options={[{ value: "Absent", label: `Absent · ${absentCount}` }, { value: "Late", label: `Late · ${lateCount}` }, { value: "all", label: "Both" }]} />
        </div>
        {absentees.isPending ? (
          <div className="grid gap-2 p-4">{[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-12" />)}</div>
        ) : absentees.error ? (
          <ErrorState error={absentees.error} onRetry={() => void absentees.refetch()} />
        ) : list.length === 0 ? (
          <EmptyState compact icon={<UserRoundCheck />} title={show === "Late" ? "Nobody was late" : "Nobody is absent"}
            description="Students appear here once their class's register has been taken." />
        ) : (
          <ul className="divide-y divide-line">{list.map((a) => <AbsenteeRow key={a.studentId} absentee={a} date={date} />)}</ul>
        )}
      </div>
    </Page>
  );
}

function AbsenteeRow({ absentee: a, date }: { absentee: Absentee; date: string }) {
  // One line per outcome ("SMS, WhatsApp · not sent") instead of a chip per message.
  const outcomes = Object.entries(a.alerts.reduce<Record<string, { channels: string[]; detail: string | null }>>((acc, m) => {
    (acc[m.status] ??= { channels: [], detail: m.detail }).channels.push(channelLabel[m.channel]);
    return acc;
  }, {})) as [Absentee["alerts"][number]["status"], { channels: string[]; detail: string | null }][];
  return (
    <li className="grid items-center gap-x-6 gap-y-2 px-4 py-3 md:grid-cols-[minmax(0,1.3fr)_minmax(0,0.8fr)_minmax(0,1fr)_minmax(0,1fr)]">
      <div className="flex min-w-0 items-center gap-3">
        <RecordAvatar name={a.fullName} fileId={a.photoFileId} size={32} />
        <div className="grid min-w-0 leading-tight">
          <span className="flex min-w-0 flex-wrap items-center gap-1.5">
            <Link href={`/students/${a.studentId}?tab=attendance`} className="truncate font-medium text-ink hover:underline">{a.fullName}</Link>
            {a.consecutiveAbsences >= 2 && (
              <StatusBadge tone={a.consecutiveAbsences >= 3 ? "danger" : "warning"} variant="soft">{a.consecutiveAbsences} days in a row</StatusBadge>
            )}
          </span>
          <span className="truncate text-xs text-ink-2">
            <Link href={`/attendance/register/${a.sectionId}?date=${date}`} className="hover:underline">{a.className}</Link>
            <span className="font-mono"> · {a.admissionNumber}</span>
          </span>
        </div>
      </div>
      <div className="grid min-w-0 leading-tight">
        <StatusBadge tone={attendanceTone[a.status]}>
          {attendanceLabel[a.status]}{a.status === "Late" && a.arrivedAt ? ` · ${formatClock(a.arrivedAt)}` : ""}
        </StatusBadge>
        {a.remark && <span className="truncate pl-3 text-xs text-ink-2">{a.remark}</span>}
      </div>
      <div className="grid min-w-0 leading-tight">
        <span className="truncate text-sm text-ink">{a.guardianName ?? <span className="text-ink-3">No guardian on file</span>}</span>
        {a.guardianPhone && (
          <a href={`tel:${a.guardianPhone}`} className="inline-flex items-center gap-1 text-sm whitespace-nowrap text-accent tabular hover:underline">
            <Phone className="size-3.5 shrink-0" /> {formatPhone(a.guardianPhone)}
          </a>
        )}
      </div>
      <div className="grid min-w-0 gap-0.5 text-xs leading-tight">
        {outcomes.length === 0 ? <span className="text-ink-3">No alert</span> : outcomes.map(([status, o]) => (
          <Tooltip key={status} content={o.detail ?? messageStatusLabel[status]}>
            <span className="w-fit"><StatusBadge tone={messageStatusTone[status]} className="text-xs text-ink-2">{summarise(o.channels)} · {messageStatusLabel[status].toLowerCase()}</StatusBadge></span>
          </Tooltip>
        ))}
      </div>
    </li>
  );
}

/** ["SMS", "SMS", "WhatsApp"] → "2 SMS, WhatsApp" */
function summarise(channels: string[]) {
  const counts = channels.reduce<Record<string, number>>((acc, c) => ({ ...acc, [c]: (acc[c] ?? 0) + 1 }), {});
  return Object.entries(counts).map(([c, n]) => (n > 1 ? `${n} ${c}` : c)).join(", ");
}
