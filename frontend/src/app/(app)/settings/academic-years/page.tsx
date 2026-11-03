"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { CalendarDays, CircleCheck, Lock, Pencil, Plus, Trash2 } from "lucide-react";
import { cn, pluralize } from "@/lib/utils";
import { formatDate, parseDateOnly, todayIso } from "@/lib/format";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import {
  useAcademicYears, useCloseYear, useDeleteAcademicYear, useDeleteTerm, useMakeCurrentYear, useSaveAcademicYear, useSaveTerm,
  type AcademicYear, type Term,
} from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Badge, Skeleton, StatusBadge } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { FormDialog } from "@/components/forms/form-dialog";

const dateRange = z.object({
  name: z.string().trim().min(1, "Enter a name.").max(60),
  startDate: z.string().min(1, "Choose a start date."),
  endDate: z.string().min(1, "Choose an end date."),
}).refine((v) => v.endDate > v.startDate, { message: "It must end after it starts.", path: ["endDate"] });
type RangeValues = z.infer<typeof dateRange>;

const days = (a: string, b: string) => (parseDateOnly(b).getTime() - parseDateOnly(a).getTime()) / 86_400_000;

function Timeline({ year }: { year: AcademicYear }) {
  const total = days(year.startDate, year.endDate);
  const today = todayIso();
  const todayOffset = today >= year.startDate && today <= year.endDate ? (days(year.startDate, today) / total) * 100 : null;
  return (
    <div className="relative mt-1 h-7" aria-hidden>
      <div className="absolute inset-x-0 top-3 h-px bg-line-strong" />
      {year.terms.map((term) => {
        const left = (days(year.startDate, term.startDate) / total) * 100;
        const width = (days(term.startDate, term.endDate) / total) * 100;
        const active = today >= term.startDate && today <= term.endDate;
        return (
          <div key={term.id} className={cn("absolute top-1.5 h-3 rounded-sm", active ? "bg-accent" : "bg-accent-line")}
            style={{ left: `${left}%`, width: `${width}%` }} />
        );
      })}
      {todayOffset !== null && (
        <div className="absolute top-0 h-6 w-px bg-ink" style={{ left: `${todayOffset}%` }}>
          <span className="absolute top-6 -translate-x-1/2 text-2xs font-medium whitespace-nowrap text-ink">Today</span>
        </div>
      )}
    </div>
  );
}

function RangeDialog({ open, onOpenChange, title, description, defaults, submitLabel, onSubmit, nameHint }: {
  open: boolean; onOpenChange: (o: boolean) => void; title: string; description?: string; defaults: RangeValues;
  submitLabel: string; onSubmit: (v: RangeValues) => Promise<unknown>; nameHint?: string;
}) {
  const form = useForm<RangeValues>({ resolver: zodResolver(dateRange) });
  const e = form.formState.errors;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} defaults={defaults} title={title} description={description}
      submitLabel={submitLabel} onSubmit={onSubmit} size="sm">
      <Field label="Name" htmlFor="r-name" error={e.name?.message} hint={nameHint}><Input autoFocus {...form.register("name")} /></Field>
      <FieldRow>
        <Field label="Starts" htmlFor="r-start" error={e.startDate?.message}><Input type="date" {...form.register("startDate")} /></Field>
        <Field label="Ends" htmlFor="r-end" error={e.endDate?.message}><Input type="date" {...form.register("endDate")} /></Field>
      </FieldRow>
    </FormDialog>
  );
}

type Dialog =
  | { kind: "year"; year: AcademicYear | null }
  | { kind: "term"; year: AcademicYear; term: Term | null }
  | { kind: "confirm"; action: "current" | "close" | "delete" | "deleteTerm"; year: AcademicYear; term?: Term };

export default function AcademicYearsPage() {
  const { can } = useSession();
  const editable = can(P.Academics.Manage);
  const years = useAcademicYears();
  const saveYear = useSaveAcademicYear();
  const saveTerm = useSaveTerm();
  const deleteTerm = useDeleteTerm();
  const makeCurrent = useMakeCurrentYear();
  const close = useCloseYear();
  const remove = useDeleteAcademicYear();
  const [dialog, setDialog] = React.useState<Dialog | null>(null);
  const closeDialog = () => setDialog(null);

  const nextYearDefaults = (): RangeValues => {
    const latest = years.data?.[0];
    if (!latest) {
      const y = new Date().getFullYear();
      return { name: `${y}–${String((y + 1) % 100).padStart(2, "0")}`, startDate: `${y}-08-01`, endDate: `${y + 1}-07-31` };
    }
    const start = parseDateOnly(latest.endDate);
    start.setUTCDate(start.getUTCDate() + 1);
    const end = new Date(start);
    end.setUTCFullYear(end.getUTCFullYear() + 1);
    end.setUTCDate(end.getUTCDate() - 1);
    const y = start.getUTCFullYear();
    return { name: `${y}–${String((y + 1) % 100).padStart(2, "0")}`, startDate: start.toISOString().slice(0, 10), endDate: end.toISOString().slice(0, 10) };
  };

  const confirmCopy = dialog?.kind === "confirm" ? {
    current: { title: `Make ${dialog.year.name} the current year?`, text: "Registers, fees and timetables will default to this year for everyone.", label: "Make current", tone: "primary" as const },
    close: { title: `Close ${dialog.year.name}?`, text: "A closed year becomes read-only. Do this after promotions and final results.", label: "Close year", tone: "danger" as const },
    delete: { title: `Delete ${dialog.year.name}?`, text: "Only years without sections can be deleted. This can't be undone.", label: "Delete year", tone: "danger" as const },
    deleteTerm: { title: `Remove ${dialog.term?.name}?`, text: "The remaining terms are renumbered by date.", label: "Remove term", tone: "danger" as const },
  }[dialog.action] : null;

  const runConfirm = async () => {
    if (dialog?.kind !== "confirm") return;
    try {
      if (dialog.action === "current") await makeCurrent.mutateAsync({ id: dialog.year.id });
      if (dialog.action === "close") await close.mutateAsync({ id: dialog.year.id });
      if (dialog.action === "delete") await remove.mutateAsync(dialog.year.id);
      if (dialog.action === "deleteTerm") await deleteTerm.mutateAsync({ yearId: dialog.year.id, termId: dialog.term!.id });
      toast.success({ current: "Current year changed", close: "Year closed", delete: "Year deleted", deleteTerm: "Term removed" }[dialog.action]);
      closeDialog();
    } catch (error) {
      toast.error(error);
    }
  };

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Academic years" }]} title="Academic years"
        description="Sections, registers, fees and results all belong to a year. Exactly one year is current."
        actions={editable && <Button variant="primary" onClick={() => setDialog({ kind: "year", year: null })}><Plus /> New academic year</Button>} />

      {years.isPending ? <Skeleton className="h-64" /> : years.error ? <ErrorState error={years.error} onRetry={() => void years.refetch()} /> :
        years.data.length === 0 ? (
          <div className="rounded-lg border border-dashed border-line-strong">
            <EmptyState icon={<CalendarDays />} title="No academic years yet" description="Create the current year first; sections and registers depend on it."
              action={editable && <Button variant="primary" size="sm" onClick={() => setDialog({ kind: "year", year: null })}><Plus /> New academic year</Button>} />
          </div>
        ) : (
          <div className="grid gap-5">
            {years.data.map((year) => (
              <section key={year.id} className={cn("rounded-lg border bg-surface", year.isCurrent ? "border-accent-line" : "border-line")}>
                <header className="flex flex-wrap items-start gap-x-4 gap-y-2 px-5 pt-4 pb-2">
                  <div className="grid flex-1 gap-1">
                    <div className="flex items-center gap-2.5">
                      <h2 className="font-display text-xl text-ink">{year.name}</h2>
                      {year.isCurrent ? <StatusBadge tone="accent">Current</StatusBadge> : year.status === "Closed" ?
                        <StatusBadge tone="neutral">Closed</StatusBadge> : <StatusBadge tone="info">Planned</StatusBadge>}
                    </div>
                    <p className="text-sm text-ink-2">
                      {formatDate(year.startDate)} – {formatDate(year.endDate)} · {pluralize(year.terms.length, "term")} · {pluralize(year.sectionCount, "section")}
                    </p>
                  </div>
                  {editable && year.status !== "Closed" && (
                    <div className="flex items-center gap-1.5">
                      <Button size="sm" variant="secondary" onClick={() => setDialog({ kind: "term", year, term: null })}><Plus /> Term</Button>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild><Button size="sm" variant="ghost">More</Button></DropdownMenuTrigger>
                        <DropdownMenuContent>
                          <DropdownMenuItem onSelect={() => setDialog({ kind: "year", year })}><Pencil /> Rename or change dates</DropdownMenuItem>
                          {!year.isCurrent && <DropdownMenuItem onSelect={() => setDialog({ kind: "confirm", action: "current", year })}><CircleCheck /> Make current</DropdownMenuItem>}
                          {!year.isCurrent && <DropdownMenuItem onSelect={() => setDialog({ kind: "confirm", action: "close", year })}><Lock /> Close year</DropdownMenuItem>}
                          {!year.isCurrent && year.sectionCount === 0 && (
                            <>
                              <DropdownMenuSeparator />
                              <DropdownMenuItem tone="danger" onSelect={() => setDialog({ kind: "confirm", action: "delete", year })}><Trash2 /> Delete</DropdownMenuItem>
                            </>
                          )}
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  )}
                </header>
                <div className="px-5 pb-3"><Timeline year={year} /></div>
                {year.terms.length > 0 ? (
                  <ul className="border-t border-line">
                    {year.terms.map((term) => (
                      <li key={term.id} className="group/row flex items-center gap-4 border-b border-line px-5 py-2.5 last:border-b-0">
                        <span className="w-6 text-sm text-ink-3 tabular">{term.sequence}</span>
                        <span className="flex-1 text-base font-medium">{term.name}</span>
                        <span className="text-sm text-ink-2 tabular">{formatDate(term.startDate)} – {formatDate(term.endDate)}</span>
                        <span className="hidden w-20 text-right text-sm text-ink-3 tabular sm:block">{Math.round(days(term.startDate, term.endDate) / 7)} weeks</span>
                        {editable && year.status !== "Closed" && (
                          <span className="flex gap-1 opacity-0 transition-opacity group-hover/row:opacity-100 [@media(hover:none)]:opacity-100">
                            <Button size="icon-xs" variant="ghost" aria-label={`Edit ${term.name}`} onClick={() => setDialog({ kind: "term", year, term })}><Pencil /></Button>
                            <Button size="icon-xs" variant="ghost" aria-label={`Remove ${term.name}`} onClick={() => setDialog({ kind: "confirm", action: "deleteTerm", year, term })}><Trash2 /></Button>
                          </span>
                        )}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="border-t border-line px-5 py-3 text-sm text-ink-2">No terms yet. Terms split the year for examinations and reports.</p>
                )}
              </section>
            ))}
          </div>
        )}

      {dialog?.kind === "year" && (
        <RangeDialog open onOpenChange={(o) => !o && closeDialog()}
          title={dialog.year ? `Edit ${dialog.year.name}` : "New academic year"}
          description={dialog.year ? undefined : "Add terms after creating it. The first year you create becomes current."}
          nameHint="e.g. 2027–28"
          defaults={dialog.year ? { name: dialog.year.name, startDate: dialog.year.startDate, endDate: dialog.year.endDate } : nextYearDefaults()}
          submitLabel={dialog.year ? "Save year" : "Create year"}
          onSubmit={async (v) => { await saveYear.mutateAsync({ id: dialog.year?.id, body: v }); toast.success(dialog.year ? "Year saved" : `${v.name} created`); }} />
      )}
      {dialog?.kind === "term" && (
        <RangeDialog open onOpenChange={(o) => !o && closeDialog()}
          title={dialog.term ? `Edit ${dialog.term.name}` : `New term in ${dialog.year.name}`}
          description={`Must fall between ${formatDate(dialog.year.startDate)} and ${formatDate(dialog.year.endDate)} without overlapping other terms.`}
          defaults={dialog.term ? { name: dialog.term.name, startDate: dialog.term.startDate, endDate: dialog.term.endDate }
            : { name: dialog.year.terms.length === 0 ? "First Term" : dialog.year.terms.length === 1 ? "Second Term" : "Third Term", startDate: "", endDate: "" }}
          submitLabel={dialog.term ? "Save term" : "Add term"}
          onSubmit={async (v) => { await saveTerm.mutateAsync({ yearId: dialog.year.id, termId: dialog.term?.id, body: v }); toast.success(dialog.term ? "Term saved" : "Term added"); }} />
      )}
      <ConfirmDialog open={dialog?.kind === "confirm"} onOpenChange={(o) => !o && closeDialog()}
        title={confirmCopy?.title ?? ""} description={confirmCopy?.text} confirmLabel={confirmCopy?.label} tone={confirmCopy?.tone}
        loading={makeCurrent.isPending || close.isPending || remove.isPending || deleteTerm.isPending} onConfirm={runConfirm} />
      {years.data && years.data.some((y) => y.status === "Closed") && (
        <p className="mt-6 flex items-center gap-1.5 text-sm text-ink-3"><Badge>Closed</Badge> years are kept for records and reports and can&apos;t be edited.</p>
      )}
    </Page>
  );
}
