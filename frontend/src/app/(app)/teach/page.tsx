"use client";

import * as React from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ArrowRight, CalendarCheck2, CalendarOff, CalendarPlus, Check, CircleDashed, ClipboardPen, UserRoundX } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import {
  correctionStatusLabel,
  correctionStatusTone,
  formatClock,
  leaveStatusLabel,
  leaveStatusTone,
  staffAttendanceCode,
  staffAttendanceLabel,
  staffAttendanceTone,
  staffStatuses,
  useMyAttendance,
  useRequestStaffCorrection,
  useTeacherToday,
  type BellPeriod,
  type StaffAttendanceStatus,
  type TeacherRegisterTask,
  formatDays,
} from "@/lib/api/attendance";
import { ApiError } from "@/lib/api/client";
import { formatDate, formatTime, todayIso } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Page } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Skeleton, StatusBadge } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { SectionHeading } from "@/components/ui/record-list";
import { Field } from "@/components/ui/field";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { FormDialog } from "@/components/forms/form-dialog";
import { StaffLeaveDialog } from "@/components/attendance/staff-leave-dialog";

function greeting(now: Date) {
  const hour = now.getHours();
  return hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
}

/** The period running now (or next), from the campus's bell times. */
function currentPeriod(periods: BellPeriod[]) {
  const now = new Date();
  const hm = `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}:00`;
  const teaching = periods.filter((p) => p.number !== null);
  return teaching.find((p) => p.startTime <= hm && hm < p.endTime) ?? teaching.find((p) => p.startTime > hm) ?? null;
}

export default function TeacherHomePage() {
  const { me } = useSession();
  const today = useTeacherToday();
  const [leaveOpen, setLeaveOpen] = React.useState(false);
  const [correctionOpen, setCorrectionOpen] = React.useState(false);
  const t = today.data;

  return (
    <Page>
      <header className="grid gap-2 pt-8 pb-6">
        <h1 className="font-display text-3xl text-ink">{greeting(new Date())}, {me.user.firstName}</h1>
        {t ? (
          <p className="text-base text-ink-2">
            {formatDate(t.date, { weekday: true })}
            {t.staff && <> · {t.staff.designationName ?? "Staff"} · {t.staff.campusName}</>}
            {!t.day.isSchoolDay && <> · {t.day.closedReason}</>}
          </p>
        ) : <Skeleton className="h-5 w-72" />}
      </header>

      {today.isPending ? (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]"><Skeleton className="h-72" /><Skeleton className="h-72" /></div>
      ) : today.error ? (
        <ErrorState error={today.error} onRetry={() => void today.refetch()} />
      ) : !t!.staff ? (
        <div className="rounded-lg border border-line bg-surface">
          <EmptyState icon={<UserRoundX />} title="Your account isn't linked to a staff record"
            description="Registers, your attendance and leave appear here once the office links your sign-in to your staff record." />
        </div>
      ) : (
        <div className="grid gap-10 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
          <section className="grid content-start gap-3">
            <SectionHeading title="Today's registers"
              description={t!.mode === "PeriodWise" ? "Your class's roll call, and a register for each lesson you teach." : "Take the roll call once a day; mark only the exceptions."} />
            {!t!.day.isSchoolDay ? (
              <div className="flex items-center gap-3 rounded-lg border border-line bg-surface px-4 py-4 text-sm text-ink-2">
                <CalendarOff className="size-4 text-ink-3" /> {t!.day.closedReason}. No registers today.
              </div>
            ) : t!.registers.length === 0 ? (
              <div className="rounded-lg border border-line bg-surface">
                <EmptyState compact icon={<CalendarCheck2 />} title="No registers to take"
                  description="You aren't class teacher of a section this year. The office assigns class and subject teachers." />
              </div>
            ) : (
              <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
                {t!.registers.map((task) => <TaskRow key={`${task.sectionId}-${task.kind}-${task.subjectId ?? ""}`} task={task} date={t!.date} periods={t!.periods} />)}
              </ul>
            )}
            {t!.pendingLeave > 0 && (
              <Link href="/attendance/leave?status=Pending" className="flex items-center justify-between rounded-lg border border-line bg-surface px-4 py-3 text-sm hover:bg-sunken/60">
                <span className="text-ink">{t!.pendingLeave} leave application{t!.pendingLeave === 1 ? "" : "s"} for your class await a decision</span>
                <ArrowRight className="size-4 text-ink-3" />
              </Link>
            )}
          </section>
          <aside className="grid content-start gap-8">
            <section className="grid content-start gap-3">
              <SectionHeading title="My attendance" actions={
                <div className="flex gap-1.5">
                  <Button size="sm" variant="secondary" onClick={() => setLeaveOpen(true)}><CalendarPlus /> Request leave</Button>
                  <Button size="sm" variant="ghost" onClick={() => setCorrectionOpen(true)}><ClipboardPen /> Correct a day</Button>
                </div>
              } />
              <div className="flex items-center gap-3 rounded-lg border border-line bg-surface px-4 py-3">
                {t!.myAttendance?.status ? (
                  <>
                    <StatusBadge tone={staffAttendanceTone[t!.myAttendance.status]}>{staffAttendanceLabel[t!.myAttendance.status]} today</StatusBadge>
                    {t!.myAttendance.checkIn && <span className="text-sm text-ink-2 tabular">in {formatClock(t!.myAttendance.checkIn)}{t!.myAttendance.checkOut && ` · out ${formatClock(t!.myAttendance.checkOut)}`}</span>}
                  </>
                ) : (
                  <span className="inline-flex items-center gap-2 text-sm text-ink-2"><CircleDashed className="size-4 text-ink-3" /> The office hasn&apos;t marked today&apos;s staff register yet.</span>
                )}
              </div>
              <MyMonth />
            </section>
          </aside>
        </div>
      )}
      <StaffLeaveDialog open={leaveOpen} onOpenChange={setLeaveOpen} myStaffId={t?.staff?.staffId ?? null} canRecordForOthers={false} />
      <SelfCorrectionDialog open={correctionOpen} onOpenChange={setCorrectionOpen} />
    </Page>
  );
}

function TaskRow({ task, date, periods }: { task: TeacherRegisterTask; date: string; periods: BellPeriod[] }) {
  const now = task.kind === "period" ? currentPeriod(periods) : null;
  const period = task.kind === "period" ? (now?.number ?? task.period ?? 1) : null;
  const href = `/attendance/register/${task.sectionId}?date=${date}${period ? `&period=${period}${task.subjectId ? `&subject=${task.subjectId}` : ""}` : ""}`;
  return (
    <li className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3">
      <span className={cn("grid size-8 shrink-0 place-items-center rounded-full", task.taken ? "bg-success-soft text-[oklch(from_var(--success)_calc(l-0.06)_c_h)] dark:text-success" : "bg-sunken text-ink-3")}>
        {task.taken ? <Check className="size-4" /> : <CircleDashed className="size-4" />}
      </span>
      <div className="grid min-w-0 flex-1 leading-tight">
        <span className="font-medium text-ink">{task.className}{task.subjectName && <span className="font-normal text-ink-2"> · {task.subjectName}</span>}</span>
        <span className="text-sm text-ink-2">
          {task.kind === "daily" ? "Daily roll call" : task.taken ? `Last taken: period ${task.period}` : "Period register"}
          {task.taken && task.takenAt && ` · ${formatTime(task.takenAt)}`}
          {task.taken && task.absent !== null && ` · ${task.absent} absent of ${task.onRoll}`}
          {!task.taken && task.onRoll !== null && ` · ${task.onRoll} on roll`}
        </span>
      </div>
      <Button asChild size="sm" variant={task.taken ? "secondary" : "primary"}>
        <Link href={href}>{task.taken ? (task.kind === "daily" ? "Review" : `Take P${period}`) : task.kind === "daily" ? "Take register" : `Take P${period}`}</Link>
      </Button>
    </li>
  );
}

function MyMonth() {
  const today = todayIso();
  const mine = useMyAttendance({});
  if (mine.isPending) return <Skeleton className="h-48" />;
  if (mine.error) return mine.error instanceof ApiError && mine.error.status === 422 ? null : <ErrorState error={mine.error} />;
  const m = mine.data;
  const pad = (new Date(`${m.days[0]!.date}T12:00:00Z`).getUTCDay() + 6) % 7;
  return (
    <div className="grid gap-6">
      <div className="grid gap-2 rounded-lg border border-line bg-surface p-4">
        <div className="flex items-baseline justify-between text-sm">
          <span className="font-medium text-ink">{new Intl.DateTimeFormat("en-GB", { month: "long", year: "numeric", timeZone: "UTC" }).format(new Date(`${m.days[0]!.date}T12:00:00Z`))}</span>
          <span className="text-ink-2 tabular">{m.totals.absent} absent · {m.totals.late} late · {m.totals.onLeave} on leave</span>
        </div>
        <div className="grid grid-cols-7 gap-1">
          {["M", "T", "W", "T", "F", "S", "S"].map((d, i) => <span key={i} className="text-center text-2xs text-ink-3">{d}</span>)}
          {Array.from({ length: pad }, (_, i) => <span key={`p${i}`} />)}
          {m.days.map((d, i) => {
            const mark = m.marks[i];
            return (
              <span key={d.date} title={`${formatDate(d.date, { weekday: true })}${mark ? `: ${staffAttendanceLabel[mark]}` : d.closedReason ? `: ${d.closedReason}` : ""}`}
                className={cn("grid h-9 place-items-center rounded-md text-xs tabular",
                  !d.isSchoolDay ? "bg-sunken text-ink-3" : "border border-line text-ink-2",
                  mark === "Absent" && "border-transparent bg-danger-soft font-semibold text-danger",
                  mark === "OnLeave" && "border-transparent bg-info-soft font-medium text-info",
                  (mark === "Late" || mark === "HalfDay") && "border-transparent bg-warning-soft font-medium text-ink",
                  d.date === today && "ring-2 ring-[var(--focus)]")}>
                <span className="leading-none">{Number(d.date.slice(8))}</span>
                {mark && mark !== "Present" && <span className="text-[9px] leading-none">{staffAttendanceCode[mark]}</span>}
              </span>
            );
          })}
        </div>
      </div>
      <div className="grid gap-2">
        <h3 className="text-sm font-medium text-ink">Leave this year</h3>
        <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
          {m.balances.map((b) => (
            <li key={b.leaveTypeId} className="flex items-center justify-between gap-3 px-4 py-2 text-sm">
              <span className="text-ink">{b.name}</span>
              <span className="text-ink-2 tabular">{b.remaining === null ? `${formatDays(b.taken)} taken` : <><span className="font-medium text-ink">{formatDays(b.remaining)}</span> left of {b.allowance}</>}{b.pending > 0 && ` · ${formatDays(b.pending)} pending`}</span>
            </li>
          ))}
        </ul>
      </div>
      {(m.leave.length > 0 || m.corrections.length > 0) && (
        <div className="grid gap-2">
          <h3 className="text-sm font-medium text-ink">Your requests</h3>
          <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
            {m.leave.slice(0, 5).map((l) => (
              <li key={l.id} className="flex items-center justify-between gap-3 px-4 py-2 text-sm">
                <span className="grid min-w-0 leading-tight">
                  <span className="text-ink">{l.leaveTypeName} · {formatDays(l.days)}</span>
                  <span className="truncate text-xs text-ink-2">{formatDate(l.fromDate)}{l.toDate !== l.fromDate && ` – ${formatDate(l.toDate)}`}{l.decisionNote && ` · “${l.decisionNote}”`}</span>
                </span>
                <StatusBadge tone={leaveStatusTone[l.status]}>{leaveStatusLabel[l.status]}</StatusBadge>
              </li>
            ))}
            {m.corrections.slice(0, 5).map((c) => (
              <li key={c.id} className="flex items-center justify-between gap-3 px-4 py-2 text-sm">
                <span className="grid min-w-0 leading-tight">
                  <span className="text-ink">Correction · {formatDate(c.date)}</span>
                  <span className="truncate text-xs text-ink-2">{c.fromStatus ? staffAttendanceLabel[c.fromStatus] : "Not marked"} → {staffAttendanceLabel[c.toStatus]}</span>
                </span>
                <StatusBadge tone={correctionStatusTone[c.status]}>{correctionStatusLabel[c.status]}</StatusBadge>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

const correctionSchema = z.object({
  date: z.string().min(1, "Choose the day."),
  status: z.enum(staffStatuses as [StaffAttendanceStatus, ...StaffAttendanceStatus[]]),
  checkIn: z.string(),
  reason: z.string().trim().min(5, "Explain what happened (e.g. exam duty, training).").max(1000),
});
type CorrectionValues = z.infer<typeof correctionSchema>;

function SelfCorrectionDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const request = useRequestStaffCorrection();
  const form = useForm<CorrectionValues>({ resolver: zodResolver(correctionSchema) });
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="sm" title="Correct a day"
      description="If the staff register is wrong about you, ask for it to be corrected. Someone else approves it."
      defaults={{ date: todayIso(), status: "OnDuty" as StaffAttendanceStatus, checkIn: "", reason: "" }} submitLabel="Send request" successMessage="Correction requested"
      onSubmit={(v) => request.mutateAsync({ staffId: null, date: v.date, status: v.status, checkIn: v.checkIn ? `${v.checkIn}:00` : null, checkOut: null, reason: v.reason })}>
      <Field label="Day" htmlFor="sc-date" error={form.formState.errors.date?.message}><Input id="sc-date" type="date" max={todayIso()} {...form.register("date")} /></Field>
      <Field label="It should say" htmlFor="sc-status">
        <NativeSelect id="sc-status" {...form.register("status")}>{staffStatuses.map((s) => <option key={s} value={s}>{staffAttendanceLabel[s]}</option>)}</NativeSelect>
      </Field>
      <Field label="Arrived at" htmlFor="sc-in" optional><Input id="sc-in" type="time" className="max-w-36" {...form.register("checkIn")} /></Field>
      <Field label="What happened" htmlFor="sc-reason" error={form.formState.errors.reason?.message}><Textarea id="sc-reason" rows={3} {...form.register("reason")} /></Field>
    </FormDialog>
  );
}
