"use client";

import * as React from "react";
import Link from "next/link";
import { Plus } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import {
  attendanceCode,
  attendanceLabel,
  formatClock,
  leaveKindLabel,
  leaveStatusLabel,
  leaveStatusTone,
  useStudentAttendance,
  type AttendanceStatus,
  type StudentAttendanceSummary,
} from "@/lib/api/attendance";
import type { StudentDetail } from "@/lib/api/students";
import { formatDate, formatPercent, todayIso } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Skeleton, StatusBadge } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { SectionHeading } from "@/components/ui/record-list";
import { Tooltip } from "@/components/ui/menu";
import { RecordStudentLeaveDialog } from "@/components/attendance/student-leave-dialogs";

const monthName = new Intl.DateTimeFormat("en-GB", { month: "short", timeZone: "UTC" });

const cell: Record<AttendanceStatus, string> = {
  Present: "bg-[color-mix(in_oklch,var(--success)_26%,var(--surface))]",
  Late: "bg-warning",
  Absent: "bg-danger",
  Leave: "bg-info",
};

/** A student's year at a glance: the percentage against the school's minimum, each month, every day, leave. */
export function AttendanceTab({ student }: { student: StudentDetail }) {
  const { can } = useSession();
  const summary = useStudentAttendance(student.id);
  const [recording, setRecording] = React.useState(false);
  if (summary.isPending) return <div className="grid gap-4"><Skeleton className="h-24" /><Skeleton className="h-48" /></div>;
  if (summary.error) return <ErrorState error={summary.error} onRetry={() => void summary.refetch()} />;
  const a = summary.data;
  const t = a.totals;
  const below = t.percentage !== null && t.percentage < a.minimumPercentage;

  return (
    <div className="grid gap-10">
      <dl className="grid grid-cols-2 border-y border-line sm:grid-cols-3 lg:grid-cols-6">
        {[
          { label: "Attendance", value: formatPercent(t.percentage), hint: below ? `Below the ${a.minimumPercentage}% minimum` : `Minimum ${a.minimumPercentage}%`, danger: below },
          { label: "School days", value: t.days, hint: `${a.academicYearName}, to ${formatDate(a.to)}` },
          { label: "Present", value: t.present },
          { label: "Late", value: t.late },
          { label: "Absent", value: t.absent, danger: t.absent > 0 && below },
          { label: "Leave", value: t.leave, hint: a.leaveCountsAsPresent ? "Counted as present" : "Left out of the percentage" },
        ].map((f, i) => (
          <div key={f.label} className={cn("grid content-start gap-1 py-5 pr-4", i > 0 && "lg:border-l lg:border-line lg:pl-5")}>
            <dt className="text-sm text-ink-2">{f.label}</dt>
            <dd className={cn("font-display text-3xl tabular", f.danger ? "text-danger" : "text-ink")}>{f.value}</dd>
            {f.hint && <dd className={cn("text-xs", f.danger ? "text-danger" : "text-ink-3")}>{f.hint}</dd>}
          </div>
        ))}
      </dl>

      <div className="grid gap-10 lg:grid-cols-[minmax(0,1.5fr)_minmax(0,1fr)]">
        <section className="grid content-start gap-3">
          <SectionHeading title="Every school day" description="Each square is a day with a register; hover for the details." />
          <YearGrid summary={a} />
        </section>
        <section className="grid content-start gap-3">
          <SectionHeading title="By month" />
          <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
            {a.months.map((m) => (
              <li key={`${m.year}-${m.month}`} className="grid grid-cols-[3rem_minmax(0,1fr)_3.5rem] items-center gap-3 px-4 py-2.5 text-sm">
                <span className="text-ink-2">{monthName.format(new Date(Date.UTC(m.year, m.month - 1, 1)))}</span>
                <span className="relative h-1.5 overflow-hidden rounded-full bg-line" aria-hidden>
                  <span className={cn("absolute inset-y-0 left-0 rounded-full", (m.totals.percentage ?? 100) < a.minimumPercentage ? "bg-danger" : "bg-[var(--chart-line)]")}
                    style={{ width: `${m.totals.percentage ?? 0}%` }} />
                  <span className="absolute inset-y-0 w-px bg-ink-3" style={{ left: `${a.minimumPercentage}%` }} />
                </span>
                <span className="text-right font-medium text-ink tabular" title={`${m.totals.absent} absent, ${m.totals.late} late, ${m.totals.leave} leave`}>{formatPercent(m.totals.percentage, 0)}</span>
              </li>
            ))}
            {a.months.length === 0 && <li className="px-4 py-6 text-center text-sm text-ink-2">No registers yet this year.</li>}
          </ul>
          {a.subjects.length > 0 && (
            <>
              <SectionHeading title="By subject" description="From period registers." />
              <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
                {a.subjects.map((s) => (
                  <li key={s.subjectId} className="flex items-center justify-between gap-3 px-4 py-2 text-sm">
                    <span className="text-ink">{s.subjectName}</span>
                    <span className="text-ink-2 tabular">{s.attended} of {s.periods} periods · <span className="font-medium text-ink">{formatPercent(s.percentage, 0)}</span></span>
                  </li>
                ))}
              </ul>
            </>
          )}
        </section>
      </div>

      <div className="grid gap-10 lg:grid-cols-2">
        <section className="grid content-start gap-3">
          <SectionHeading title="Days missed" description="Absences, late arrivals and leave, most recent first." />
          {(() => {
            const missed = [...a.days].filter((d) => d.status !== "Present").reverse().slice(0, 12);
            return missed.length === 0 ? <p className="rounded-lg border border-line bg-surface px-4 py-6 text-center text-sm text-ink-2">Present every day so far.</p> : (
              <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
                {missed.map((d) => (
                  <li key={d.date} className="flex items-center justify-between gap-3 px-4 py-2 text-sm">
                    <span className="text-ink tabular">{formatDate(d.date, { weekday: true })}</span>
                    <span className="min-w-0 flex-1 truncate text-right text-ink-2">{d.remark}{d.status === "Late" && d.arrivedAt && ` · arrived ${formatClock(d.arrivedAt)}`}</span>
                    <StatusBadge tone={d.status === "Absent" ? "danger" : d.status === "Late" ? "warning" : "info"}>{attendanceLabel[d.status]}</StatusBadge>
                  </li>
                ))}
              </ul>
            );
          })()}
        </section>
        <section className="grid content-start gap-3">
          <SectionHeading title="Leave" actions={can(P.Attendance.ManageLeave) && <Button size="sm" variant="secondary" onClick={() => setRecording(true)}><Plus /> Record leave</Button>} />
          {a.leave.length === 0 ? <p className="rounded-lg border border-line bg-surface px-4 py-6 text-center text-sm text-ink-2">No leave this year.</p> : (
            <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
              {a.leave.map((l) => (
                <li key={l.id} className="flex items-center justify-between gap-3 px-4 py-2.5 text-sm">
                  <span className="grid min-w-0 leading-tight">
                    <span className="text-ink tabular">{formatDate(l.fromDate)}{l.toDate !== l.fromDate && ` – ${formatDate(l.toDate)}`} · {l.schoolDays} day{l.schoolDays === 1 ? "" : "s"}</span>
                    <span className="truncate text-xs text-ink-2">{leaveKindLabel[l.kind]} · {l.reason}</span>
                  </span>
                  <StatusBadge tone={leaveStatusTone[l.status]}>{leaveStatusLabel[l.status]}</StatusBadge>
                </li>
              ))}
            </ul>
          )}
          <Link href={`/attendance/leave?status=all`} className="text-sm font-medium text-accent hover:underline">All student leave</Link>
        </section>
      </div>
      <RecordStudentLeaveDialog open={recording} onOpenChange={setRecording}
        student={{ id: student.id, fullName: student.fullName, admissionNumber: student.admissionNumber, className: student.currentEnrollment?.className ?? null, photoFileId: student.photoFileId }} />
    </div>
  );
}

/** Weeks as columns, Monday–Saturday as rows: the shape of a paper register. */
function YearGrid({ summary }: { summary: StudentAttendanceSummary }) {
  const byDate = new Map(summary.days.map((d) => [d.date, d]));
  const start = new Date(`${summary.from}T12:00:00Z`);
  start.setUTCDate(start.getUTCDate() - ((start.getUTCDay() + 6) % 7));
  const end = new Date(`${summary.to}T12:00:00Z`);
  const weeks: Date[][] = [];
  for (let d = new Date(start); d <= end; d.setUTCDate(d.getUTCDate() + 7)) {
    weeks.push(Array.from({ length: 6 }, (_, i) => new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate() + i, 12))));
  }
  const today = todayIso();
  return (
    <div className="grid gap-2 rounded-lg border border-line bg-surface p-4">
      <div className="flex gap-[3px] overflow-x-auto pb-1">
        <div className="grid shrink-0 grid-rows-6 gap-[3px] pr-1 text-2xs leading-[14px] text-ink-3">
          {["Mon", "", "Wed", "", "Fri", ""].map((d, i) => <span key={i}>{d}</span>)}
        </div>
        {weeks.map((week) => (
          <div key={week[0]!.toISOString()} className="grid shrink-0 grid-rows-6 gap-[3px]">
            {week.map((day) => {
              const iso = day.toISOString().slice(0, 10);
              const mark = byDate.get(iso);
              const outside = iso < summary.from || iso > summary.to;
              return mark ? (
                <Tooltip key={iso} content={`${formatDate(iso, { weekday: true })}: ${attendanceLabel[mark.status]}${mark.remark ? ` — ${mark.remark}` : ""}`}>
                  <span tabIndex={0} aria-label={`${formatDate(iso)}: ${attendanceLabel[mark.status]}`}
                    className={cn("block size-[14px] rounded-[3px] outline-none focus-visible:ring-2 focus-visible:ring-[var(--focus)]", cell[mark.status], iso === today && "ring-1 ring-ink")} />
                </Tooltip>
              ) : (
                <span key={iso} className={cn("block size-[14px] rounded-[3px]", outside ? "" : "bg-sunken")} />
              );
            })}
          </div>
        ))}
      </div>
      <p className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-ink-2">
        {(["Present", "Late", "Absent", "Leave"] as AttendanceStatus[]).map((s) => (
          <span key={s} className="inline-flex items-center gap-1.5"><span className={cn("size-2.5 rounded-[2px]", cell[s])} /> {attendanceLabel[s]} ({attendanceCode[s]})</span>
        ))}
        <span className="inline-flex items-center gap-1.5"><span className="size-2.5 rounded-[2px] bg-sunken" /> No register</span>
      </p>
    </div>
  );
}
