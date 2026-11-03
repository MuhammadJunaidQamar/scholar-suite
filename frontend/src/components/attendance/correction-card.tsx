"use client";

import * as React from "react";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import {
  attendanceLabel,
  correctionStatusLabel,
  correctionStatusTone,
  useDecideCorrection,
  useWithdrawCorrection,
  type Correction,
} from "@/lib/api/attendance";
import { formatDate, formatDateTime } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/display";
import { ConfirmDialog } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { toast } from "@/components/ui/toaster";

/** One requested change to a locked register: what, why, by whom — and the decision buttons for approvers. */
export function CorrectionCard({ correction: c }: { correction: Correction }) {
  const decide = useDecideCorrection();
  const withdraw = useWithdrawCorrection();
  const [rejecting, setRejecting] = React.useState(false);
  const [note, setNote] = React.useState("");
  const stale = c.status === "Pending" && c.items.some((i) => i.currentStatus !== i.fromStatus);

  const act = async (approve: boolean) => {
    try {
      await decide.mutateAsync({ id: c.id, approve, note: note.trim() || null });
      toast.success(approve ? "Correction approved" : "Correction rejected", approve ? "The register now shows the corrected marks." : undefined);
      setRejecting(false);
      setNote("");
    } catch (error) {
      toast.error(error, "The correction couldn't be decided");
    }
  };

  return (
    <article className="rounded-lg border border-line bg-surface">
      <header className="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-line px-4 py-2.5">
        <Link href={`/attendance/register/${c.sectionId}?date=${c.date}${c.period ? `&period=${c.period}` : ""}`} className="inline-flex items-center gap-1 font-medium text-ink hover:underline">
          {c.className} · {formatDate(c.date, { weekday: true })}{c.period ? ` · Period ${c.period}` : ""} <ArrowRight className="size-3.5 text-ink-3" />
        </Link>
        <StatusBadge tone={correctionStatusTone[c.status]}>{correctionStatusLabel[c.status]}</StatusBadge>
        <span className="ml-auto text-sm text-ink-2">Asked by {c.requestedByName ?? "—"} · {formatDateTime(c.requestedAt)}</span>
      </header>
      <div className="grid gap-3 px-4 py-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1.2fr)]">
        <div className="grid content-start gap-1">
          <span className="text-xs font-medium text-ink-3">Reason</span>
          <p className="text-sm text-ink">{c.reason}</p>
          {c.decisionNote && (
            <p className="text-sm text-ink-2"><span className="text-ink-3">{c.decidedByName ?? "Approver"}:</span> {c.decisionNote}</p>
          )}
          {c.status !== "Pending" && c.decidedAt && <p className="text-xs text-ink-3">Decided {formatDateTime(c.decidedAt)}{c.decidedByName && ` by ${c.decidedByName}`}</p>}
        </div>
        <div className="grid content-start gap-1">
          <span className="text-xs font-medium text-ink-3">Changes</span>
          <ul className="grid gap-1 text-sm">
            {c.items.map((i) => (
              <li key={i.studentId} className="flex flex-wrap items-baseline gap-x-1.5">
                <span className="font-medium text-ink">{i.studentName}</span>
                <span className="text-ink-2">{i.fromStatus ? attendanceLabel[i.fromStatus] : "Not marked"} → <span className="text-ink">{attendanceLabel[i.toStatus]}</span></span>
                {i.remark && <span className="text-ink-3">“{i.remark}”</span>}
                {c.status === "Pending" && i.currentStatus !== i.fromStatus && (
                  <span className="text-danger">now {i.currentStatus ? attendanceLabel[i.currentStatus] : "not marked"}</span>
                )}
              </li>
            ))}
          </ul>
        </div>
      </div>
      {(c.canDecide || c.canWithdraw) && (
        <footer className="flex flex-wrap items-center justify-end gap-2 border-t border-line bg-sunken/40 px-4 py-2.5">
          {stale && <span className="mr-auto text-sm text-danger">The register has changed since this was asked. Reject it and ask for a new one.</span>}
          {c.canWithdraw && (
            <Button size="sm" variant="ghost" loading={withdraw.isPending}
              onClick={() => void withdraw.mutateAsync(c.id).then(() => toast.success("Correction withdrawn"), (e) => toast.error(e))}>Withdraw</Button>
          )}
          {c.canDecide && (
            <>
              <Button size="sm" variant="secondary" onClick={() => setRejecting(true)}>Reject…</Button>
              <Button size="sm" variant="primary" disabled={stale} loading={decide.isPending && !rejecting} onClick={() => void act(true)}>Approve</Button>
            </>
          )}
        </footer>
      )}
      <ConfirmDialog open={rejecting} onOpenChange={setRejecting} title="Reject this correction?" tone="danger" confirmLabel="Reject correction"
        description="The register stays as it is. Your note is shown to the person who asked." loading={decide.isPending} onConfirm={() => act(false)}>
        <Input autoFocus value={note} onChange={(e) => setNote(e.target.value)} placeholder="Reason (required)" className="mt-2" aria-label="Reason for rejecting" />
      </ConfirmDialog>
    </article>
  );
}
