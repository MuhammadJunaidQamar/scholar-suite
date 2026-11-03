"use client";

import * as React from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useSequences, useUpdateSequence, type ResetPolicy, type Sequence } from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Input, NativeSelect } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { RecordList, RecordRow } from "@/components/ui/record-list";
import { FormDialog } from "@/components/forms/form-dialog";

const labels: Record<string, { title: string; text: string }> = {
  admission: { title: "Admission numbers", text: "Given to each student on enrolment." },
  application: { title: "Application numbers", text: "Admission applications and online forms." },
  employee: { title: "Employee IDs", text: "Staff records." },
  invoice: { title: "Challan numbers", text: "Fee challans and vouchers." },
  receipt: { title: "Receipt numbers", text: "Payments received." },
  refund: { title: "Refund numbers", text: "Refunds paid out." },
  journal: { title: "Journal entries", text: "Ledger postings." },
  certificate: { title: "Certificate numbers", text: "Bonafide, character, leaving and transfer certificates." },
  purchase_order: { title: "Purchase orders", text: "Inventory purchases." },
  accession: { title: "Library accession numbers", text: "One per physical book copy." },
  payroll: { title: "Payroll runs", text: "Monthly payroll." },
};
const resetLabel: Record<ResetPolicy, string> = { Never: "Never restarts", Yearly: "Restarts each year", Monthly: "Restarts each month" };

/** Mirrors SequencePattern.Format on the server for instant previews while typing. */
function preview(pattern: string, prefix: string, value: number) {
  const now = new Date();
  const text = pattern
    .replaceAll("{prefix}", prefix.toUpperCase())
    .replaceAll("{yyyy}", String(now.getFullYear()))
    .replaceAll("{yy}", String(now.getFullYear() % 100).padStart(2, "0"))
    .replaceAll("{mm}", String(now.getMonth() + 1).padStart(2, "0"));
  return text.replace(/\{seq(?::(\d{1,2}))?\}/g, (_, width?: string) => String(value).padStart(width ? Number(width) : 1, "0"));
}

const schema = z
  .object({
    prefix: z.string().trim().min(1, "Enter a prefix.").max(20).regex(/^[A-Za-z0-9/-]+$/, "Letters, digits, '/' or '-'."),
    pattern: z.string().trim().min(1).max(80).refine((p) => p.includes("{seq"), "Include {seq} or {seq:N}."),
    resetPolicy: z.enum(["Never", "Yearly", "Monthly"]),
    nextNumber: z.coerce.number<number>().int("Whole numbers only.").min(1).max(999_999_999),
  })
  .superRefine((v, ctx) => {
    // Mirrors the server: a counter that restarts must print its period, or restarted numbers would repeat.
    if (v.resetPolicy !== "Never" && !v.pattern.includes("{yyyy}") && !v.pattern.includes("{yy}"))
      ctx.addIssue({ code: "custom", path: ["pattern"], message: "Numbers that restart must include the year ({yyyy} or {yy})." });
    else if (v.resetPolicy === "Monthly" && !v.pattern.includes("{mm}"))
      ctx.addIssue({ code: "custom", path: ["pattern"], message: "Numbers that restart each month must include {mm}." });
  });
type Values = z.infer<typeof schema>;

function SequenceDialog({ sequence, onOpenChange }: { sequence: Sequence | null; onOpenChange: (open: boolean) => void }) {
  const update = useUpdateSequence();
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  const [pattern = "", prefix = "", nextNumber] = useWatch({ control: form.control, name: ["pattern", "prefix", "nextNumber"] });
  const current = sequence?.nextValue ?? 1;
  return (
    <FormDialog open={sequence !== null} onOpenChange={onOpenChange} form={form}
      defaults={{ prefix: sequence?.prefix ?? "", pattern: sequence?.pattern ?? "", resetPolicy: sequence?.resetPolicy ?? "Never", nextNumber: current }}
      title={labels[sequence?.key ?? ""]?.title ?? "Numbering"} description="Changes apply to the next number issued. Existing numbers never change."
      submitLabel="Save format" successMessage="Numbering saved"
      onSubmit={({ nextNumber: next, ...v }) =>
        update.mutateAsync({ key: sequence!.key, body: next !== current ? { ...v, nextNumber: next } : v })}>
      <FieldRow>
        <Field label="Prefix" htmlFor="seq-prefix" error={form.formState.errors.prefix?.message}><Input className="uppercase" {...form.register("prefix")} /></Field>
        <Field label="Restarts" htmlFor="seq-reset">
          <NativeSelect {...form.register("resetPolicy")}>
            {(["Never", "Yearly", "Monthly"] as const).map((r) => <option key={r} value={r}>{resetLabel[r]}</option>)}
          </NativeSelect>
        </Field>
      </FieldRow>
      <Field label="Pattern" htmlFor="seq-pattern" error={form.formState.errors.pattern?.message}
        hint="{prefix} · {yyyy} or {yy} year · {mm} month · {seq:5} number padded to 5 digits">
        <Input className="font-mono text-sm" {...form.register("pattern")} />
      </Field>
      <Field label="Continue from" htmlFor="seq-next" error={form.formState.errors.nextNumber?.message}
        hint={`Raise this to carry on from a previous system. Numbers only move forward, so it can't go below ${current}.`}>
        <Input type="number" inputMode="numeric" min={current} className="w-40 font-mono tabular-nums" {...form.register("nextNumber")} />
      </Field>
      <div className="rounded-md border border-dashed border-line-strong bg-sunken/60 px-3 py-2.5">
        <p className="text-xs text-ink-3">Next number</p>
        <p className="font-mono text-lg text-ink">{preview(pattern, prefix, Number(nextNumber) || 1)}</p>
      </div>
    </FormDialog>
  );
}

export default function NumberingPage() {
  const { can } = useSession();
  const editable = can(P.School.Manage);
  const sequences = useSequences();
  const [editing, setEditing] = React.useState<Sequence | null>(null);
  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Numbering" }]} title="Numbering"
        description="Formats for numbers the system issues. Numbers are gap-free: an abandoned transaction gives its number back." />
      {sequences.isPending ? <Skeleton className="h-96" /> : sequences.error ? <ErrorState error={sequences.error} onRetry={() => void sequences.refetch()} /> : (
        <RecordList>
          {sequences.data.map((s) => (
            <RecordRow key={s.key} onClick={editable ? () => setEditing(s) : undefined}
              title={labels[s.key]?.title ?? s.key} subtitle={labels[s.key]?.text}
              facts={[resetLabel[s.resetPolicy], <span key="p" className="font-mono text-ink">{s.preview}</span>]} />
          ))}
        </RecordList>
      )}
      <SequenceDialog sequence={editing} onOpenChange={(o) => !o && setEditing(null)} />
    </Page>
  );
}
