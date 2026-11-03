"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { CalendarOff, Check, ClipboardCheck, Lock, Undo2 } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCampuses } from "@/lib/api/access";
import {
  correctionStatusLabel,
  correctionStatusTone,
  exportStaffMonth,
  staffAttendanceCode,
  staffAttendanceLabel,
  staffAttendanceTone,
  staffStatuses,
  useDecideStaffCorrection,
  useSaveStaffRegister,
  useStaffCorrections,
  useStaffMonth,
  useStaffRegister,
  useWithdrawStaffCorrection,
  type StaffAttendanceStatus,
  type StaffCorrection,
  type StaffRegister,
  type StaffRegisterRow,
} from "@/lib/api/attendance";
import { staffTypeLabel, type StaffType } from "@/lib/api/staff";
import { formatDate, formatDateTime, formatPercent, todayIso } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Badge, Skeleton, StatusBadge, Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Input, NativeSelect } from "@/components/ui/input";
import { Segmented } from "@/components/ui/toggles";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Tooltip } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { ExportMenu } from "@/components/data-table/toolbar";
import { RecordAvatar } from "@/components/records/record-avatar";
import { AttendanceNav } from "@/components/attendance/attendance-nav";
import { DayStepper } from "@/components/attendance/day-stepper";
import { MarkControl, type MarkOption } from "@/components/attendance/mark-control";

const staffOptions: MarkOption<StaffAttendanceStatus>[] = staffStatuses.map((s) => ({
  value: s,
  label: staffAttendanceLabel[s],
  code: staffAttendanceCode[s],
  tone: s === "Present" ? "neutral" : staffAttendanceTone[s],
}));

type View = "register" | "month" | "corrections";

export default function StaffAttendancePage() {
  const search = useSearchParams();
  const router = useRouter();
  const campuses = useCampuses();
  const active = (campuses.data ?? []).filter((c) => c.isActive);
  const campusId = search.get("campus") ?? active.find((c) => c.isMain)?.id ?? active[0]?.id;
  const view = (search.get("view") as View | null) ?? "register";
  const date = search.get("date") ?? todayIso();

  const set = (patch: Record<string, string>) => {
    const params = new URLSearchParams(search.toString());
    Object.entries(patch).forEach(([k, v]) => params.set(k, v));
    router.replace(`/attendance/staff?${params.toString()}`);
  };

  return (
    <Page>
      <PageHeader
        title="Staff register"
        description="Who is in today: the daily staff register, monthly sheets for payroll, and corrections staff have asked for."
        actions={
          <>
            {active.length > 1 && (
              <NativeSelect aria-label="Campus" value={campusId ?? ""} onChange={(e) => set({ campus: e.target.value })} className="w-auto">
                {active.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </NativeSelect>
            )}
            {view === "register" && <DayStepper value={date} onChange={(d) => set({ date: d })} />}
          </>
        }
      />
      <AttendanceNav className="mb-6" />
      <Tabs value={view} onValueChange={(v) => set({ view: v })}>
        <TabsList className="mb-5">
          <TabsTrigger value="register">Daily register</TabsTrigger>
          <TabsTrigger value="month">Month</TabsTrigger>
          <TabsTrigger value="corrections">Corrections</TabsTrigger>
        </TabsList>
        <TabsContent value="register">{campusId ? <RegisterView campusId={campusId} date={date} /> : <Skeleton className="h-96" />}</TabsContent>
        <TabsContent value="month">{campusId ? <MonthView campusId={campusId} /> : <Skeleton className="h-96" />}</TabsContent>
        <TabsContent value="corrections"><CorrectionsView /></TabsContent>
      </Tabs>
    </Page>
  );
}

// ───────────────────────────── Daily register ─────────────────────────────

type Mark = { status: StaffAttendanceStatus; checkIn: string | null; checkOut: string | null; remark: string | null };

const toMarks = (rows: StaffRegisterRow[]) =>
  Object.fromEntries(rows.map((r) => [r.staffId, { status: r.status, checkIn: r.checkIn?.slice(0, 5) ?? null, checkOut: r.checkOut?.slice(0, 5) ?? null, remark: r.remark }])) as Record<string, Mark>;

function RegisterView({ campusId, date }: { campusId: string; date: string }) {
  const register = useStaffRegister({ campusId, date });
  if (register.isPending) return <Skeleton className="h-96" />;
  if (register.error) return <ErrorState error={register.error} onRetry={() => void register.refetch()} />;
  return <StaffRegisterSheet register={register.data} />;
}

function StaffRegisterSheet({ register: r }: { register: StaffRegister }) {
  const save = useSaveStaffRegister();
  const [marks, setMarks] = React.useState(() => toMarks(r.staff));
  const [key, setKey] = React.useState("");
  const [override, setOverride] = React.useState(false);
  const currentKey = `${r.campusId}|${r.date}|${r.lastSavedAt ?? "new"}`;
  if (currentKey !== key) {
    setKey(currentKey);
    setMarks(toMarks(r.staff));
    setOverride(false);
  }

  const closed = !r.day.isSchoolDay;
  const editable = r.canRecord && (!closed || override);
  const changed = r.staff.filter((row) => {
    const m = marks[row.staffId];
    return m && (!row.saved || m.status !== row.status || (m.checkIn ?? "") !== (row.checkIn?.slice(0, 5) ?? "") || (m.checkOut ?? "") !== (row.checkOut?.slice(0, 5) ?? "") || (m.remark ?? "") !== (row.remark ?? ""));
  });
  const groups = (["Teaching", "Administrative", "NonTeaching", "Support"] as StaffType[])
    .map((type) => ({ type, rows: r.staff.filter((s) => s.staffType === type) }))
    .filter((g) => g.rows.length > 0);
  const counts = staffStatuses.map((s) => ({ s, n: r.staff.filter((row) => (marks[row.staffId]?.status ?? row.status) === s).length }));

  const set = (id: string, patch: Partial<Mark>) => setMarks((m) => {
    const next = { ...m[id]!, ...patch };
    if (patch.status === "Absent" || patch.status === "OnLeave") { next.checkIn = null; next.checkOut = null; }
    return { ...m, [id]: next };
  });

  const submit = async () => {
    try {
      await save.mutateAsync({
        campusId: r.campusId, date: r.date, overrideClosedDay: override,
        marks: r.staff.map((row) => {
          const m = marks[row.staffId]!;
          return { staffId: row.staffId, status: m.status, checkIn: m.checkIn ? `${m.checkIn}:00` : null, checkOut: m.checkOut ? `${m.checkOut}:00` : null, remark: m.remark?.trim() || null };
        }),
      });
      toast.success(r.taken ? "Staff register updated" : "Staff register saved");
    } catch (error) {
      toast.error(error, "The staff register couldn't be saved");
    }
  };

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border border-line bg-surface px-4 py-2.5 text-sm text-ink-2">
        {r.day.isFuture ? (
          <><CalendarOff className="size-4 text-ink-3" /> This day hasn&apos;t happened yet.</>
        ) : closed && !override ? (
          <>
            <CalendarOff className="size-4 text-ink-3" /> <span className="flex-1">{r.day.closedReason}. No staff register is expected.</span>
            {r.canOverrideClosedDay && !r.isLocked && <Button size="sm" variant="secondary" onClick={() => setOverride(true)}>Record anyway</Button>}
          </>
        ) : r.isLocked ? (
          <><Lock className="size-4 text-ink-3" /> Locked since {formatDateTime(r.locksAt)}. Changes are made as corrections, which need approval.</>
        ) : r.taken ? (
          <>Saved {r.lastSavedAt ? formatDateTime(r.lastSavedAt) : ""}{r.lastSavedByName && ` by ${r.lastSavedByName}`}. Open for changes until {formatDateTime(r.locksAt)}.</>
        ) : (
          <>Not taken yet. Everyone starts as present; approved leave is filled in.</>
        )}
      </div>

      <section className="rounded-lg border border-line bg-surface">
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-b border-line px-4 py-2.5 text-sm text-ink-2">
          <span className="font-medium text-ink tabular">{r.staff.length} staff</span>
          {counts.filter((c) => c.n > 0).map(({ s, n }) => (
            <StatusBadge key={s} tone={staffAttendanceTone[s]} className="text-ink-2"><span className="tabular text-ink">{n}</span> {staffAttendanceLabel[s].toLowerCase()}</StatusBadge>
          ))}
          <span className="ml-auto hidden text-xs text-ink-3 md:inline">P present · LT late · HD half day · A absent · L on leave · OD on duty</span>
        </div>
        {groups.map((g) => (
          <div key={g.type}>
            <h3 className="border-b border-line bg-sunken/50 px-4 py-1.5 text-xs font-medium text-ink-2">{staffTypeLabel[g.type]} · {g.rows.length}</h3>
            <ul className="divide-y divide-line">
              {g.rows.map((row) => {
                const m = marks[row.staffId]!;
                const works = m.status !== "Absent" && m.status !== "OnLeave";
                return (
                  <li key={row.staffId} className={cn("grid items-center gap-x-3 gap-y-2 px-4 py-2 sm:grid-cols-[minmax(0,1fr)_auto]",
                    m.status === "Absent" && "bg-danger-soft/40 shadow-[inset_2px_0_0_var(--danger)]", m.status === "OnLeave" && "shadow-[inset_2px_0_0_var(--info)]")}>
                    <div className="flex min-w-0 items-center gap-2.5">
                      <RecordAvatar name={row.fullName} fileId={row.photoFileId} size={30} />
                      <div className="grid min-w-0 leading-tight">
                        <span className="flex min-w-0 items-center gap-1.5">
                          <Link href={`/staff/${row.staffId}`} className="truncate font-medium text-ink hover:underline">{row.fullName}</Link>
                          {row.hint && <Badge tone={row.hint.kind === "leave" ? "info" : "warning"}>{row.hint.text}</Badge>}
                        </span>
                        <span className="truncate text-xs text-ink-2">{row.designationName ?? "—"} · <span className="font-mono">{row.employeeNumber}</span></span>
                      </div>
                    </div>
                    <div className="flex flex-wrap items-center justify-end gap-2">
                      {works && (
                        <>
                          <label className="inline-flex items-center gap-1.5 text-xs text-ink-3">In
                            <Input type="time" inputSize="sm" className="w-[7.25rem]" aria-label={`Check-in for ${row.fullName}`} value={m.checkIn ?? ""}
                              disabled={!editable} onChange={(e) => set(row.staffId, { checkIn: e.target.value || null })} />
                          </label>
                          {/* Check-out matters once the day is over; on the morning register it would be an empty box on every row. */}
                          {(!r.day.isToday || m.checkOut) && (
                            <label className="inline-flex items-center gap-1.5 text-xs text-ink-3">Out
                              <Input type="time" inputSize="sm" className="w-[7.25rem]" aria-label={`Check-out for ${row.fullName}`} value={m.checkOut ?? ""}
                                disabled={!editable} onChange={(e) => set(row.staffId, { checkOut: e.target.value || null })} />
                            </label>
                          )}
                        </>
                      )}
                      {m.status !== "Present" && (
                        <Input inputSize="sm" className="w-full sm:w-40" placeholder="Note" aria-label={`Note for ${row.fullName}`} value={m.remark ?? ""}
                          disabled={!editable} onChange={(e) => set(row.staffId, { remark: e.target.value })} />
                      )}
                      <MarkControl codesOnly label={`Attendance for ${row.fullName}`} value={m.status} options={staffOptions} disabled={!editable}
                        onChange={(status) => set(row.staffId, { status })} />
                    </div>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
        {editable && (
          <div className="sticky bottom-0 z-10 flex items-center gap-3 rounded-b-lg border-t border-line bg-surface px-4 py-3 shadow-[0_-6px_16px_-12px_oklch(0.2_0.01_70/0.25)]">
            <span className="text-sm text-ink-2">{r.taken ? `${changed.length} change${changed.length === 1 ? "" : "s"}` : "Mark the exceptions, then save."}</span>
            <div className="ml-auto flex items-center gap-2">
              {r.taken && changed.length > 0 && <Button variant="ghost" onClick={() => setMarks(toMarks(r.staff))}><Undo2 /> Discard</Button>}
              <Button variant="primary" loading={save.isPending} disabled={r.taken && changed.length === 0} onClick={() => void submit()}>
                <Check /> {r.taken ? "Save changes" : "Save register"}
              </Button>
            </div>
          </div>
        )}
      </section>
    </div>
  );
}

// ───────────────────────────── Month ─────────────────────────────

function MonthView({ campusId }: { campusId: string }) {
  const { can } = useSession();
  const today = todayIso();
  const [month, setMonth] = React.useState(today.slice(0, 7));
  const [year, m] = month.split("-").map(Number) as [number, number];
  const sheet = useStaffMonth({ campusId, year, month: m });
  const [exporting, setExporting] = React.useState(false);
  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <Input type="month" aria-label="Month" value={month} max={today.slice(0, 7)} onChange={(e) => e.target.value && setMonth(e.target.value)} className="w-40" />
        <div className="ml-auto">
          {can(P.Staff.ViewAttendance) && (
            <ExportMenu busy={exporting} onExport={async (format) => {
              setExporting(true);
              try { await exportStaffMonth(campusId, year, m, format); } catch (e) { toast.error(e, "The export couldn't be created"); } finally { setExporting(false); }
            }} />
          )}
        </div>
      </div>
      {sheet.isPending ? <Skeleton className="h-96" /> : sheet.error ? <ErrorState error={sheet.error} onRetry={() => void sheet.refetch()} /> : (
        <div className={cn("overflow-x-auto rounded-lg border border-line bg-surface transition-opacity", sheet.isFetching && "opacity-60")}>
          <table className="w-max min-w-full border-collapse text-xs tabular">
            <caption className="sr-only">Staff attendance for the month</caption>
            <thead>
              <tr className="border-b border-line text-ink-3">
                <th scope="col" className="sticky left-0 z-10 bg-surface px-3 py-2 text-left font-medium">Staff</th>
                {sheet.data.days.map((d) => {
                  const date = new Date(`${d.date}T12:00:00Z`);
                  return (
                    <th key={d.date} scope="col" title={d.closedReason ?? formatDate(d.date, { weekday: true })}
                      className={cn("w-7 px-0 py-1.5 text-center font-medium", !d.isSchoolDay && "bg-sunken text-ink-3/70")}>
                      <span className="block text-2xs leading-none">{"SMTWTFS"[date.getUTCDay()]}</span>
                      <span className="block leading-tight">{date.getUTCDate()}</span>
                    </th>
                  );
                })}
                <th scope="col" className="px-2 py-2 text-right font-medium">A</th>
                <th scope="col" className="px-2 py-2 text-right font-medium">L</th>
                <th scope="col" className="px-3 py-2 text-right font-medium">%</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {sheet.data.staff.map((s) => (
                <tr key={s.staffId} className="hover:bg-sunken/40">
                  <th scope="row" className="sticky left-0 z-10 max-w-56 bg-surface px-3 py-1.5 text-left font-normal">
                    <Link href={`/staff/${s.staffId}`} className="block truncate text-sm text-ink hover:underline">{s.fullName}</Link>
                  </th>
                  {s.marks.map((mark, i) => {
                    const day = sheet.data.days[i]!;
                    return (
                      <td key={day.date} className={cn("h-8 w-7 p-0 text-center", !day.isSchoolDay && "bg-sunken")}>
                        {mark && (
                          <Tooltip content={`${formatDate(day.date, { weekday: true })}: ${staffAttendanceLabel[mark]}`}>
                            <span className={cn("mx-auto grid h-6 min-w-6 place-items-center rounded-sm px-0.5 text-[10px]",
                              mark === "Present" ? "text-ink-3" : mark === "Absent" ? "bg-danger-soft font-semibold text-danger" : mark === "OnLeave" ? "bg-info-soft font-medium text-info" : "font-medium text-ink")}>
                              {mark === "Present" ? "·" : staffAttendanceCode[mark]}
                            </span>
                          </Tooltip>
                        )}
                      </td>
                    );
                  })}
                  <td className={cn("px-2 text-right", s.totals.absent > 0 ? "text-danger" : "text-ink-3")}>{s.totals.absent}</td>
                  <td className="px-2 text-right text-ink-2">{s.totals.onLeave}</td>
                  <td className="px-3 text-right font-medium text-ink">{formatPercent(s.totals.percentage, 0)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ───────────────────────────── Corrections ─────────────────────────────

function CorrectionsView() {
  const [status, setStatus] = React.useState<"Pending" | "all">("Pending");
  const corrections = useStaffCorrections({ status: status === "all" ? undefined : status, pageSize: 50 });
  return (
    <div className="grid gap-3">
      <Segmented size="sm" aria-label="Show" value={status} onChange={setStatus}
        options={[{ value: "Pending", label: "Awaiting approval" }, { value: "all", label: "All" }]} />
      {corrections.isPending ? <Skeleton className="h-40" /> : corrections.error ? <ErrorState error={corrections.error} /> : corrections.data.items.length === 0 ? (
        <div className="rounded-lg border border-line bg-surface">
          <EmptyState compact icon={<ClipboardCheck />} title="Nothing waiting" description="Staff ask for corrections to their own days from their teacher page." />
        </div>
      ) : (
        <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
          {corrections.data.items.map((c) => <StaffCorrectionRow key={c.id} correction={c} />)}
        </ul>
      )}
    </div>
  );
}

function StaffCorrectionRow({ correction: c }: { correction: StaffCorrection }) {
  const decide = useDecideStaffCorrection();
  const withdraw = useWithdrawStaffCorrection();
  const [rejecting, setRejecting] = React.useState(false);
  const [note, setNote] = React.useState("");
  const act = async (approve: boolean) => {
    try {
      await decide.mutateAsync({ id: c.id, approve, note: note.trim() || null });
      toast.success(approve ? "Correction approved" : "Correction rejected");
      setRejecting(false);
    } catch (error) {
      toast.error(error, "The correction couldn't be decided");
    }
  };
  return (
    <li className="grid gap-x-4 gap-y-2 px-4 py-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)_auto] md:items-center">
      <div className="grid leading-tight">
        <Link href={`/staff/${c.staffId}`} className="font-medium text-ink hover:underline">{c.staffName}</Link>
        <span className="text-xs text-ink-2">{formatDate(c.date, { weekday: true })} · {c.fromStatus ? staffAttendanceLabel[c.fromStatus] : "Not marked"} → <span className="font-medium text-ink">{staffAttendanceLabel[c.toStatus]}</span></span>
      </div>
      <div className="grid gap-0.5 leading-tight">
        <span className="text-sm text-ink">{c.reason}</span>
        <span className="text-xs text-ink-3">{c.requestedBySelf ? "Asked by themselves" : `Asked by ${c.requestedByName ?? "—"}`} · {formatDateTime(c.requestedAt)}</span>
        {c.decisionNote && <span className="text-xs text-ink-2">“{c.decisionNote}”</span>}
      </div>
      <div className="flex items-center justify-end gap-2">
        {c.canDecide ? (
          <>
            <Button size="sm" variant="ghost" onClick={() => setRejecting(true)}>Reject…</Button>
            <Button size="sm" variant="primary" loading={decide.isPending && !rejecting} onClick={() => void act(true)}>Approve</Button>
          </>
        ) : c.canWithdraw ? (
          <Button size="sm" variant="ghost" loading={withdraw.isPending} onClick={() => void withdraw.mutateAsync(c.id).then(() => toast.success("Correction withdrawn"), (e) => toast.error(e))}>Withdraw</Button>
        ) : (
          <StatusBadge tone={correctionStatusTone[c.status]}>{correctionStatusLabel[c.status]}</StatusBadge>
        )}
      </div>
      <ConfirmDialog open={rejecting} onOpenChange={setRejecting} title="Reject this correction?" tone="danger" confirmLabel="Reject correction"
        description="The staff register stays as it is. Your note is shown to the person who asked." loading={decide.isPending} onConfirm={() => act(false)}>
        <Input autoFocus value={note} onChange={(e) => setNote(e.target.value)} placeholder="Reason (required)" className="mt-2" aria-label="Reason for rejecting" />
      </ConfirmDialog>
    </li>
  );
}
