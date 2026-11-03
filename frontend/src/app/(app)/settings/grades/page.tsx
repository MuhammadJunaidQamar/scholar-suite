"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ArrowRight, Copy, GraduationCap, Pencil, Plus } from "lucide-react";
import { cn, pluralize } from "@/lib/utils";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCampuses } from "@/lib/api/access";
import {
  stageLabel, useAcademicYears, useCopySections, useDeleteSection, useGrades, useRooms, useSaveGrade, useSaveSection, useSections,
  useSubjectGroups, type Grade, type SchoolStage, type Section,
} from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input, NativeSelect } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Badge, Skeleton } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Dialog, DialogBody, DialogContent, DialogFooter, DialogHeader } from "@/components/ui/dialog";
import { Switch } from "@/components/ui/toggles";
import { toast } from "@/components/ui/toaster";
import { FormDialog } from "@/components/forms/form-dialog";

const stages: SchoolStage[] = ["EarlyYears", "Primary", "Middle", "Secondary", "HigherSecondary"];

const gradeSchema = z.object({
  name: z.string().trim().min(1, "Enter a name.").max(60),
  code: z.string().trim().min(1, "Enter a code.").max(20).regex(/^[A-Za-z0-9-]+$/, "Letters, digits or '-' only."),
  stage: z.enum(["EarlyYears", "Primary", "Middle", "Secondary", "HigherSecondary"]),
  nextGradeId: z.string(),
  isActive: z.boolean(),
});
type GradeValues = z.infer<typeof gradeSchema>;

function GradeDialog({ grade, grades, open, onOpenChange }: { grade: Grade | null; grades: Grade[]; open: boolean; onOpenChange: (o: boolean) => void }) {
  const save = useSaveGrade();
  const form = useForm<GradeValues>({ resolver: zodResolver(gradeSchema) });
  const last = grades[grades.length - 1];
  const e = form.formState.errors;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form}
      defaults={{ name: grade?.name ?? "", code: grade?.code ?? "", stage: grade?.stage ?? last?.stage ?? "Primary", nextGradeId: grade?.nextGradeId ?? "", isActive: grade?.isActive ?? true }}
      title={grade ? `Edit ${grade.name}` : "New grade"} description={grade ? undefined : "New grades are added after the highest grade; change the order later if needed."}
      submitLabel={grade ? "Save grade" : "Add grade"} successMessage={grade ? "Grade saved" : "Grade added"}
      onSubmit={(v) => save.mutateAsync({ id: grade?.id, body: { ...v, nextGradeId: v.nextGradeId || null } })}>
      <FieldRow>
        <Field label="Name" htmlFor="g-name" error={e.name?.message}><Input autoFocus placeholder="e.g. Class 6" {...form.register("name")} /></Field>
        <Field label="Code" htmlFor="g-code" error={e.code?.message}><Input className="uppercase" placeholder="C6" {...form.register("code")} /></Field>
      </FieldRow>
      <FieldRow>
        <Field label="Stage" htmlFor="g-stage"><NativeSelect {...form.register("stage")}>{stages.map((s) => <option key={s} value={s}>{stageLabel[s]}</option>)}</NativeSelect></Field>
        <Field label="Promotes to" htmlFor="g-next" hint="Leave empty for the final grade (students graduate).">
          <NativeSelect {...form.register("nextGradeId")}>
            <option value="">— Final grade —</option>
            {grades.filter((g) => g.id !== grade?.id).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
          </NativeSelect>
        </Field>
      </FieldRow>
      <Field inline label="Active" hint="Inactive grades can't receive new sections." htmlFor="g-active">
        <Switch checked={form.watch("isActive")} onCheckedChange={(v) => form.setValue("isActive", v, { shouldDirty: true })} />
      </Field>
    </FormDialog>
  );
}

const sectionSchema = z.object({
  campusId: z.string().min(1, "Choose a campus."),
  gradeId: z.string().min(1, "Choose a grade."),
  name: z.string().trim().min(1, "Enter a section name.").max(30),
  capacity: z.coerce.number<number>().int().min(1, "At least 1 seat.").max(200),
  roomId: z.string(),
  subjectGroupId: z.string(),
  isActive: z.boolean(),
});
type SectionValues = z.infer<typeof sectionSchema>;

function SectionDialog({ yearId, section, preset, grades, open, onOpenChange }: {
  yearId: string; section: Section | null; preset?: { gradeId?: string; campusId?: string }; grades: Grade[]; open: boolean; onOpenChange: (o: boolean) => void;
}) {
  const save = useSaveSection();
  const remove = useDeleteSection();
  const campuses = useCampuses();
  const groups = useSubjectGroups();
  const form = useForm<SectionValues>({ resolver: zodResolver(sectionSchema) });
  const campusId = form.watch("campusId");
  const rooms = useRooms({ campusId: campusId || undefined });
  const e = form.formState.errors;
  const editing = section !== null;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form}
      defaults={{
        campusId: section?.campusId ?? preset?.campusId ?? campuses.data?.find((c) => c.isMain)?.id ?? "",
        gradeId: section?.gradeId ?? preset?.gradeId ?? "", name: section?.name ?? "", capacity: section?.capacity ?? 32,
        roomId: section?.roomId ?? "", subjectGroupId: section?.subjectGroupId ?? "", isActive: section?.isActive ?? true,
      }}
      title={editing ? `Section ${section.displayName}` : "New section"}
      description={editing ? `${section.campusName}` : "A class group of students in one grade, on one campus, for this year."}
      submitLabel={editing ? "Save section" : "Add section"} successMessage={editing ? "Section saved" : "Section added"}
      footerStart={editing && (
        <Button type="button" variant="danger-ghost" loading={remove.isPending} onClick={async () => {
          try { await remove.mutateAsync(section.id); toast.success("Section removed"); onOpenChange(false); }
          catch (err) { toast.error(err, "This section has records and can't be removed. Mark it inactive instead."); }
        }}>Remove</Button>
      )}
      onSubmit={(v) => save.mutateAsync({ id: section?.id, body: {
        academicYearId: yearId, campusId: v.campusId, gradeId: v.gradeId, name: v.name, capacity: v.capacity,
        roomId: v.roomId || null, subjectGroupId: v.subjectGroupId || null, isActive: v.isActive,
      } })}>
      <FieldRow>
        <Field label="Grade" htmlFor="s-grade" error={e.gradeId?.message}>
          <NativeSelect disabled={editing} {...form.register("gradeId")}>
            <option value="">Choose…</option>
            {grades.filter((g) => g.isActive || g.id === section?.gradeId).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
          </NativeSelect>
        </Field>
        <Field label="Campus" htmlFor="s-campus" error={e.campusId?.message}>
          <NativeSelect disabled={editing} {...form.register("campusId", { onChange: () => form.setValue("roomId", "") })}>
            {campuses.data?.filter((c) => c.isActive).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </NativeSelect>
        </Field>
      </FieldRow>
      <FieldRow>
        <Field label="Section" htmlFor="s-name" error={e.name?.message} hint="Usually a letter or a name, e.g. A or Iqbal.">
          <Input autoFocus {...form.register("name")} />
        </Field>
        <Field label="Seats" htmlFor="s-capacity" error={e.capacity?.message}><Input type="number" min={1} {...form.register("capacity")} /></Field>
      </FieldRow>
      <FieldRow>
        <Field label="Home room" htmlFor="s-room" optional>
          <NativeSelect {...form.register("roomId")}>
            <option value="">None</option>
            {rooms.data?.filter((r) => r.isActive).map((r) => <option key={r.id} value={r.id}>{r.name} ({r.code})</option>)}
          </NativeSelect>
        </Field>
        <Field label="Stream" htmlFor="s-group" optional hint="For Matric/Inter groups such as Science.">
          <NativeSelect {...form.register("subjectGroupId")}>
            <option value="">None</option>
            {groups.data?.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
          </NativeSelect>
        </Field>
      </FieldRow>
      <Field inline label="Active" htmlFor="s-active" hint="Inactive sections take no new students.">
        <Switch checked={form.watch("isActive")} onCheckedChange={(v) => form.setValue("isActive", v, { shouldDirty: true })} />
      </Field>
    </FormDialog>
  );
}

function CopyDialog({ open, onOpenChange, toYearId }: { open: boolean; onOpenChange: (o: boolean) => void; toYearId: string }) {
  const years = useAcademicYears();
  const copy = useCopySections();
  const sources = years.data?.filter((y) => y.id !== toYearId && y.sectionCount > 0) ?? [];
  const [from, setFrom] = React.useState("");
  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (open) setFrom(sources[0]?.id ?? "");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent size="sm">
        <DialogHeader title="Copy sections from another year" description="Recreates the same sections, seats and home rooms. Sections that already exist are skipped." />
        <DialogBody>
          <Field label="Copy from" htmlFor="copy-from">
            <NativeSelect value={from} onChange={(e) => setFrom(e.target.value)}>
              {sources.map((y) => <option key={y.id} value={y.id}>{y.name} · {pluralize(y.sectionCount, "section")}</option>)}
            </NativeSelect>
          </Field>
        </DialogBody>
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button variant="primary" disabled={!from} loading={copy.isPending} onClick={async () => {
            try {
              const result = await copy.mutateAsync({ fromAcademicYearId: from, toAcademicYearId: toYearId });
              toast.success(`${pluralize(result.created, "section")} copied`, result.skipped ? `${result.skipped} already existed.` : undefined);
              onOpenChange(false);
            } catch (err) { toast.error(err); }
          }}>Copy sections</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default function GradesPage() {
  const { can } = useSession();
  const editable = can(P.Academics.Manage);
  const years = useAcademicYears();
  const grades = useGrades();
  const campuses = useCampuses();
  const [yearId, setYearId] = React.useState("");
  const selectedYear = years.data?.find((y) => y.id === yearId) ?? years.data?.find((y) => y.isCurrent) ?? years.data?.[0];
  const sections = useSections({ academicYearId: selectedYear?.id ?? "" });
  const [gradeDialog, setGradeDialog] = React.useState<{ open: boolean; grade: Grade | null }>({ open: false, grade: null });
  const [sectionDialog, setSectionDialog] = React.useState<{ open: boolean; section: Section | null; preset?: { gradeId?: string; campusId?: string } }>({ open: false, section: null });
  const [copyOpen, setCopyOpen] = React.useState(false);
  const yearClosed = selectedYear?.status === "Closed";

  const loading = years.isPending || grades.isPending || (selectedYear && sections.isPending);
  const error = years.error ?? grades.error ?? sections.error;

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Grades & sections" }]} title="Grades & sections"
        description="Grades are levels of study in promotion order; sections are this year's class groups on each campus."
        actions={editable && (
          <>
            <Button variant="secondary" onClick={() => setGradeDialog({ open: true, grade: null })}><Plus /> Grade</Button>
            {selectedYear && !yearClosed && <Button variant="primary" onClick={() => setSectionDialog({ open: true, section: null })}><Plus /> Section</Button>}
          </>
        )}
        meta={years.data && years.data.length > 0 && (
          <div className="flex items-center gap-2">
            <span>Sections for</span>
            <NativeSelect aria-label="Academic year" className="h-7 w-auto text-sm" value={selectedYear?.id ?? ""} onChange={(e) => setYearId(e.target.value)}>
              {years.data.map((y) => <option key={y.id} value={y.id}>{y.name}{y.isCurrent ? " (current)" : ""}</option>)}
            </NativeSelect>
            {editable && selectedYear && !yearClosed && (
              <Button variant="ghost" size="sm" onClick={() => setCopyOpen(true)}><Copy /> Copy from another year</Button>
            )}
          </div>
        )}
      />

      {loading ? <Skeleton className="h-96" /> : error ? <ErrorState error={error} onRetry={() => { void grades.refetch(); void sections.refetch(); }} /> :
        grades.data!.length === 0 ? (
          <div className="rounded-lg border border-dashed border-line-strong">
            <EmptyState icon={<GraduationCap />} title="No grades yet" description="Add your levels of study, from the youngest to the oldest."
              action={editable && <Button variant="primary" size="sm" onClick={() => setGradeDialog({ open: true, grade: null })}><Plus /> Add grade</Button>} />
          </div>
        ) : (
          <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
            {grades.data!.map((grade) => {
              const gradeSections = sections.data?.filter((s) => s.gradeId === grade.id) ?? [];
              const seats = gradeSections.reduce((n, s) => n + s.capacity, 0);
              return (
                <li key={grade.id} className={cn("grid gap-3 px-4 py-3.5 lg:grid-cols-[14rem_1fr]", !grade.isActive && "opacity-60")}>
                  <div className="group/grade flex items-start gap-2">
                    <div className="grid flex-1 gap-0.5">
                      <span className="flex items-center gap-2 text-base font-semibold">{grade.name}{!grade.isActive && <Badge>Inactive</Badge>}</span>
                      <span className="flex items-center gap-1 text-sm text-ink-2">
                        {stageLabel[grade.stage]} · <span className="font-mono text-xs">{grade.code}</span>
                        {grade.nextGradeName ? <><ArrowRight className="size-3" />{grade.nextGradeName}</> : <> · final grade</>}
                      </span>
                    </div>
                    {editable && (
                      <Button size="icon-xs" variant="ghost" aria-label={`Edit ${grade.name}`} className="opacity-0 group-hover/grade:opacity-100 [@media(hover:none)]:opacity-100"
                        onClick={() => setGradeDialog({ open: true, grade })}><Pencil /></Button>
                    )}
                  </div>
                  <div className="grid gap-2">
                    {(campuses.data ?? []).map((campus) => {
                      const here = gradeSections.filter((s) => s.campusId === campus.id);
                      if (here.length === 0 && (campuses.data?.length ?? 0) > 1 && !editable) return null;
                      return (
                        <div key={campus.id} className="flex flex-wrap items-center gap-2">
                          {(campuses.data?.length ?? 0) > 1 && <span className="w-36 shrink-0 truncate text-sm text-ink-2">{campus.name}</span>}
                          {here.map((section) => (
                            <button key={section.id} type="button" disabled={!editable}
                              title={`${section.displayName} · ${section.capacity} seats${section.roomName ? ` · home room ${section.roomName}` : ""}`}
                              onClick={() => setSectionDialog({ open: true, section })}
                              className={cn("inline-flex h-8 items-center gap-2 rounded-md border border-line-strong bg-canvas px-2.5 text-sm transition-colors",
                                editable && "hover:border-accent-line hover:bg-accent-soft", !section.isActive && "border-dashed opacity-60")}>
                              <span className="font-semibold text-ink">{section.name}</span>
                              <span className="text-ink-3 tabular">{section.capacity}</span>
                              {section.roomName && <span className="text-ink-3">· {section.roomName.replace("Room ", "")}</span>}
                            </button>
                          ))}
                          {here.length === 0 && <span className="text-sm text-ink-3">No sections</span>}
                          {editable && !yearClosed && grade.isActive && (
                            <Button size="xs" variant="ghost" aria-label={`Add section to ${grade.name} at ${campus.name}`}
                              onClick={() => setSectionDialog({ open: true, section: null, preset: { gradeId: grade.id, campusId: campus.id } })}><Plus /></Button>
                          )}
                        </div>
                      );
                    })}
                    {gradeSections.length > 0 && <span className="text-xs text-ink-3 tabular">{pluralize(gradeSections.length, "section")} · {seats} seats</span>}
                  </div>
                </li>
              );
            })}
          </ul>
        )}

      <GradeDialog open={gradeDialog.open} grade={gradeDialog.grade} grades={grades.data ?? []} onOpenChange={(o) => setGradeDialog((d) => ({ ...d, open: o }))} />
      {selectedYear && (
        <SectionDialog open={sectionDialog.open} yearId={selectedYear.id} section={sectionDialog.section} preset={sectionDialog.preset}
          grades={grades.data ?? []} onOpenChange={(o) => setSectionDialog((d) => ({ ...d, open: o }))} />
      )}
      {selectedYear && <CopyDialog open={copyOpen} onOpenChange={setCopyOpen} toYearId={selectedYear.id} />}
    </Page>
  );
}
