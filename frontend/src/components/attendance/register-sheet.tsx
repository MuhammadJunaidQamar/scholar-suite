"use client";

import * as React from "react";
import Link from "next/link";
import { CalendarOff, Check, CheckCheck, CircleAlert, Clock, HeartPulse, Lock, LockOpen, MessageSquareOff, Send, Undo2 } from "lucide-react";
import {
  attendanceCode,
  attendanceKey,
  attendanceLabel,
  attendanceStatuses,
  attendanceTone,
  correctionStatusLabel,
  type AttendanceStatus,
  type Correction,
  type RegisterRow,
  type RegisterSheet,
  useDecideCorrection,
  useRequestCorrection,
  useSaveRegister,
  useWithdrawCorrection,
} from "@/lib/api/attendance";
import { formatDateTime, formatTime } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Badge, Kbd, StatusBadge } from "@/components/ui/display";
import { Input } from "@/components/ui/input";
import { Segmented } from "@/components/ui/toggles";
import { Tooltip } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { ConfirmDialog } from "@/components/ui/dialog";
import { RecordAvatar } from "@/components/records/record-avatar";
import { MarkControl, type MarkOption } from "./mark-control";

type Mark = { status: AttendanceStatus; remark: string | null; arrivedAt: string | null };
type Draft = Record<string, Mark>;

export const studentMarkOptions: MarkOption<AttendanceStatus>[] = attendanceStatuses.map((s) => ({
  value: s,
  label: attendanceLabel[s],
  code: attendanceCode[s],
  // Present is the norm: it stays quiet so the exceptions are what the eye finds.
  tone: s === "Present" ? "neutral" : attendanceTone[s],
  shortcut: attendanceKey[s],
}));

const keyToStatus = Object.fromEntries(attendanceStatuses.map((s) => [attendanceKey[s], s])) as Record<string, AttendanceStatus>;

const fromRows = (rows: RegisterRow[]): Draft =>
  Object.fromEntries(rows.map((r) => [r.studentId, { status: r.status, remark: r.remark, arrivedAt: r.arrivedAt?.slice(0, 5) ?? null }]));

const stripe: Record<AttendanceStatus, string> = {
  Present: "",
  Late: "shadow-[inset_2px_0_0_var(--warning)]",
  Absent: "shadow-[inset_2px_0_0_var(--danger)] bg-danger-soft/40",
  Leave: "shadow-[inset_2px_0_0_var(--info)]",
};

function differs(a: Mark | undefined, row: RegisterRow) {
  if (!a) return false;
  return a.status !== row.status || (a.remark ?? "") !== (row.remark ?? "") || (a.arrivedAt ?? "") !== (row.arrivedAt?.slice(0, 5) ?? "");
}

/**
 * Taking a register: everyone starts Present (or Leave when leave is approved), the teacher marks the exceptions.
 * Keyboard: P / T / A / L mark the focused student and move on; ↑ ↓ move. Once the register locks, the same
 * screen collects a correction instead.
 */
export function RegisterSheetView({ sheet }: { sheet: RegisterSheet }) {
  const [draft, setDraft] = React.useState<Draft>(() => fromRows(sheet.students));
  const [draftKey, setDraftKey] = React.useState("");
  const [override, setOverride] = React.useState(false);
  const [correcting, setCorrecting] = React.useState(false);
  const [reason, setReason] = React.useState("");
  const [filter, setFilter] = React.useState<"all" | "exceptions">("all");
  const save = useSaveRegister();
  const requestCorrection = useRequestCorrection();

  // A different register (or a newer saved version) starts a fresh draft; background refetches of the same one don't.
  const key = `${sheet.sectionId}|${sheet.date}|${sheet.period ?? "day"}|${sheet.register?.version ?? "new"}`;
  if (key !== draftKey) {
    setDraftKey(key);
    setDraft(fromRows(sheet.students));
    setCorrecting(false);
    setReason("");
    setOverride(false);
  }

  const closed = !sheet.day.isSchoolDay;
  const editable = correcting || (sheet.canTake && (!closed || override));
  const rows = sheet.students;
  const changed = rows.filter((r) => differs(draft[r.studentId], r));
  const dirty = sheet.register ? changed.length > 0 : true;
  const visible = filter === "all" ? rows : rows.filter((r) => (draft[r.studentId]?.status ?? r.status) !== "Present");
  const counts = rows.filter((r) => r.hint?.kind !== "left").reduce(
    (acc, r) => {
      const s = draft[r.studentId]?.status ?? r.status;
      acc[s]++;
      return acc;
    },
    { Present: 0, Late: 0, Absent: 0, Leave: 0 } as Record<AttendanceStatus, number>,
  );
  const onRoll = rows.filter((r) => r.hint?.kind !== "left").length;
  const newAbsences = rows.filter((r) => draft[r.studentId]?.status === "Absent" && (!r.saved || r.status !== "Absent")).length;

  const setMark = (studentId: string, patch: Partial<Mark>) =>
    setDraft((d) => {
      const current = d[studentId] ?? { status: "Present" as AttendanceStatus, remark: null, arrivedAt: null };
      const next = { ...current, ...patch };
      if (patch.status === "Present") {
        next.remark = null;
        next.arrivedAt = null;
      }
      if (patch.status && patch.status !== "Late") next.arrivedAt = null;
      return { ...d, [studentId]: next };
    });

  const markAllPresent = () =>
    setDraft((d) => {
      const next = { ...d };
      for (const r of rows) {
        if (r.hint?.kind === "leave" || r.hint?.kind === "suspended" || r.hint?.kind === "left") continue;
        next[r.studentId] = { status: "Present", remark: null, arrivedAt: null };
      }
      return next;
    });

  const moveFocus = (from: HTMLElement, step: number) => {
    const list = from.closest("[data-register]")?.querySelectorAll<HTMLElement>("[data-row]");
    if (!list) return;
    const items = Array.from(list);
    const index = items.indexOf(from);
    items[Math.min(items.length - 1, Math.max(0, index + step))]?.focus();
  };

  const onRowKey = (e: React.KeyboardEvent<HTMLElement>, studentId: string) => {
    if ((e.target as HTMLElement).closest("input,textarea,select,button")) return;
    const k = e.key.toLowerCase();
    if (editable && keyToStatus[k]) {
      e.preventDefault();
      setMark(studentId, { status: keyToStatus[k] });
      moveFocus(e.currentTarget, 1);
    } else if (k === "arrowdown" || k === "j") {
      e.preventDefault();
      moveFocus(e.currentTarget, 1);
    } else if (k === "arrowup" || k === "k") {
      e.preventDefault();
      moveFocus(e.currentTarget, -1);
    }
  };

  const submit = async () => {
    try {
      const saved = await save.mutateAsync({
        sectionId: sheet.sectionId,
        date: sheet.date,
        period: sheet.period,
        subjectId: sheet.subjectId,
        note: sheet.register?.note ?? null,
        version: sheet.register?.version ?? null,
        overrideClosedDay: override,
        marks: rows.map((r) => {
          const m = draft[r.studentId] ?? { status: r.status, remark: r.remark, arrivedAt: null };
          return { studentId: r.studentId, status: m.status, remark: m.remark?.trim() || null, arrivedAt: m.status === "Late" && m.arrivedAt ? `${m.arrivedAt}:00` : null };
        }),
      });
      const alerts = saved.alerts;
      const detail = alerts && alerts.total > 0
        ? alerts.notSent > 0
          ? `${alerts.notSent} guardian alert${alerts.notSent === 1 ? "" : "s"} recorded but not sent: no SMS or WhatsApp provider is connected.`
          : `${alerts.queued + alerts.sent + alerts.delivered} guardian alert${alerts.total === 1 ? "" : "s"} handed to the provider.`
        : undefined;
      toast.success(sheet.register ? "Register updated" : "Register saved", detail);
    } catch (error) {
      toast.error(error, "The register couldn't be saved");
    }
  };

  const submitCorrection = async () => {
    if (!sheet.register) return;
    try {
      const result = await requestCorrection.mutateAsync({
        registerId: sheet.register.id,
        body: {
          reason: reason.trim(),
          changes: changed.map((r) => ({ studentId: r.studentId, status: draft[r.studentId]!.status, remark: draft[r.studentId]!.remark?.trim() || null })),
        },
      });
      toast.success(result.status === "Approved" ? "Correction applied" : "Correction sent for approval",
        result.status === "Approved" ? "Your own corrections apply at once and stay on record." : "Nothing changes until someone with approval rights accepts it.");
      setCorrecting(false);
    } catch (error) {
      toast.error(error, "The correction couldn't be sent");
    }
  };

  return (
    <div className="grid gap-4">
      <StatusLine sheet={sheet} closed={closed} override={override} onOverride={() => setOverride(true)}
        correcting={correcting} onCorrect={() => setCorrecting(true)} />

      {sheet.pendingCorrection && <PendingCorrection correction={sheet.pendingCorrection} />}

      <section data-register className="rounded-lg border border-line bg-surface">
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2 border-b border-line px-4 py-2.5">
          <CountsLine counts={counts} onRoll={onRoll} />
          <div className="ml-auto flex items-center gap-2">
            <Segmented size="sm" aria-label="Show" value={filter} onChange={setFilter}
              options={[{ value: "all", label: "Everyone" }, { value: "exceptions", label: `Not present · ${onRoll - counts.Present}` }]} />
            {editable && !correcting && (
              <Button size="sm" variant="ghost" onClick={markAllPresent}><CheckCheck /> All present</Button>
            )}
          </div>
        </div>

        {visible.length === 0 ? (
          <p className="px-4 py-10 text-center text-sm text-ink-2">
            {rows.length === 0 ? "No students are placed in this section on this date." : "Everyone is present."}
          </p>
        ) : (
          <ul className="divide-y divide-line">
            {visible.map((row) => (
              <RegisterLine key={row.studentId} row={row} mark={draft[row.studentId] ?? { status: row.status, remark: row.remark, arrivedAt: null }}
                editable={editable && row.hint?.kind !== "left"} changed={!!sheet.register && differs(draft[row.studentId], row)}
                onChange={(patch) => setMark(row.studentId, patch)} onKeyDown={(e) => onRowKey(e, row.studentId)} />
            ))}
          </ul>
        )}

        {editable && (
          <div className="sticky bottom-0 z-10 flex flex-wrap items-center gap-x-4 gap-y-2 rounded-b-lg border-t border-line bg-surface px-4 py-3 shadow-[0_-6px_16px_-12px_oklch(0.2_0.01_70/0.25)]">
            {correcting ? (
              <>
                <div className="min-w-0 flex-1 basis-72">
                  <Input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Why does the register need correcting? (required)"
                    aria-label="Reason for the correction" maxLength={1000} />
                </div>
                <span className="text-sm text-ink-2 tabular">{changed.length} change{changed.length === 1 ? "" : "s"}</span>
                <Button variant="ghost" onClick={() => { setCorrecting(false); setDraft(fromRows(rows)); }}>Cancel</Button>
                <Button variant="primary" loading={requestCorrection.isPending} disabled={changed.length === 0 || reason.trim().length < 5}
                  onClick={() => void submitCorrection()}>
                  <Send /> {sheet.canApprove ? "Apply correction" : "Request correction"}
                </Button>
              </>
            ) : (
              <>
                <AlertPreview sheet={sheet} newAbsences={newAbsences} />
                <span className="hidden text-xs text-ink-3 lg:inline-flex lg:items-center lg:gap-1">
                  <Kbd>P</Kbd><Kbd>T</Kbd><Kbd>A</Kbd><Kbd>L</Kbd> mark · <Kbd>↑</Kbd><Kbd>↓</Kbd> move
                </span>
                <div className="ml-auto flex items-center gap-2">
                  {sheet.register && dirty && (
                    <Button variant="ghost" onClick={() => setDraft(fromRows(rows))}><Undo2 /> Discard changes</Button>
                  )}
                  <Button variant="primary" loading={save.isPending} disabled={!dirty || rows.length === 0} onClick={() => void submit()}>
                    <Check /> {sheet.register ? (dirty ? "Save changes" : "Saved") : "Submit register"}
                  </Button>
                </div>
              </>
            )}
          </div>
        )}
      </section>
    </div>
  );
}

function CountsLine({ counts, onRoll }: { counts: Record<AttendanceStatus, number>; onRoll: number }) {
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-2" aria-live="polite">
      <span className="font-medium text-ink tabular">{onRoll} on roll</span>
      {attendanceStatuses.map((s) => (
        <StatusBadge key={s} tone={attendanceTone[s]} className="text-ink-2">
          <span className="tabular text-ink">{counts[s]}</span> {attendanceLabel[s].toLowerCase()}
        </StatusBadge>
      ))}
    </div>
  );
}

function RegisterLine({ row, mark, editable, changed, onChange, onKeyDown }: {
  row: RegisterRow;
  mark: Mark;
  editable: boolean;
  changed: boolean;
  onChange: (patch: Partial<Mark>) => void;
  onKeyDown: (e: React.KeyboardEvent<HTMLElement>) => void;
}) {
  const left = row.hint?.kind === "left";
  const extra = mark.status !== "Present";
  return (
    <li data-row tabIndex={0} onKeyDown={onKeyDown}
      className={cn(
        // One line per student on every screen; a note (and arrival time) opens a second line only when needed.
        "grid grid-cols-[1.5rem_minmax(0,1fr)_auto] items-center gap-x-3 gap-y-2 px-4 py-2 outline-none transition-colors sm:grid-cols-[1.75rem_minmax(0,1fr)_auto]",
        "focus-visible:bg-sunken focus-visible:shadow-[inset_0_0_0_2px_var(--focus)]",
        stripe[mark.status], left && "opacity-60",
      )}>
      <span className="text-right text-sm text-ink-3 tabular">{row.rollNumber ?? ""}</span>
      <div className="flex min-w-0 items-center gap-2.5">
        <RecordAvatar name={row.fullName} fileId={row.photoFileId} size={30} className="max-sm:hidden" />
        <div className="grid min-w-0 leading-tight">
          <span className="flex min-w-0 flex-wrap items-center gap-x-1.5 gap-y-0.5">
            <Link href={`/students/${row.studentId}`} className="truncate font-medium text-ink hover:underline" tabIndex={-1}>{row.fullName}</Link>
            {row.medicalAlert && (
              <Tooltip content={row.medicalAlert}>
                <HeartPulse className="size-3.5 shrink-0 text-danger" aria-label={`Medical alert: ${row.medicalAlert}`} />
              </Tooltip>
            )}
            {row.hint && <HintBadge row={row} />}
            {changed && <Badge tone="accent">Changed</Badge>}
          </span>
          <span className="truncate text-xs text-ink-2">
            <span className="font-mono max-sm:hidden">{row.admissionNumber}</span>
            {row.recentAbsences >= 3 && (
              <span className="text-[oklch(from_var(--warning)_calc(l-0.16)_c_h)] dark:text-warning"><span className="max-sm:hidden"> · </span>{row.recentAbsences} absences in 30 days</span>
            )}
          </span>
        </div>
      </div>
      <div className="flex items-center justify-end gap-2">
        {extra && (
          <div className="flex items-center gap-2 max-sm:hidden">
            <ExtraInputs row={row} mark={mark} editable={editable} onChange={onChange} />
          </div>
        )}
        <MarkControl label={`Attendance for ${row.fullName}`} value={mark.status} options={studentMarkOptions} disabled={!editable}
          onChange={(status) => onChange({ status })} />
      </div>
      {extra && (
        <div className="col-span-2 col-start-2 flex items-center gap-2 sm:hidden">
          <ExtraInputs row={row} mark={mark} editable={editable} onChange={onChange} />
        </div>
      )}
    </li>
  );
}

function ExtraInputs({ row, mark, editable, onChange }: { row: RegisterRow; mark: Mark; editable: boolean; onChange: (patch: Partial<Mark>) => void }) {
  return (
    <>
      {mark.status === "Late" && (
        <Input type="time" inputSize="sm" aria-label={`Arrival time for ${row.fullName}`} className="w-28 shrink-0" value={mark.arrivedAt ?? ""}
          disabled={!editable} onChange={(e) => onChange({ arrivedAt: e.target.value || null })} leading={<Clock className="size-3.5" />} />
      )}
      <Input inputSize="sm" aria-label={`Note for ${row.fullName}`} placeholder="Note" className="w-full sm:w-44" maxLength={300}
        value={mark.remark ?? ""} disabled={!editable} onChange={(e) => onChange({ remark: e.target.value })} />
    </>
  );
}

function HintBadge({ row }: { row: RegisterRow }) {
  const hint = row.hint!;
  const tone = hint.kind === "leave" ? "info" : hint.kind === "suspended" ? "warning" : hint.kind === "pendingLeave" ? "warning" : "neutral";
  return <Badge tone={tone}>{hint.text}</Badge>;
}

function StatusLine({ sheet, closed, override, onOverride, correcting, onCorrect }: {
  sheet: RegisterSheet;
  closed: boolean;
  override: boolean;
  onOverride: () => void;
  correcting: boolean;
  onCorrect: () => void;
}) {
  const r = sheet.register;
  let icon = <LockOpen className="size-4 text-ink-3" />;
  let text: React.ReactNode;
  let action: React.ReactNode = null;

  if (sheet.day.isFuture) {
    icon = <CalendarOff className="size-4 text-ink-3" />;
    text = "This day hasn't happened yet. Registers are taken on the day.";
  } else if (closed && !r) {
    icon = <CalendarOff className="size-4 text-ink-3" />;
    text = override
      ? <>Taking a register on a closed day ({sheet.day.closedReason}). It will count as a school day for this section.</>
      : <>{sheet.day.closedReason}. No register is expected.</>;
    if (sheet.canOverrideClosedDay && !override) action = <Button size="sm" variant="secondary" onClick={onOverride}>Take anyway (make-up day)</Button>;
  } else if (!r) {
    text = sheet.canTake
      ? <>Not taken yet. Everyone starts as present — mark the exceptions and submit.{sheet.locksAt && <span className="text-ink-3"> Open until {formatDateTime(sheet.locksAt)}.</span>}</>
      : "Not taken yet.";
  } else if (sheet.isLocked) {
    icon = <Lock className="size-4 text-ink-3" />;
    text = correcting
      ? "Change the marks that are wrong, give the reason, and send the correction."
      : <>Taken {formatTime(r.takenAt)}{r.takenByName && ` by ${r.takenByName}`}. Locked since {formatDateTime(sheet.locksAt)} — changes need an approved correction.</>;
    if (sheet.canCorrect && !correcting && !sheet.pendingCorrection) action = <Button size="sm" variant="secondary" onClick={onCorrect}>Request a correction</Button>;
  } else {
    text = (
      <>
        Taken {formatTime(r.takenAt)}{r.takenByName && ` by ${r.takenByName}`}
        {r.updatedAt && <>, last changed {formatTime(r.updatedAt)}{r.updatedByName && ` by ${r.updatedByName}`}</>}.
        {sheet.locksAt && <span className="text-ink-3"> Open for changes until {formatDateTime(sheet.locksAt)}.</span>}
      </>
    );
  }

  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border border-line bg-surface px-4 py-2.5 text-sm text-ink-2">
      {icon}
      <p className="min-w-0 flex-1">{text}</p>
      {action}
    </div>
  );
}

function AlertPreview({ sheet, newAbsences }: { sheet: RegisterSheet; newAbsences: number }) {
  if (sheet.period !== null || !sheet.alertPolicy.notifyAbsent) return <span className="text-sm text-ink-3">{sheet.period !== null ? "Period registers don\u2019t alert guardians." : "Guardian alerts are switched off."}</span>;
  if (!sheet.day.isToday) return <span className="text-sm text-ink-3">Guardian alerts go out only for today&apos;s register.</span>;
  if (newAbsences === 0) return <span className="text-sm text-ink-3">No new absences to alert guardians about.</span>;
  return sheet.alertPolicy.canSend ? (
    <span className="text-sm text-ink-2">Guardians of {newAbsences} absent student{newAbsences === 1 ? "" : "s"} will be alerted when you save.</span>
  ) : (
    <span className="inline-flex items-center gap-1.5 text-sm text-ink-2">
      <MessageSquareOff className="size-3.5 shrink-0 text-ink-3" />
      {newAbsences} absence alert{newAbsences === 1 ? "" : "s"} will be recorded, not sent — no SMS or WhatsApp provider is connected.
    </span>
  );
}

function PendingCorrection({ correction }: { correction: Correction }) {
  const decide = useDecideCorrection();
  const withdraw = useWithdrawCorrection();
  const [rejecting, setRejecting] = React.useState(false);
  const [note, setNote] = React.useState("");

  const act = async (approve: boolean) => {
    try {
      await decide.mutateAsync({ id: correction.id, approve, note: note.trim() || null });
      toast.success(approve ? "Correction approved" : "Correction rejected");
      setRejecting(false);
    } catch (error) {
      toast.error(error, "The correction couldn't be decided");
    }
  };

  return (
    <section className="rounded-lg border border-[color-mix(in_oklch,var(--warning)_40%,var(--line))] bg-warning-soft/60 px-4 py-3">
      <div className="flex flex-wrap items-start gap-3">
        <CircleAlert className="mt-0.5 size-4 shrink-0 text-[oklch(from_var(--warning)_calc(l-0.16)_c_h)] dark:text-warning" />
        <div className="grid min-w-0 flex-1 gap-1.5">
          <p className="text-sm text-ink">
            <span className="font-medium">{correctionStatusLabel[correction.status]}</span>
            <span className="text-ink-2"> · requested by {correction.requestedByName ?? "someone"} {formatDateTime(correction.requestedAt)}</span>
          </p>
          <p className="text-sm text-ink-2">“{correction.reason}”</p>
          <ul className="grid gap-0.5 text-sm">
            {correction.items.map((item) => (
              <li key={item.studentId} className="text-ink">
                {item.studentName}: <span className="text-ink-2">{item.fromStatus ? attendanceLabel[item.fromStatus] : "not marked"}</span> → <span className="font-medium">{attendanceLabel[item.toStatus]}</span>
                {item.remark && <span className="text-ink-2"> ({item.remark})</span>}
                {item.currentStatus !== item.fromStatus && <span className="text-danger"> · the mark has changed since</span>}
              </li>
            ))}
          </ul>
        </div>
        <div className="flex items-center gap-2">
          {correction.canWithdraw && <Button size="sm" variant="ghost" loading={withdraw.isPending} onClick={() => void withdraw.mutateAsync(correction.id).then(() => toast.success("Correction withdrawn"), (e) => toast.error(e))}>Withdraw</Button>}
          {correction.canDecide && (
            <>
              <Button size="sm" variant="secondary" onClick={() => setRejecting(true)}>Reject…</Button>
              <Button size="sm" variant="primary" loading={decide.isPending && !rejecting} onClick={() => void act(true)}>Approve</Button>
            </>
          )}
        </div>
      </div>
      <ConfirmDialog open={rejecting} onOpenChange={setRejecting} title="Reject this correction?" tone="danger" confirmLabel="Reject correction"
        description="The register stays as it is. Your note is shown to the person who asked." loading={decide.isPending} onConfirm={() => act(false)}>
        <Input autoFocus value={note} onChange={(e) => setNote(e.target.value)} placeholder="Reason (required)" className="mt-2" aria-label="Reason for rejecting" />
      </ConfirmDialog>
    </section>
  );
}
