"use client";

import * as React from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { CircleAlert, FileSpreadsheet, Upload } from "lucide-react";
import { ApiError } from "@/lib/api/client";
import { todayIso, formatNumber } from "@/lib/format";
import { pluralize } from "@/lib/utils";
import { useSections } from "@/lib/api/org";
import {
  downloadImportTemplate,
  studentStatusLabel,
  useBulkTransfer,
  useChangeStudentStatus,
  useImportStudents,
  useTransferStudent,
  type ImportReport,
  type StudentStatus,
} from "@/lib/api/students";
import { Button } from "@/components/ui/button";
import { Input, Textarea } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Checkbox, RadioGroup, RadioItem } from "@/components/ui/toggles";
import { Dialog, DialogBody, DialogContent, DialogFooter, DialogHeader } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toaster";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";
import { SectionSelect } from "./section-picker";

/** Inline notice for a full section, with the explicit choice to exceed capacity. */
function CapacityNotice({ message, checked, onCheckedChange }: { message: string; checked: boolean; onCheckedChange: (v: boolean) => void }) {
  return (
    <div className="grid gap-2.5 rounded-md border border-warning/40 bg-warning-soft px-3 py-2.5 text-sm">
      <p className="flex gap-2 text-ink">
        <CircleAlert className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
        {message}
      </p>
      <label className="flex items-center gap-2 pl-6 text-ink">
        <Checkbox checked={checked} onCheckedChange={(v) => onCheckedChange(v === true)} />
        Place anyway, over the section&apos;s capacity
      </label>
    </div>
  );
}

// ───────────────────────────── Move section ─────────────────────────────

const moveSchema = z.object({
  sectionId: z.string().min(1, "Choose the section."),
  effectiveDate: z.string().min(1, "Choose the date."),
  reason: z.string().trim().max(500).optional(),
});
type MoveValues = z.infer<typeof moveSchema>;

export type MovableStudent = { id: string; fullName: string; sectionId: string | null };

/** Moves one student, or a selection, to another section of the current year. */
export function MoveSectionDialog({ students, open, onOpenChange, onDone }: {
  students: MovableStudent[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDone?: () => void;
}) {
  const single = students.length === 1 ? students[0] : undefined;
  const transfer = useTransferStudent();
  const bulk = useBulkTransfer();
  const sections = useSections({});
  const form = useForm<MoveValues>({ resolver: zodResolver(moveSchema) });
  const [full, setFull] = React.useState<string | null>(null);
  const [override, setOverride] = React.useState(false);
  const [allClasses, setAllClasses] = React.useState(false);
  const sectionId = useWatch({ control: form.control, name: "sectionId" });

  const handleOpenChange = (next: boolean) => {
    if (!next) {
      setFull(null);
      setOverride(false);
      setAllClasses(false);
    }
    onOpenChange(next);
  };

  const byId = new Map((sections.data ?? []).map((s) => [s.id, s]));
  const grades = new Set(students.map((s) => (s.sectionId ? byId.get(s.sectionId)?.gradeId : undefined)).filter(Boolean));
  const gradeId = !allClasses && grades.size === 1 ? [...grades][0] : undefined;
  const target = sectionId ? byId.get(sectionId) : undefined;
  const otherClass = target ? students.filter((s) => s.sectionId && byId.get(s.sectionId)?.gradeId !== target.gradeId).length : 0;

  return (
    <FormDialog open={open} onOpenChange={handleOpenChange} form={form}
      defaults={{ sectionId: "", effectiveDate: todayIso(), reason: "" }}
      title={single ? `Move ${single.fullName}` : `Move ${formatNumber(students.length)} students`}
      description="The current placement closes the day before the move and stays in the student's history."
      submitLabel={override ? "Move over capacity" : "Move"}
      successMessage={single ? "Section changed" : `${pluralize(students.length, "student")} moved`}
      onSubmit={async (v) => {
        const body = { sectionId: v.sectionId, effectiveDate: v.effectiveDate, reason: nullIfEmpty(v.reason), overrideCapacity: override };
        try {
          if (single) await transfer.mutateAsync({ id: single.id, body });
          else await bulk.mutateAsync({ ...body, studentIds: students.map((s) => s.id) });
          onDone?.();
        } catch (error) {
          if (error instanceof ApiError && error.code === "section.full") {
            setFull(error.message);
            return false;
          }
          throw error;
        }
      }}>
      <Field label="New section" htmlFor="move-section" error={form.formState.errors.sectionId?.message}
        action={grades.size === 1 && (
          <button type="button" className="text-xs font-medium text-accent hover:underline" onClick={() => setAllClasses((v) => !v)}>
            {allClasses ? "Same class only" : "Show all classes"}
          </button>
        )}>
        <SectionSelect value={sectionId ?? ""} gradeId={gradeId} excludeId={single?.sectionId}
          invalid={!!form.formState.errors.sectionId}
          onChange={(id) => {
            form.setValue("sectionId", id, { shouldValidate: form.formState.isSubmitted });
            setFull(null);
            setOverride(false);
          }} />
      </Field>
      {otherClass > 0 && (
        <p className="flex gap-2 text-sm text-ink-2">
          <CircleAlert className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
          {single ? "This moves the student to a different class." : `${pluralize(otherClass, "selected student")} would change class.`}{" "}
          Moving between classes is normally done by promotion at year end.
        </p>
      )}
      <FieldRow>
        <Field label="Moves on" htmlFor="move-date" error={form.formState.errors.effectiveDate?.message}>
          <Input type="date" {...form.register("effectiveDate")} />
        </Field>
      </FieldRow>
      <Field label="Reason" htmlFor="move-reason" optional>
        <Input placeholder="e.g. Re-sectioning, parent request" {...form.register("reason")} />
      </Field>
      {full && <CapacityNotice message={full} checked={override} onCheckedChange={setOverride} />}
    </FormDialog>
  );
}

// ───────────────────────────── Status ─────────────────────────────

type StatusAction = { status: StudentStatus; label: string; description: string; needsReason?: boolean; needsSection?: boolean; destination?: boolean };

function actionsFor(status: StudentStatus): StatusAction[] {
  const leave: StatusAction[] = [
    { status: "Withdrawn", label: "Withdraw", description: "The family has taken the student out of school.", needsReason: true },
    { status: "TransferredOut", label: "Transfer to another school", description: "Record where the student is going for the leaving certificate.", needsReason: true, destination: true },
    { status: "Graduated", label: "Graduate", description: "The student completed their final class." },
  ];
  switch (status) {
    case "Active":
      return [{ status: "Suspended", label: "Suspend", description: "Temporarily away from class; the place is kept.", needsReason: true }, ...leave];
    case "Suspended":
      return [{ status: "Active", label: "Reinstate", description: "The student returns to their class." }, ...leave];
    case "Inactive":
      return [{ status: "Active", label: "Reactivate", description: "The student returns to their class." }, ...leave];
    case "Graduated":
      return [
        { status: "Alumni", label: "Move to alumni", description: "Keep in touch through the alumni register." },
        { status: "Active", label: "Re-admit", description: "The student rejoins a class.", needsSection: true },
      ];
    case "Withdrawn":
    case "TransferredOut":
      return [{ status: "Active", label: "Re-admit", description: "The student rejoins a class; the earlier placement stays in history.", needsSection: true }];
    default:
      return [];
  }
}

const statusSchema = z.object({
  status: z.string().min(1, "Choose what happens."),
  effectiveDate: z.string().min(1, "Choose the date."),
  reason: z.string().trim().max(500).optional(),
  destinationSchool: z.string().trim().max(200).optional(),
  sectionId: z.string().optional(),
});
type StatusValues = z.infer<typeof statusSchema>;

export function ChangeStatusDialog({ student, open, onOpenChange }: {
  student: { id: string; fullName: string; status: StudentStatus } | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const change = useChangeStudentStatus();
  const form = useForm<StatusValues>({ resolver: zodResolver(statusSchema) });
  const [full, setFull] = React.useState<string | null>(null);
  const [override, setOverride] = React.useState(false);
  const actions = student ? actionsFor(student.status) : [];
  const [chosen, sectionId] = useWatch({ control: form.control, name: ["status", "sectionId"] });
  const action = actions.find((a) => a.status === chosen);

  const handleOpenChange = (next: boolean) => {
    if (!next) {
      setFull(null);
      setOverride(false);
    }
    onOpenChange(next);
  };

  return (
    <FormDialog open={open && !!student} onOpenChange={handleOpenChange} form={form} size="md"
      defaults={{ status: actions.length === 1 ? actions[0]!.status : "", effectiveDate: todayIso(), reason: "", destinationSchool: "", sectionId: "" }}
      title={`Change status · ${student?.fullName ?? ""}`}
      description={student ? `Currently ${studentStatusLabel[student.status].toLowerCase()}. Every change is kept in the student's history.` : undefined}
      submitLabel={action?.label ?? "Save"}
      successMessage={() => `${student?.fullName} · ${action ? studentStatusLabel[action.status].toLowerCase() : "updated"}`}
      onSubmit={async (v) => {
        if (!student || !action) return false;
        if (action.needsReason && !v.reason?.trim()) {
          form.setError("reason", { message: "Record the reason." });
          return false;
        }
        if (action.needsSection && !v.sectionId) {
          form.setError("sectionId", { message: "Choose the section the student rejoins." });
          return false;
        }
        try {
          await change.mutateAsync({
            id: student.id,
            body: {
              status: action.status, effectiveDate: v.effectiveDate, reason: nullIfEmpty(v.reason),
              destinationSchool: action.destination ? nullIfEmpty(v.destinationSchool) : null,
              ...(action.needsSection ? { sectionId: v.sectionId, overrideCapacity: override } : {}),
            },
          });
        } catch (error) {
          if (error instanceof ApiError && error.code === "section.full") {
            setFull(error.message);
            return false;
          }
          throw error;
        }
      }}>
      <RadioGroup value={chosen ?? ""} onValueChange={(v) => form.setValue("status", v, { shouldValidate: true })} className="grid gap-2"
        aria-label="What happens">
        {actions.map((a) => (
          <label key={a.status} className="flex cursor-pointer items-start gap-3 rounded-md border border-line px-3 py-2.5 has-[[data-state=checked]]:border-ink/40 has-[[data-state=checked]]:bg-sunken/70">
            <RadioItem value={a.status} className="mt-0.5" />
            <span className="grid gap-0.5">
              <span className="text-sm font-medium text-ink">{a.label}</span>
              <span className="text-sm text-ink-2">{a.description}</span>
            </span>
          </label>
        ))}
      </RadioGroup>
      {form.formState.errors.status && <p className="text-sm text-danger">{form.formState.errors.status.message}</p>}
      {action && (
        <>
          <FieldRow>
            <Field label="Effective" htmlFor="status-date" error={form.formState.errors.effectiveDate?.message}>
              <Input type="date" {...form.register("effectiveDate")} />
            </Field>
          </FieldRow>
          {action.needsSection && (
            <Field label="Rejoins" htmlFor="status-section" error={form.formState.errors.sectionId?.message}>
              <SectionSelect value={sectionId ?? ""} invalid={!!form.formState.errors.sectionId}
                onChange={(id) => {
                  form.setValue("sectionId", id);
                  form.clearErrors("sectionId");
                  setFull(null);
                }} />
            </Field>
          )}
          {action.destination && (
            <Field label="New school" htmlFor="status-destination" optional>
              <Input placeholder="School name and city" {...form.register("destinationSchool")} />
            </Field>
          )}
          <Field label="Reason" htmlFor="status-reason" optional={!action.needsReason} error={form.formState.errors.reason?.message}>
            <Textarea rows={2} {...form.register("reason")} />
          </Field>
          {full && <CapacityNotice message={full} checked={override} onCheckedChange={setOverride} />}
        </>
      )}
    </FormDialog>
  );
}

// ───────────────────────────── Import ─────────────────────────────

/** Two steps: check the whole file, then import every row or none. */
export function ImportStudentsDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const importer = useImportStudents();
  const [file, setFile] = React.useState<File | null>(null);
  const [report, setReport] = React.useState<ImportReport | null>(null);
  const inputRef = React.useRef<HTMLInputElement>(null);

  const handleOpenChange = (next: boolean) => {
    if (!next) {
      setFile(null);
      setReport(null);
    }
    onOpenChange(next);
  };

  const run = async (commit: boolean) => {
    if (!file) return;
    try {
      const result = await importer.mutateAsync({ file, commit });
      setReport(result);
      if (result.committed) {
        toast.success(`Imported ${pluralize(result.created, "student")}`);
        handleOpenChange(false);
      }
    } catch (error) {
      toast.error(error, "The file couldn't be read");
    }
  };

  const ready = report && report.errors.length === 0 && report.validRows > 0;
  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent size="lg">
        <DialogHeader title="Import students"
          description="Add many students at once from a CSV file. The file is checked first; nothing is saved until every row is valid." />
        <DialogBody className="grid gap-4">
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-line bg-sunken/50 px-3 py-2.5">
            <p className="flex items-center gap-2 text-sm text-ink-2">
              <FileSpreadsheet className="size-4 text-ink-3" aria-hidden />
              Start from the template: one row per student with their class, section and a parent&apos;s mobile.
            </p>
            <Button size="sm" variant="secondary" onClick={() => void downloadImportTemplate().catch((e) => toast.error(e))}>Download template</Button>
          </div>
          <div className="flex items-center gap-3">
            <input ref={inputRef} type="file" accept=".csv,text/csv" className="sr-only" aria-label="Choose a CSV file"
              onChange={(e) => {
                setFile(e.target.files?.[0] ?? null);
                setReport(null);
              }} />
            <Button variant="secondary" onClick={() => inputRef.current?.click()}><Upload /> Choose file</Button>
            <span className="truncate text-sm text-ink-2">{file ? file.name : "No file chosen"}</span>
          </div>
          {report && (
            <div className="grid gap-3">
              <p className="text-sm text-ink">
                {formatNumber(report.totalRows)} {report.totalRows === 1 ? "row" : "rows"} checked ·{" "}
                <span className={report.errors.length ? "text-danger" : "text-ink"}>
                  {report.errors.length ? `${pluralize(report.errors.length, "problem")} to fix` : "no problems found"}
                </span>
              </p>
              {report.errors.length > 0 && (
                <div className="max-h-64 overflow-auto rounded-md border border-line">
                  <table className="w-full text-sm">
                    <thead className="sticky top-0 bg-sunken text-left text-xs text-ink-2">
                      <tr><th className="px-3 py-2 font-medium">Row</th><th className="px-3 py-2 font-medium">Column</th><th className="px-3 py-2 font-medium">Problem</th></tr>
                    </thead>
                    <tbody>
                      {report.errors.slice(0, 200).map((e, i) => (
                        <tr key={i} className="border-t border-line">
                          <td className="px-3 py-1.5 tabular text-ink-2">{e.row}</td>
                          <td className="px-3 py-1.5 font-mono text-xs text-ink-2">{e.column}</td>
                          <td className="px-3 py-1.5 text-ink">{e.message}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </DialogBody>
        <DialogFooter>
          <Button variant="secondary" onClick={() => handleOpenChange(false)}>Cancel</Button>
          {ready ? (
            <Button variant="primary" loading={importer.isPending} onClick={() => void run(true)}>
              Import {pluralize(report.validRows, "student")}
            </Button>
          ) : (
            <Button variant="primary" disabled={!file} loading={importer.isPending} onClick={() => void run(false)}>Check file</Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
