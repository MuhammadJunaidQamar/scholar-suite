"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { LibraryBig, Plus, Trash2 } from "lucide-react";
import { cn, pluralize } from "@/lib/utils";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import {
  subjectTypeLabel, useAcademicYears, useCurriculum, useGrades, useSaveSubject, useSaveSubjectGroup, useSetCurriculum, useSubjectGroups,
  useSubjects, type Subject, type SubjectGroup, type SubjectType,
} from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Badge, Skeleton, Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Checkbox, Switch } from "@/components/ui/toggles";
import { toast } from "@/components/ui/toaster";
import { RecordList, RecordRow } from "@/components/ui/record-list";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";

const types: SubjectType[] = ["Core", "Language", "Elective", "CoCurricular"];
const palette = ["#3B6EA8", "#2F7D5B", "#5B4FA3", "#9A5B2E", "#3E7A6E", "#A3536B", "#7A6A3A", "#455A80", "#6B7280"];

const subjectSchema = z.object({
  code: z.string().trim().min(1, "Enter a code.").max(20).regex(/^[A-Za-z0-9-]+$/, "Letters, digits or '-' only."),
  name: z.string().trim().min(1, "Enter a name.").max(100),
  shortName: z.string().max(30),
  type: z.enum(["Core", "Elective", "Language", "CoCurricular"]),
  subjectGroupId: z.string(),
  color: z.string(),
  isActive: z.boolean(),
});
type SubjectValues = z.infer<typeof subjectSchema>;

function SubjectDialog({ subject, open, onOpenChange }: { subject: Subject | null; open: boolean; onOpenChange: (o: boolean) => void }) {
  const save = useSaveSubject();
  const groups = useSubjectGroups();
  const form = useForm<SubjectValues>({ resolver: zodResolver(subjectSchema) });
  const color = form.watch("color");
  const e = form.formState.errors;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form}
      defaults={{ code: subject?.code ?? "", name: subject?.name ?? "", shortName: subject?.shortName ?? "", type: subject?.type ?? "Core",
        subjectGroupId: subject?.subjectGroupId ?? "", color: subject?.color ?? palette[0]!, isActive: subject?.isActive ?? true }}
      title={subject ? `Edit ${subject.name}` : "New subject"} submitLabel={subject ? "Save subject" : "Add subject"}
      successMessage={subject ? "Subject saved" : "Subject added"}
      onSubmit={(v) => save.mutateAsync({ id: subject?.id, body: { ...v, shortName: nullIfEmpty(v.shortName), subjectGroupId: v.subjectGroupId || null, color: v.color || null } })}>
      <FieldRow>
        <Field label="Name" htmlFor="sub-name" error={e.name?.message}><Input autoFocus {...form.register("name")} /></Field>
        <Field label="Code" htmlFor="sub-code" error={e.code?.message}><Input className="uppercase" {...form.register("code")} /></Field>
      </FieldRow>
      <FieldRow>
        <Field label="Short name" htmlFor="sub-short" optional hint="Used in timetables."><Input {...form.register("shortName")} /></Field>
        <Field label="Type" htmlFor="sub-type"><NativeSelect {...form.register("type")}>{types.map((t) => <option key={t} value={t}>{subjectTypeLabel[t]}</option>)}</NativeSelect></Field>
      </FieldRow>
      <Field label="Stream" htmlFor="sub-group" optional hint="Only for subjects specific to a group, such as Biology in Science.">
        <NativeSelect {...form.register("subjectGroupId")}>
          <option value="">All students</option>
          {groups.data?.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
        </NativeSelect>
      </Field>
      <Field label="Colour" hint="Tints the subject in timetables.">
        <div className="flex flex-wrap gap-1.5">
          {palette.map((c) => (
            <button key={c} type="button" aria-label={c} aria-pressed={color === c} onClick={() => form.setValue("color", c, { shouldDirty: true })}
              className={cn("size-7 rounded-md shadow-[inset_0_0_0_1px_oklch(0_0_0/0.1)]", color === c && "ring-2 ring-ink ring-offset-2 ring-offset-raised")}
              style={{ background: c }} />
          ))}
        </div>
      </Field>
      <Field inline label="Active" htmlFor="sub-active"><Switch checked={form.watch("isActive")} onCheckedChange={(v) => form.setValue("isActive", v, { shouldDirty: true })} /></Field>
    </FormDialog>
  );
}

const groupSchema = z.object({ name: z.string().trim().min(1, "Enter a name.").max(100), description: z.string().max(500) });

function GroupDialog({ group, open, onOpenChange }: { group: SubjectGroup | null; open: boolean; onOpenChange: (o: boolean) => void }) {
  const save = useSaveSubjectGroup();
  const form = useForm<z.infer<typeof groupSchema>>({ resolver: zodResolver(groupSchema) });
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="sm"
      defaults={{ name: group?.name ?? "", description: group?.description ?? "" }}
      title={group ? `Edit ${group.name}` : "New stream"} description="A group of elective subjects, e.g. Pre-Medical or Computer Science."
      submitLabel="Save stream" successMessage="Stream saved"
      onSubmit={(v) => save.mutateAsync({ id: group?.id, body: { name: v.name, description: nullIfEmpty(v.description) } })}>
      <Field label="Name" htmlFor="grp-name" error={form.formState.errors.name?.message}><Input autoFocus {...form.register("name")} /></Field>
      <Field label="Description" htmlFor="grp-desc" optional><Textarea rows={2} {...form.register("description")} /></Field>
    </FormDialog>
  );
}

type Row = { subjectId: string; isMandatory: boolean; periodsPerWeek: number; subjectGroupId: string | null };

function CurriculumEditor({ editable }: { editable: boolean }) {
  const years = useAcademicYears();
  const grades = useGrades();
  const subjects = useSubjects();
  const setCurriculum = useSetCurriculum();
  const [yearId, setYearId] = React.useState("");
  const [gradeId, setGradeId] = React.useState("");
  const year = years.data?.find((y) => y.id === yearId) ?? years.data?.find((y) => y.isCurrent);
  const grade = grades.data?.find((g) => g.id === gradeId) ?? grades.data?.[0];
  const curriculum = useCurriculum({ academicYearId: year?.id, gradeId: grade?.id });
  const [rows, setRows] = React.useState<Row[]>([]);
  const [adding, setAdding] = React.useState("");

  React.useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    if (curriculum.data) setRows(curriculum.data.map((c) => ({ subjectId: c.subjectId, isMandatory: c.isMandatory, periodsPerWeek: c.periodsPerWeek, subjectGroupId: c.subjectGroupId })));
  }, [curriculum.data]);

  if (years.isPending || grades.isPending || subjects.isPending) return <Skeleton className="h-80" />;
  if (!year || !grade) return <EmptyState compact title="Set up academic years and grades first" />;

  const subject = (id: string) => subjects.data?.find((s) => s.id === id);
  const original = curriculum.data ?? [];
  const dirty = rows.length !== original.length || rows.some((r) => {
    const o = original.find((x) => x.subjectId === r.subjectId);
    return !o || o.periodsPerWeek !== r.periodsPerWeek || o.isMandatory !== r.isMandatory;
  });
  const total = rows.reduce((n, r) => n + (Number(r.periodsPerWeek) || 0), 0);
  const available = subjects.data?.filter((s) => s.isActive && !rows.some((r) => r.subjectId === s.id)) ?? [];
  const readOnly = !editable || year.status === "Closed";

  const save = async () => {
    try {
      await setCurriculum.mutateAsync({ academicYearId: year.id, gradeId: grade.id, items: rows.map((r) => ({ ...r, periodsPerWeek: Number(r.periodsPerWeek) })) });
      toast.success(`${grade.name} curriculum saved`);
    } catch (error) {
      toast.error(error, "The curriculum couldn't be saved");
    }
  };

  return (
    <div className="grid gap-6 lg:grid-cols-[13rem_1fr]">
      <div className="grid content-start gap-3">
        <NativeSelect aria-label="Academic year" value={year.id} onChange={(e) => setYearId(e.target.value)}>
          {years.data!.map((y) => <option key={y.id} value={y.id}>{y.name}{y.isCurrent ? " (current)" : ""}</option>)}
        </NativeSelect>
        <nav aria-label="Grades" className="grid gap-px">
          {grades.data!.filter((g) => g.isActive).map((g) => (
            <button key={g.id} type="button" onClick={() => setGradeId(g.id)} aria-current={g.id === grade.id ? "true" : undefined}
              className={cn("rounded-md px-2.5 py-1.5 text-left text-sm text-ink-2 hover:bg-sunken hover:text-ink", g.id === grade.id && "bg-sunken font-medium text-ink")}>
              {g.name}
            </button>
          ))}
        </nav>
      </div>
      <div className="rounded-lg border border-line bg-surface">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-4 py-3">
          <div className="grid gap-0.5">
            <h3 className="text-md font-semibold">{grade.name} · {year.name}</h3>
            <p className={cn("text-sm tabular", total > 48 ? "text-danger" : "text-ink-2")}>{pluralize(rows.length, "subject")} · {total} periods a week</p>
          </div>
          {!readOnly && (
            <div className="flex gap-2">
              <Button variant="secondary" size="sm" disabled={!dirty} onClick={() => curriculum.data && setRows(curriculum.data.map((c) => ({ subjectId: c.subjectId, isMandatory: c.isMandatory, periodsPerWeek: c.periodsPerWeek, subjectGroupId: c.subjectGroupId })))}>Discard</Button>
              <Button variant="primary" size="sm" disabled={!dirty} loading={setCurriculum.isPending} onClick={save}>Save curriculum</Button>
            </div>
          )}
        </div>
        {curriculum.isPending ? <Skeleton className="m-4 h-40" /> : (
          <table className="w-full text-left">
            <thead>
              <tr className="border-b border-line bg-sunken/50 text-xs text-ink-2">
                <th className="px-4 py-2 font-medium">Subject</th>
                <th className="w-32 px-3 py-2 font-medium">Periods / week</th>
                <th className="w-28 px-3 py-2 font-medium">Compulsory</th>
                <th className="w-12 px-3 py-2" />
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 && (
                <tr><td colSpan={4}><EmptyState compact icon={<LibraryBig />} title="No subjects for this grade" description="Add the subjects this grade studies." /></td></tr>
              )}
              {rows.map((row, index) => {
                const s = subject(row.subjectId);
                return (
                  <tr key={row.subjectId} className="border-b border-line last:border-b-0">
                    <td className="px-4 py-2">
                      <span className="flex items-center gap-2.5">
                        <span className="size-2.5 rounded-full" style={{ background: s?.color ?? "var(--neutral)" }} aria-hidden />
                        <span className="font-medium">{s?.name}</span>
                        <span className="font-mono text-xs text-ink-3">{s?.code}</span>
                        {s?.subjectGroupName && <Badge>{s.subjectGroupName}</Badge>}
                      </span>
                    </td>
                    <td className="px-3 py-2">
                      <Input type="number" min={0} max={40} inputSize="sm" className="w-20" aria-label={`Periods per week for ${s?.name}`} disabled={readOnly}
                        value={row.periodsPerWeek} onChange={(e) => setRows(rows.map((r, i) => (i === index ? { ...r, periodsPerWeek: Number(e.target.value) } : r)))} />
                    </td>
                    <td className="px-3 py-2">
                      <Checkbox aria-label={`${s?.name} is compulsory`} disabled={readOnly} checked={row.isMandatory}
                        onCheckedChange={(v) => setRows(rows.map((r, i) => (i === index ? { ...r, isMandatory: v === true } : r)))} />
                    </td>
                    <td className="px-3 py-2 text-right">
                      {!readOnly && (
                        <Button size="icon-xs" variant="ghost" aria-label={`Remove ${s?.name}`} onClick={() => setRows(rows.filter((_, i) => i !== index))}><Trash2 /></Button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
        {!readOnly && available.length > 0 && (
          <div className="flex items-center gap-2 border-t border-line px-4 py-3">
            <NativeSelect aria-label="Subject to add" className="max-w-64" value={adding} onChange={(e) => setAdding(e.target.value)}>
              <option value="">Add a subject…</option>
              {available.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </NativeSelect>
            <Button size="sm" variant="secondary" disabled={!adding} onClick={() => {
              const s = subject(adding);
              setRows([...rows, { subjectId: adding, isMandatory: s?.type !== "Elective", periodsPerWeek: 4, subjectGroupId: s?.subjectGroupId ?? null }]);
              setAdding("");
            }}><Plus /> Add</Button>
          </div>
        )}
      </div>
    </div>
  );
}

export default function SubjectsPage() {
  const { can } = useSession();
  const editable = can(P.Academics.Manage);
  const subjects = useSubjects();
  const groups = useSubjectGroups();
  const [tab, setTab] = React.useState("subjects");
  const [subjectDialog, setSubjectDialog] = React.useState<{ open: boolean; subject: Subject | null }>({ open: false, subject: null });
  const [groupDialog, setGroupDialog] = React.useState<{ open: boolean; group: SubjectGroup | null }>({ open: false, group: null });

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Subjects & curriculum" }]} title="Subjects & curriculum"
        description="The subjects the school teaches, and which of them each grade studies this year."
        actions={editable && tab === "subjects" ? <Button variant="primary" onClick={() => setSubjectDialog({ open: true, subject: null })}><Plus /> New subject</Button>
          : editable && tab === "streams" ? <Button variant="primary" onClick={() => setGroupDialog({ open: true, group: null })}><Plus /> New stream</Button> : undefined} />
      <Tabs value={tab} onValueChange={setTab}>
        <TabsList className="mb-5">
          <TabsTrigger value="subjects">Subjects</TabsTrigger>
          <TabsTrigger value="curriculum">Curriculum by grade</TabsTrigger>
          <TabsTrigger value="streams">Streams</TabsTrigger>
        </TabsList>
        <TabsContent value="subjects">
          {subjects.isPending ? <Skeleton className="h-80" /> : subjects.error ? <ErrorState error={subjects.error} onRetry={() => void subjects.refetch()} /> : (
            <RecordList>
              {subjects.data.map((s) => (
                <RecordRow key={s.id} muted={!s.isActive} onClick={editable ? () => setSubjectDialog({ open: true, subject: s }) : undefined}
                  leading={<span className="size-3 shrink-0 rounded-full" style={{ background: s.color ?? "var(--neutral)" }} aria-hidden />}
                  title={s.name} badges={<>{s.subjectGroupName && <Badge>{s.subjectGroupName}</Badge>}{!s.isActive && <Badge>Inactive</Badge>}</>}
                  subtitle={<span><span className="font-mono text-xs">{s.code}</span> · {subjectTypeLabel[s.type]}{s.shortName ? ` · “${s.shortName}”` : ""}</span>}
                  facts={[s.gradeCount ? `Taught in ${pluralize(s.gradeCount, "grade")}` : "Not in a curriculum"]} />
              ))}
            </RecordList>
          )}
        </TabsContent>
        <TabsContent value="curriculum"><CurriculumEditor editable={editable} /></TabsContent>
        <TabsContent value="streams">
          {groups.isPending ? <Skeleton className="h-40" /> : groups.data?.length === 0 ? (
            <EmptyState compact title="No streams" description="Add streams if senior students choose between subject groups." />
          ) : (
            <RecordList>
              {groups.data?.map((g) => (
                <RecordRow key={g.id} onClick={editable ? () => setGroupDialog({ open: true, group: g }) : undefined}
                  title={g.name} subtitle={g.description} facts={[pluralize(g.subjectCount, "subject")]} />
              ))}
            </RecordList>
          )}
        </TabsContent>
      </Tabs>
      <SubjectDialog open={subjectDialog.open} subject={subjectDialog.subject} onOpenChange={(o) => setSubjectDialog((d) => ({ ...d, open: o }))} />
      <GroupDialog open={groupDialog.open} group={groupDialog.group} onOpenChange={(o) => setGroupDialog((d) => ({ ...d, open: o }))} />
    </Page>
  );
}
