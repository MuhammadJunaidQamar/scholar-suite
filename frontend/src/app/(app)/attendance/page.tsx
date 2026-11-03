"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ArrowRight, CalendarOff, ClipboardCheck, MessageSquareOff, PhoneCall } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCampuses } from "@/lib/api/access";
import { noProviderPhrase, useAbsentees, useAttendanceOverview, type AttendanceOverview, type SectionDay } from "@/lib/api/attendance";
import { formatNumber, formatPercent, formatTime, todayIso } from "@/lib/format";
import { formatPhone } from "@/lib/api/students";
import { cn } from "@/lib/utils";
import { Page, PageHeader } from "@/components/shell/page";
import { Skeleton, StatusBadge } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { NativeSelect } from "@/components/ui/input";
import { Segmented } from "@/components/ui/toggles";
import { RecordAvatar } from "@/components/records/record-avatar";
import { AttendanceNav } from "@/components/attendance/attendance-nav";
import { DayStepper } from "@/components/attendance/day-stepper";

export default function AttendanceTodayPage() {
  const search = useSearchParams();
  const router = useRouter();
  const { can } = useSession();
  const date = search.get("date") ?? todayIso();
  const campusId = search.get("campus") ?? undefined;
  const campuses = useCampuses();
  const active = (campuses.data ?? []).filter((c) => c.isActive);
  // Staff-attendance and HR users reach the workspace through the same menu item; send them to what they can open.
  const canStudents = can(P.Attendance.View, P.Attendance.Manage);
  const fallback = can(P.Staff.ViewAttendance, P.Staff.RecordAttendance) ? "/attendance/staff" : can(P.Hr.ViewLeave, P.Hr.ApproveLeave) ? "/attendance/staff-leave" : null;
  React.useEffect(() => {
    if (!canStudents && fallback) router.replace(fallback);
  }, [canStudents, fallback, router]);
  const overview = useAttendanceOverview({ date, campusId }, canStudents);
  const [filter, setFilter] = React.useState<"all" | "pending">("all");

  const go = (patch: { date?: string; campus?: string | null }) => {
    const params = new URLSearchParams(search.toString());
    if (patch.date) params.set("date", patch.date);
    if (patch.campus) params.set("campus", patch.campus);
    else if (patch.campus === null) params.delete("campus");
    router.replace(`/attendance?${params.toString()}`);
  };

  const o = overview.data;
  return (
    <Page>
      <PageHeader
        title="Attendance"
        description="Today's registers across the school: what has been taken, who is absent, and what is waiting for a decision."
        actions={
          <>
            {active.length > 1 && (
              <NativeSelect aria-label="Campus" value={campusId ?? ""} onChange={(e) => go({ campus: e.target.value || null })} className="h-8 w-auto">
                <option value="">All campuses</option>
                {active.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </NativeSelect>
            )}
            <DayStepper value={date} onChange={(d) => go({ date: d })} />
          </>
        }
      />
      <AttendanceNav className="mb-6" />

      {overview.isPending ? (
        <div className="grid gap-6"><Skeleton className="h-24" /><Skeleton className="h-96" /></div>
      ) : overview.error ? (
        <ErrorState error={overview.error} onRetry={() => void overview.refetch()} />
      ) : (
        <div className="grid gap-8">
          {!o!.day.isSchoolDay && o!.totals.taken === 0 && (
            <div className="flex items-center gap-3 rounded-lg border border-line bg-surface px-4 py-3 text-sm text-ink-2">
              <CalendarOff className="size-4 shrink-0 text-ink-3" />
              {o!.day.closedReason}. No registers are expected{o!.day.isFuture ? "" : " — the office can still open a make-up day from a class register"}.
            </div>
          )}
          <Figures overview={o!} />
          <AlertsLine overview={o!} />
          <div className="grid gap-8 xl:grid-cols-[minmax(0,1fr)_22rem]">
            <section className="grid content-start gap-3">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <h2 className="text-md font-semibold text-ink">Registers</h2>
                <Segmented size="sm" aria-label="Show" value={filter} onChange={setFilter}
                  options={[{ value: "all", label: `All · ${o!.totals.sections}` }, { value: "pending", label: `Not taken · ${o!.totals.sections - o!.totals.taken}` }]} />
              </div>
              <SectionsTable sections={o!.sections.filter((s) => filter === "all" || !s.registerId)} date={date}
                multiCampus={active.length > 1 && !campusId} empty={filter === "pending" ? "Every register has been taken." : "No sections this year."} />
            </section>
            <aside className="grid content-start gap-8">
              <Attention overview={o!} canApprove={can(P.Attendance.Approve)} canLeave={can(P.Attendance.ManageLeave)}
                onShowPending={() => setFilter("pending")} />
              <AbsentToday date={date} campusId={campusId} />
            </aside>
          </div>
        </div>
      )}
    </Page>
  );
}

function Figures({ overview: o }: { overview: AttendanceOverview }) {
  const t = o.totals;
  const figures: { label: string; value: React.ReactNode; hint?: React.ReactNode }[] = [
    { label: "Attendance", value: t.attendanceRate === null ? "—" : formatPercent(t.attendanceRate), hint: `${formatNumber(t.present + t.late)} of ${formatNumber(t.marked)} marked` },
    { label: "Registers taken", value: <>{t.taken}<span className="text-ink-3"> / {t.sections}</span></>, hint: <Progress value={t.sections ? t.taken / t.sections : 0} /> },
    { label: "Absent", value: formatNumber(t.absent), hint: t.onRoll ? `${formatPercent((100 * t.absent) / Math.max(1, t.marked))} of those marked` : undefined },
    { label: "Late", value: formatNumber(t.late) },
    { label: "On leave", value: formatNumber(t.leave) },
  ];
  if (o.staff) {
    const s = o.staff;
    figures.push({
      label: "Staff in today",
      value: <>{s.present + s.late + s.halfDay + s.onDuty}<span className="text-ink-3"> / {s.staff}</span></>,
      hint: s.marked === 0 ? "Staff register not taken" : `${s.absent} absent · ${s.onLeave} on leave`,
    });
  }
  return (
    <dl className="grid grid-cols-2 border-y border-line sm:grid-cols-3 lg:grid-cols-6">
      {figures.map((f, i) => (
        <div key={f.label} className={cn("grid content-start gap-1 py-5 pr-4", i > 0 && "lg:border-l lg:border-line lg:pl-5")}>
          <dt className="text-sm text-ink-2">{f.label}</dt>
          <dd className="font-display text-3xl text-ink tabular">{f.value}</dd>
          {f.hint && <dd className="text-xs text-ink-3 tabular">{f.hint}</dd>}
        </div>
      ))}
    </dl>
  );
}

function Progress({ value }: { value: number }) {
  return (
    <span className="mt-1 block h-1 w-full max-w-32 overflow-hidden rounded-full bg-line" aria-hidden>
      <span className="block h-full rounded-full bg-accent" style={{ width: `${Math.round(value * 100)}%` }} />
    </span>
  );
}

function AlertsLine({ overview }: { overview: AttendanceOverview }) {
  const a = overview.alerts;
  if (a.total === 0) return null;
  const handed = a.queued + a.sent + a.delivered;
  return (
    <p className="-mt-4 flex flex-wrap items-center gap-2 text-sm text-ink-2">
      <MessageSquareOff className="size-4 shrink-0 text-ink-3" />
      {a.notSent > 0 ? (
        <>
          <span><span className="font-medium text-ink tabular">{a.notSent}</span> absence alert{a.notSent === 1 ? "" : "s"} recorded but not sent — {noProviderPhrase(a.notSentChannels)}.</span>
          <Link href={`/attendance/absentees?date=${overview.date}`} className="font-medium text-accent hover:underline">Open the call list</Link>
        </>
      ) : (
        <span>{handed} alert{handed === 1 ? "" : "s"} with the provider · {a.delivered} confirmed delivered{a.failed ? ` · ${a.failed} failed` : ""}.</span>
      )}
    </p>
  );
}

function SectionsTable({ sections, date, multiCampus, empty }: { sections: SectionDay[]; date: string; multiCampus: boolean; empty: string }) {
  const router = useRouter();
  if (sections.length === 0) {
    return <p className="rounded-lg border border-line bg-surface px-4 py-10 text-center text-sm text-ink-2">{empty}</p>;
  }
  return (
    <div className="overflow-x-auto rounded-lg border border-line bg-surface">
      <table className="w-full min-w-[40rem] text-sm">
        <caption className="sr-only">Registers for the day</caption>
        <thead>
          <tr className="border-b border-line text-left text-xs font-medium text-ink-3">
            <th className="px-4 py-2 font-medium">Class</th>
            <th className="px-3 py-2 font-medium max-md:hidden">Class teacher</th>
            <th className="px-3 py-2 font-medium">Register</th>
            <th className="px-3 py-2 text-right font-medium">Present</th>
            <th className="px-3 py-2 text-right font-medium">Absent</th>
            <th className="px-3 py-2 text-right font-medium max-sm:hidden">Late</th>
            <th className="px-3 py-2 text-right font-medium max-sm:hidden">Leave</th>
            <th className="w-10 px-2 py-2"><span className="sr-only">Open</span></th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {sections.map((s) => {
            const marked = s.present + s.late + s.absent + s.leave;
            const href = `/attendance/register/${s.sectionId}?date=${date}`;
            return (
              <tr key={s.sectionId} className="group cursor-pointer transition-colors hover:bg-sunken/60"
                onClick={(e) => { if (!(e.target as HTMLElement).closest("a")) router.push(href); }}>
                <td className="px-4 py-2.5">
                  <Link href={href} className="font-medium text-ink hover:underline">{s.className}</Link>
                  {multiCampus && <span className="block text-xs text-ink-3">{s.campusName}</span>}
                </td>
                <td className="px-3 py-2.5 text-ink-2 max-md:hidden">{s.classTeacherName ?? <span className="text-ink-3">Not assigned</span>}</td>
                <td className="px-3 py-2.5">
                  {s.registerId ? (
                    <span className="grid leading-tight">
                      <StatusBadge tone="success">Taken {s.takenAt ? formatTime(s.takenAt) : ""}</StatusBadge>
                      {s.takenByName && s.takenByName !== s.classTeacherName && <span className="pl-3 text-xs text-ink-3">by {s.takenByName}</span>}
                    </span>
                  ) : (
                    <StatusBadge tone="warning">Not taken</StatusBadge>
                  )}
                </td>
                <td className="px-3 py-2.5 text-right tabular">
                  {s.registerId ? <span className="text-ink">{s.present + s.late}<span className="text-ink-3"> / {marked}</span></span> : <span className="text-ink-3">{s.onRoll} on roll</span>}
                </td>
                <td className={cn("px-3 py-2.5 text-right tabular", s.absent > 0 ? "font-medium text-danger" : "text-ink-3")}>{s.registerId ? s.absent : "—"}</td>
                <td className="px-3 py-2.5 text-right text-ink-2 tabular max-sm:hidden">{s.registerId ? s.late : "—"}</td>
                <td className="px-3 py-2.5 text-right text-ink-2 tabular max-sm:hidden">{s.registerId ? s.leave : "—"}</td>
                <td className="px-2 py-2.5 text-ink-3"><ArrowRight className="size-4 opacity-0 transition-opacity group-hover:opacity-100" /></td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function Attention({ overview: o, canApprove, canLeave, onShowPending }: {
  overview: AttendanceOverview;
  canApprove: boolean;
  canLeave: boolean;
  onShowPending: () => void;
}) {
  const items = [
    canApprove && { href: "/attendance/corrections", label: "Corrections waiting for approval", count: o.pendingCorrections },
    canLeave && { href: "/attendance/leave?status=Pending", label: "Leave applications to decide", count: o.pendingLeave },
    o.day.isSchoolDay && { label: "Registers not taken yet", count: o.totals.sections - o.totals.taken, onClick: onShowPending },
  ].filter(Boolean) as { href?: string; label: string; count: number; onClick?: () => void }[];
  const row = (item: (typeof items)[number]) => (
    <>
      <ClipboardCheck className="size-4 shrink-0 text-ink-3" />
      <span className="min-w-0 flex-1 text-left text-ink">{item.label}</span>
      <span className={cn("tabular", item.count > 0 ? "font-semibold text-ink" : "text-ink-3")}>{item.count}</span>
    </>
  );
  return (
    <section className="grid content-start gap-3">
      <h2 className="text-md font-semibold text-ink">Needs attention</h2>
      <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
        {items.map((item) => (
          <li key={item.label}>
            {item.href ? (
              <Link href={item.href} className="flex items-center gap-3 px-4 py-3 text-sm hover:bg-sunken/60">{row(item)}</Link>
            ) : (
              <button type="button" onClick={item.onClick} className="flex w-full items-center gap-3 px-4 py-3 text-sm hover:bg-sunken/60">{row(item)}</button>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}

function AbsentToday({ date, campusId }: { date: string; campusId?: string }) {
  const absentees = useAbsentees({ date, campusId });
  const list = (absentees.data ?? []).filter((a) => a.status === "Absent");
  const top = [...list].sort((a, b) => b.consecutiveAbsences - a.consecutiveAbsences).slice(0, 6);
  return (
    <section className="grid content-start gap-3">
      <div className="flex items-center justify-between gap-3">
        <h2 className="text-md font-semibold text-ink">Absent {date === todayIso() ? "today" : "that day"}</h2>
        {list.length > 0 && (
          <Link href={`/attendance/absentees?date=${date}${campusId ? `&campus=${campusId}` : ""}`} className="inline-flex items-center gap-1 text-sm font-medium text-accent hover:underline">
            <PhoneCall className="size-3.5" /> Call list · {list.length}
          </Link>
        )}
      </div>
      {absentees.isPending ? <Skeleton className="h-40" /> : top.length === 0 ? (
        <p className="rounded-lg border border-line bg-surface px-4 py-6 text-center text-sm text-ink-2">No absences recorded.</p>
      ) : (
        <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
          {top.map((a) => (
            <li key={a.studentId} className="flex items-center gap-3 px-4 py-2.5">
              <RecordAvatar name={a.fullName} fileId={a.photoFileId} size={28} />
              <div className="grid min-w-0 flex-1 leading-tight">
                <Link href={`/students/${a.studentId}?tab=attendance`} className="truncate text-sm font-medium text-ink hover:underline">{a.fullName}</Link>
                <span className="truncate text-xs text-ink-2">{a.className}{a.guardianPhone && <> · <a href={`tel:${a.guardianPhone}`} className="tabular hover:underline">{formatPhone(a.guardianPhone)}</a></>}</span>
              </div>
              {a.consecutiveAbsences >= 2 && (
                <StatusBadge tone={a.consecutiveAbsences >= 3 ? "danger" : "warning"} variant="soft">{a.consecutiveAbsences} days in a row</StatusBadge>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
