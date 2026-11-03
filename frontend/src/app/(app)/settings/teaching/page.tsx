"use client";

import * as React from "react";
import { Pencil, UserRoundX } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCampuses } from "@/lib/api/access";
import { useSaveSectionTeachers, useStaffOptions, useTeaching, type SectionTeaching, type StaffOption } from "@/lib/api/staff";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Badge, Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { Field } from "@/components/ui/field";
import { NativeSelect } from "@/components/ui/input";
import { Segmented } from "@/components/ui/toggles";
import { Dialog, DialogBody, DialogContent, DialogFooter, DialogHeader } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toaster";

/** Class teachers (who take the daily register) and subject teachers (period registers, marks) for every section. */
export default function TeachingAssignmentsPage() {
  const { can } = useSession();
  const editable = can(P.Academics.Manage);
  const campuses = useCampuses();
  const active = (campuses.data ?? []).filter((c) => c.isActive);
  const [campusId, setCampusId] = React.useState("");
  const [filter, setFilter] = React.useState<"all" | "gaps">("all");
  const teaching = useTeaching({ campusId: campusId || undefined });
  const [editing, setEditing] = React.useState<SectionTeaching | null>(null);
  const rows = (teaching.data ?? []).filter((s) => filter === "all" || !s.classTeacherId || s.subjects.some((x) => x.offered && !x.staffId));
  const gaps = (teaching.data ?? []).filter((s) => !s.classTeacherId).length;

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Teaching assignments" }]} title="Teaching assignments"
        description="Who is class teacher of each section (they take its daily register) and who teaches each subject (period registers, and later marks)." />
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {active.length > 1 && (
          <NativeSelect aria-label="Campus" value={campusId} onChange={(e) => setCampusId(e.target.value)} className="w-auto">
            <option value="">All campuses</option>
            {active.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </NativeSelect>
        )}
        <Segmented size="sm" aria-label="Show" value={filter} onChange={setFilter}
          options={[{ value: "all", label: "All sections" }, { value: "gaps", label: "Missing a teacher" }]} />
        {gaps > 0 && <span className="text-sm text-danger">{gaps} section{gaps === 1 ? " has" : "s have"} no class teacher</span>}
      </div>
      {teaching.isPending ? <Skeleton className="h-96" /> : teaching.error ? <ErrorState error={teaching.error} onRetry={() => void teaching.refetch()} /> : (
        <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
          {rows.map((s) => {
            const missing = s.subjects.filter((x) => x.offered && !x.staffId).length;
            return (
              <li key={s.sectionId} className="grid items-start gap-x-6 gap-y-2 px-4 py-3 md:grid-cols-[12rem_14rem_minmax(0,1fr)_auto]">
                <div className="grid leading-tight">
                  <span className="font-medium text-ink">{s.className}</span>
                  {active.length > 1 && <span className="text-xs text-ink-3">{s.campusName}</span>}
                </div>
                <div className="grid leading-tight">
                  <span className="text-xs text-ink-3">Class teacher</span>
                  {s.classTeacherName ? <span className="text-sm text-ink">{s.classTeacherName}</span>
                    : <span className="inline-flex items-center gap-1 text-sm text-danger"><UserRoundX className="size-3.5" /> Not assigned</span>}
                </div>
                <div className="flex min-w-0 flex-wrap gap-1.5">
                  {s.subjects.filter((x) => x.staffId).map((x) => (
                    <Badge key={x.subjectId} className="font-normal">{x.subjectCode} · {x.staffName}</Badge>
                  ))}
                  {missing > 0 && <span className="text-xs text-ink-3">{missing} subject{missing === 1 ? "" : "s"} taught by the class teacher</span>}
                </div>
                {editable && <Button size="sm" variant="ghost" onClick={() => setEditing(s)}><Pencil /> Edit</Button>}
              </li>
            );
          })}
        </ul>
      )}
      {editing && <EditSectionTeachers section={editing} onClose={() => setEditing(null)} />}
    </Page>
  );
}

function StaffSelect({ id, value, onChange, options, placeholder }: { id: string; value: string; onChange: (v: string) => void; options: StaffOption[]; placeholder: string }) {
  const teaching = options.filter((o) => o.staffType === "Teaching");
  const others = options.filter((o) => o.staffType !== "Teaching");
  return (
    <NativeSelect id={id} value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="">{placeholder}</option>
      <optgroup label="Teachers">
        {teaching.map((o) => <option key={o.id} value={o.id}>{o.fullName}{o.classTeacherCount ? ` · class teacher of ${o.classTeacherCount}` : ""}</option>)}
      </optgroup>
      {others.length > 0 && <optgroup label="Other staff">{others.map((o) => <option key={o.id} value={o.id}>{o.fullName}{o.designationName ? ` · ${o.designationName}` : ""}</option>)}</optgroup>}
    </NativeSelect>
  );
}

function EditSectionTeachers({ section, onClose }: { section: SectionTeaching; onClose: () => void }) {
  const staff = useStaffOptions(section.campusId);
  const save = useSaveSectionTeachers();
  const [classTeacher, setClassTeacher] = React.useState(section.classTeacherId ?? "");
  const [subjects, setSubjects] = React.useState<Record<string, string>>(() => Object.fromEntries(section.subjects.map((x) => [x.subjectId, x.staffId ?? ""])));
  const options = staff.data ?? [];

  const submit = async () => {
    try {
      await save.mutateAsync({
        sectionId: section.sectionId,
        body: { classTeacherId: classTeacher || null, subjects: section.subjects.map((x) => ({ subjectId: x.subjectId, staffId: subjects[x.subjectId] || null })) },
      });
      toast.success(`${section.className} updated`);
      onClose();
    } catch (error) {
      toast.error(error, "The assignments couldn't be saved");
    }
  };

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent size="md">
        <DialogHeader title={section.className} description={`${section.campusName} · subjects without a teacher are taught by the class teacher.`} />
        <DialogBody>
          <Field label="Class teacher" htmlFor="ta-class" hint="Takes the daily register and sees the class's leave and absences.">
            <StaffSelect id="ta-class" value={classTeacher} onChange={setClassTeacher} options={options} placeholder="No class teacher" />
          </Field>
          <div className="grid gap-3 border-t border-line pt-4">
            {section.subjects.map((x) => (
              <Field key={x.subjectId} label={<span>{x.subjectName}{!x.offered && <span className="text-ink-3"> · not in this year&apos;s curriculum</span>}</span>} htmlFor={`ta-${x.subjectId}`} inline>
                <StaffSelect id={`ta-${x.subjectId}`} value={subjects[x.subjectId] ?? ""} onChange={(v) => setSubjects((s) => ({ ...s, [x.subjectId]: v }))}
                  options={options} placeholder="Class teacher" />
              </Field>
            ))}
          </div>
        </DialogBody>
        <DialogFooter>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button variant="primary" loading={save.isPending} onClick={() => void submit()}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
