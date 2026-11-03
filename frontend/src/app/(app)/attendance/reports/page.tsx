"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ChartNoAxesColumn } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCampuses } from "@/lib/api/access";
import { useGrades, useSections } from "@/lib/api/org";
import {
  addDays,
  attendanceCode,
  exportLowAttendance,
  exportMonthRegister,
  useAttendanceTrend,
  useLowAttendance,
  useMonthRegister,
  type AttendanceStatus,
  type MonthRegister,
} from "@/lib/api/attendance";
import { formatPhone } from "@/lib/api/students";
import { formatDate, formatPercent, todayIso } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Skeleton, Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Input, NativeSelect } from "@/components/ui/input";
import { Field } from "@/components/ui/field";
import { Segmented } from "@/components/ui/toggles";
import { Tooltip } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { ExportMenu } from "@/components/data-table/toolbar";
import { RecordAvatar } from "@/components/records/record-avatar";
import { AttendanceNav } from "@/components/attendance/attendance-nav";
import { AttendanceTrend } from "@/components/attendance/attendance-trend";

type View = "month" | "low" | "trend";

export default function AttendanceReportsPage() {
  const search = useSearchParams();
  const router = useRouter();
  const view = (search.get("view") as View | null) ?? "month";
  return (
    <Page>
      <PageHeader title="Attendance reports" description="The monthly register, who is falling below the school's minimum, and how attendance is moving." />
      <AttendanceNav className="mb-6" />
      <Tabs value={view} onValueChange={(v) => router.replace(`/attendance/reports?view=${v}`)}>
        <TabsList className="mb-5">
          <TabsTrigger value="month">Monthly register</TabsTrigger>
          <TabsTrigger value="low">Below minimum</TabsTrigger>
          <TabsTrigger value="trend">Trend</TabsTrigger>
        </TabsList>
        <TabsContent value="month"><MonthView /></TabsContent>
        <TabsContent value="low"><LowView /></TabsContent>
        <TabsContent value="trend"><TrendView /></TabsContent>
      </Tabs>
    </Page>
  );
}

// ───────────────────────────── Monthly register ─────────────────────────────

function SectionPicker({ value, onChange }: { value: string; onChange: (id: string) => void }) {
  const sections = useSections({});
  const list = (sections.data ?? []).filter((s) => s.isActive);
  const multiCampus = new Set(list.map((s) => s.campusId)).size > 1;
  return (
    <NativeSelect aria-label="Section" value={value} onChange={(e) => onChange(e.target.value)} className="w-auto min-w-52">
      <option value="">{sections.isPending ? "Loading sections…" : "Choose a section"}</option>
      {list.map((s) => <option key={s.id} value={s.id}>{s.displayName}{multiCampus ? ` · ${s.campusName}` : ""}</option>)}
    </NativeSelect>
  );
}

function MonthView() {
  const { can } = useSession();
  const search = useSearchParams();
  const router = useRouter();
  const sections = useSections({});
  const today = todayIso();
  const sectionId = search.get("section") ?? sections.data?.find((s) => s.isActive)?.id ?? "";
  // Early in a month there is little to show: default to the month just finished.
  const month = search.get("month") ?? (Number(today.slice(8, 10)) <= 7 ? addDays(`${today.slice(0, 7)}-01`, -1).slice(0, 7) : today.slice(0, 7));
  const [year, m] = month.split("-").map(Number) as [number, number];
  const register = useMonthRegister({ sectionId, year, month: m }, !!sectionId);
  const [exporting, setExporting] = React.useState(false);

  const set = (patch: Record<string, string>) => {
    const params = new URLSearchParams(search.toString());
    params.set("view", "month");
    Object.entries(patch).forEach(([k, v]) => params.set(k, v));
    router.replace(`/attendance/reports?${params.toString()}`);
  };

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <SectionPicker value={sectionId} onChange={(id) => set({ section: id })} />
        <Input type="month" aria-label="Month" value={month} max={today.slice(0, 7)} onChange={(e) => e.target.value && set({ month: e.target.value })} className="w-40" />
        <div className="ml-auto">
          {can(P.Attendance.Export) && sectionId && (
            <ExportMenu busy={exporting} onExport={async (format) => {
              setExporting(true);
              try { await exportMonthRegister(sectionId, year, m, format); } catch (e) { toast.error(e, "The export couldn't be created"); } finally { setExporting(false); }
            }} />
          )}
        </div>
      </div>
      {!sectionId ? (
        <div className="rounded-lg border border-line bg-surface"><EmptyState compact icon={<ChartNoAxesColumn />} title="Choose a section" /></div>
      ) : register.isPending ? (
        <Skeleton className="h-96" />
      ) : register.error ? (
        <ErrorState error={register.error} onRetry={() => void register.refetch()} />
      ) : (
        <MonthGrid register={register.data} fetching={register.isFetching} />
      )}
    </div>
  );
}

const cellTone: Record<AttendanceStatus, string> = {
  Present: "text-ink-3",
  Late: "font-medium text-[oklch(from_var(--warning)_calc(l-0.16)_c_h)] dark:text-warning",
  Absent: "font-semibold text-danger bg-danger-soft",
  Leave: "font-medium text-info bg-info-soft",
};

function MonthGrid({ register, fetching }: { register: MonthRegister; fetching: boolean }) {
  const today = todayIso();
  const t = register.totals;
  return (
    <div className={cn("grid gap-3 transition-opacity", fetching && "opacity-60")}>
      <p className="text-sm text-ink-2">
        <span className="font-medium text-ink">{register.className}</span> · {register.days.filter((d) => d.taken).length} registers taken ·
        overall <span className="font-medium text-ink tabular">{formatPercent(t.percentage)}</span> · {t.absent} absences · {t.late} late · {t.leave} on leave
      </p>
      <div className="overflow-x-auto rounded-lg border border-line bg-surface">
        <table className="w-max min-w-full border-collapse text-xs tabular">
          <caption className="sr-only">Monthly register for {register.className}</caption>
          <thead>
            <tr className="border-b border-line text-ink-3">
              <th scope="col" className="sticky left-0 z-10 bg-surface px-3 py-2 text-left font-medium">Student</th>
              {register.days.map((d) => {
                const date = new Date(`${d.date}T12:00:00Z`);
                return (
                  <th key={d.date} scope="col" title={d.closedReason ?? formatDate(d.date, { weekday: true })}
                    className={cn("w-6 px-0 py-1.5 text-center font-medium", !d.isSchoolDay && "bg-sunken text-ink-3/70", d.date === today && "text-accent")}>
                    <span className="block text-2xs leading-none">{"SMTWTFS"[date.getUTCDay()]}</span>
                    <span className="block leading-tight">{date.getUTCDate()}</span>
                  </th>
                );
              })}
              <th scope="col" className="px-2 py-2 text-right font-medium">A</th>
              <th scope="col" className="px-2 py-2 text-right font-medium">LT</th>
              <th scope="col" className="px-2 py-2 text-right font-medium">L</th>
              <th scope="col" className="px-3 py-2 text-right font-medium">%</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {register.students.map((s) => (
              <tr key={s.studentId} className="hover:bg-sunken/40">
                <th scope="row" className="sticky left-0 z-10 max-w-48 bg-surface px-3 py-1.5 text-left font-normal">
                  <Link href={`/students/${s.studentId}?tab=attendance`} className="block truncate text-sm text-ink hover:underline">
                    {s.rollNumber && <span className="mr-1.5 text-ink-3">{s.rollNumber}</span>}{s.fullName}
                  </Link>
                </th>
                {s.marks.map((mark, i) => {
                  const day = register.days[i]!;
                  return (
                    <td key={day.date} className={cn("h-8 w-6 p-0 text-center", !day.isSchoolDay && "bg-sunken")}>
                      {mark ? (
                        <Tooltip content={`${formatDate(day.date, { weekday: true })}: ${mark}`}>
                          <span className={cn("mx-auto grid h-6 w-[22px] place-items-center rounded-sm text-[11px]", cellTone[mark])}>
                            {mark === "Present" ? "·" : attendanceCode[mark]}
                          </span>
                        </Tooltip>
                      ) : day.isSchoolDay && day.date <= today && !day.taken ? (
                        <span className="text-ink-3/60" title="No register taken">–</span>
                      ) : null}
                    </td>
                  );
                })}
                <td className={cn("px-2 text-right", s.totals.absent > 0 ? "text-danger" : "text-ink-3")}>{s.totals.absent}</td>
                <td className="px-2 text-right text-ink-2">{s.totals.late}</td>
                <td className="px-2 text-right text-ink-2">{s.totals.leave}</td>
                <td className={cn("px-3 text-right font-medium", (s.totals.percentage ?? 100) < 75 ? "text-danger" : "text-ink")}>{formatPercent(s.totals.percentage, 0)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-ink-3">
        <span>· present</span><span>LT late</span><span>A absent</span><span>L leave</span><span>– register not taken</span><span>shaded: holiday or day off</span>
      </p>
    </div>
  );
}

// ───────────────────────────── Below minimum ─────────────────────────────

function LowView() {
  const { can } = useSession();
  const grades = useGrades();
  const campuses = useCampuses();
  const [gradeId, setGradeId] = React.useState("");
  const [campusId, setCampusId] = React.useState("");
  const [below, setBelow] = React.useState("");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [exporting, setExporting] = React.useState(false);
  const query = { gradeId: gradeId || undefined, campusId: campusId || undefined, below: below || undefined, from: from || undefined, to: to || undefined, page, pageSize: 50 };
  const low = useLowAttendance(query);
  const data = low.data;
  const activeCampuses = (campuses.data ?? []).filter((c) => c.isActive);

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-end gap-3">
        <Field label="Below" htmlFor="low-below" className="w-28">
          <Input id="low-below" type="number" min={1} max={100} step={1} placeholder={data ? String(data.below) : "75"} value={below}
            onChange={(e) => { setBelow(e.target.value); setPage(1); }} trailing={<span className="pr-1 text-sm">%</span>} />
        </Field>
        <Field label="From" htmlFor="low-from" className="w-40">
          <Input id="low-from" type="date" value={from || data?.from || ""} onChange={(e) => { setFrom(e.target.value); setPage(1); }} />
        </Field>
        <Field label="To" htmlFor="low-to" className="w-40">
          <Input id="low-to" type="date" value={to || data?.to || ""} max={todayIso()} onChange={(e) => { setTo(e.target.value); setPage(1); }} />
        </Field>
        <Field label="Class" htmlFor="low-grade" className="w-40">
          <NativeSelect id="low-grade" value={gradeId} onChange={(e) => { setGradeId(e.target.value); setPage(1); }}>
            <option value="">All classes</option>
            {(grades.data ?? []).filter((g) => g.isActive).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
          </NativeSelect>
        </Field>
        {activeCampuses.length > 1 && (
          <Field label="Campus" htmlFor="low-campus" className="w-48">
            <NativeSelect id="low-campus" value={campusId} onChange={(e) => { setCampusId(e.target.value); setPage(1); }}>
              <option value="">All campuses</option>
              {activeCampuses.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </NativeSelect>
          </Field>
        )}
        <div className="ml-auto pb-0.5">
          {can(P.Attendance.Export) && (
            <ExportMenu busy={exporting} onExport={async (format) => {
              setExporting(true);
              try { await exportLowAttendance({ ...query, page: undefined, pageSize: undefined }, format); } catch (e) { toast.error(e, "The export couldn't be created"); } finally { setExporting(false); }
            }} />
          )}
        </div>
      </div>
      {low.isPending ? <Skeleton className="h-96" /> : low.error ? <ErrorState error={low.error} onRetry={() => void low.refetch()} /> : (
        <div className={cn("rounded-lg border border-line bg-surface transition-opacity", low.isFetching && "opacity-60")}>
          <p className="border-b border-line px-4 py-2.5 text-sm text-ink-2">
            <span className="font-medium text-ink tabular">{data!.students.total}</span> student{data!.students.total === 1 ? "" : "s"} below {data!.below}% between {formatDate(data!.from)} and {formatDate(data!.to)}
          </p>
          {data!.students.items.length === 0 ? (
            <EmptyState compact icon={<ChartNoAxesColumn />} title="Nobody is below the minimum" description="Everyone attended at least the minimum share of school days in this period." />
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[44rem] text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-xs text-ink-3">
                    <th className="px-4 py-2 font-medium">Student</th>
                    <th className="px-3 py-2 font-medium">Class</th>
                    <th className="px-3 py-2 text-right font-medium">Attendance</th>
                    <th className="px-3 py-2 text-right font-medium">Absent</th>
                    <th className="px-3 py-2 text-right font-medium">Days</th>
                    <th className="px-3 py-2 font-medium">Last absent</th>
                    <th className="px-4 py-2 font-medium">Family</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-line">
                  {data!.students.items.map((r) => (
                    <tr key={r.studentId}>
                      <td className="px-4 py-2">
                        <span className="flex items-center gap-2.5">
                          <RecordAvatar name={r.fullName} fileId={r.photoFileId} size={26} />
                          <Link href={`/students/${r.studentId}?tab=attendance`} className="font-medium text-ink hover:underline">{r.fullName}</Link>
                        </span>
                      </td>
                      <td className="px-3 py-2 text-ink-2">{r.className ?? "—"}</td>
                      <td className="px-3 py-2 text-right">
                        <span className="inline-flex items-center gap-2">
                          <span className="hidden h-1.5 w-16 overflow-hidden rounded-full bg-line sm:block" aria-hidden>
                            <span className="block h-full rounded-full bg-danger" style={{ width: `${r.totals.percentage ?? 0}%` }} />
                          </span>
                          <span className="font-medium text-danger tabular">{formatPercent(r.totals.percentage)}</span>
                        </span>
                      </td>
                      <td className="px-3 py-2 text-right text-ink tabular">{r.totals.absent}</td>
                      <td className="px-3 py-2 text-right text-ink-2 tabular">{r.totals.days}</td>
                      <td className="px-3 py-2 text-ink-2 tabular">{formatDate(r.lastAbsent)}</td>
                      <td className="px-4 py-2">
                        <span className="grid leading-tight">
                          <span className="truncate text-ink">{r.guardianName ?? "—"}</span>
                          {r.guardianPhone && <a href={`tel:${r.guardianPhone}`} className="text-xs text-accent tabular hover:underline">{formatPhone(r.guardianPhone)}</a>}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {data!.students.totalPages > 1 && (
            <div className="flex items-center justify-end gap-2 border-t border-line px-4 py-2.5 text-sm text-ink-2">
              <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous</Button>
              <span className="tabular">Page {page} of {data!.students.totalPages}</span>
              <Button size="sm" variant="secondary" disabled={page >= data!.students.totalPages} onClick={() => setPage((p) => p + 1)}>Next</Button>
            </div>
          )}
        </div>
      )}
      <p className="text-xs text-ink-3">Percentages count school days with a register; approved leave follows the school&apos;s attendance policy.</p>
    </div>
  );
}

// ───────────────────────────── Trend ─────────────────────────────

function TrendView() {
  const campuses = useCampuses();
  const [range, setRange] = React.useState<"30" | "90" | "365">("30");
  const [campusId, setCampusId] = React.useState("");
  const today = todayIso();
  // Days without registers (before term, holidays) carry no rate and are left out, so a long range starts at term.
  const from = addDays(today, -(Number(range) - 1));
  const trend = useAttendanceTrend({ from, to: today, campusId: campusId || undefined });
  const activeCampuses = (campuses.data ?? []).filter((c) => c.isActive);
  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <Segmented aria-label="Period" value={range} onChange={setRange}
          options={[{ value: "30", label: "Last 30 days" }, { value: "90", label: "Last 90 days" }, { value: "365", label: "Past year" }]} />
        {activeCampuses.length > 1 && (
          <NativeSelect aria-label="Campus" value={campusId} onChange={(e) => setCampusId(e.target.value)} className="w-auto">
            <option value="">All campuses</option>
            {activeCampuses.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </NativeSelect>
        )}
      </div>
      <section className="rounded-lg border border-line bg-surface py-4">
        {trend.isPending ? <Skeleton className="mx-4 h-64" /> : trend.error ? <ErrorState error={trend.error} onRetry={() => void trend.refetch()} /> : (
          <AttendanceTrend points={trend.data} fetching={trend.isFetching} />
        )}
      </section>
    </div>
  );
}
