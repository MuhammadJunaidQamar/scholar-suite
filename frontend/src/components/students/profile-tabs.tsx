"use client";

import * as React from "react";
import Link from "next/link";
import { useForm } from "react-hook-form";
import { ArrowRight, Download, Eye, FileText, Image as ImageIcon, MoreHorizontal, Pencil, Phone, Plus, Trash2, Upload, UserMinus } from "lucide-react";
import { cn } from "@/lib/utils";
import { formatDate, formatDateTime } from "@/lib/format";
import { download, openFile } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import {
  ageFrom,
  documentCategoryLabel,
  enrollmentStatusLabel,
  formatNationalId,
  formatPhone,
  studentKeys,
  studentStatusLabel,
  studentStatusTone,
  useDeleteFile,
  useMedical,
  useRemoveGuardianLink,
  useStudentDocuments,
  useUpdateMedical,
  useUploadStudentFile,
  type GuardianLink,
  type MedicalProfile,
  type StudentDetail,
} from "@/lib/api/students";
import { Panel } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Badge, DetailList, Skeleton, StatusBadge } from "@/components/ui/display";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";
import { RecordAvatar } from "@/components/records/record-avatar";
import { AddGuardianDialog, EditGuardianLinkDialog, EmergencyContactsDialog } from "./guardian-dialogs";

const muted = (value: React.ReactNode) => <span className="text-ink-3">{value}</span>;

function guardianRoles(link: GuardianLink) {
  return [
    link.isPrimary && "Primary contact",
    link.isFeePayer && "Pays fees",
    link.isEmergencyContact && "Emergency contact",
    link.canPickUp && "May collect",
  ].filter(Boolean) as string[];
}

// ───────────────────────────── Overview ─────────────────────────────

export function OverviewTab({ student, onShowFamily }: { student: StudentDetail; onShowFamily: () => void }) {
  const current = student.currentEnrollment;
  const address = [student.address.line1, student.address.line2, student.address.city, student.address.province].filter(Boolean).join(", ");
  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <div className="grid content-start gap-6">
        <Panel title="Personal">
          <DetailList columns={2} items={[
            { label: "Date of birth", value: <span className="tabular">{formatDate(student.dateOfBirth)} <span className="text-ink-2">· {ageFrom(student.dateOfBirth)} years</span></span> },
            { label: "Gender", value: student.gender === "Female" ? "Girl" : student.gender === "Male" ? "Boy" : "Other" },
            { label: "B-Form number", value: student.nationalId ? <span className="tabular">{formatNationalId(student.nationalId)}</span> : null },
            { label: "Place of birth", value: student.placeOfBirth },
            { label: "Nationality", value: student.nationality },
            { label: "Religion", value: student.religion },
            { label: "Mother tongue", value: student.motherTongue },
            { label: "Blood group", value: student.bloodGroup },
          ]} />
        </Panel>
        <Panel title="Schooling">
          <DetailList columns={2} items={[
            { label: "Admission number", value: <span className="font-mono text-sm">{student.admissionNumber}</span> },
            { label: "Admitted", value: <span className="tabular">{formatDate(student.admissionDate)}</span> },
            { label: "Class", value: current ? current.className : muted("Not placed") },
            { label: "Roll number", value: current?.rollNumber },
            { label: "Campus", value: student.campusName },
            { label: "Board registration", value: student.registrationNumber },
            { label: "Previous school", value: student.previousSchoolName ? `${student.previousSchoolName}${student.previousSchoolClass ? ` · ${student.previousSchoolClass}` : ""}` : null },
          ]} />
        </Panel>
        <Panel title="Home">
          <DetailList columns={2} items={[
            { label: "Address", value: address || null },
            { label: "Student's mobile", value: student.phone ? formatPhone(student.phone) : null, hidden: !student.phone },
            { label: "Student's email", value: student.email, hidden: !student.email },
          ]} />
        </Panel>
      </div>

      <div className="grid content-start gap-6">
        <Panel title="Family" actions={<Button size="sm" variant="ghost" onClick={onShowFamily}>Details <ArrowRight /></Button>} flush>
          {student.guardians.length === 0 ? (
            <p className="px-4 py-4 text-sm text-ink-2">No parent or guardian recorded.</p>
          ) : (
            <ul className="divide-y divide-line">
              {student.guardians.map((g) => (
                <li key={g.linkId} className="grid gap-0.5 px-4 py-3">
                  <div className="flex items-center justify-between gap-2">
                    <Link href={`/guardians/${g.guardianId}`} className="truncate font-medium text-ink hover:underline">{g.fullName}</Link>
                    <span className="shrink-0 text-xs text-ink-2">{g.relationship}</span>
                  </div>
                  <a href={`tel:${g.phone}`} className="flex w-fit items-center gap-1.5 text-sm text-ink-2 tabular hover:text-ink">
                    <Phone className="size-3.5" aria-hidden /> {formatPhone(g.phone)}
                  </a>
                  {g.isPrimary && <span className="text-xs text-accent">Primary contact</span>}
                </li>
              ))}
            </ul>
          )}
        </Panel>
        {student.siblings.length > 0 && (
          <Panel title="Brothers & sisters" flush>
            <ul className="divide-y divide-line">
              {student.siblings.map((s) => (
                <li key={s.id}>
                  <Link href={`/students/${s.id}`} className="flex items-center gap-2.5 px-4 py-2.5 hover:bg-sunken/60">
                    <RecordAvatar name={s.fullName} fileId={s.photoFileId} size={28} />
                    <span className="grid min-w-0 leading-tight">
                      <span className="truncate text-sm font-medium text-ink">{s.fullName}</span>
                      <span className="truncate text-xs text-ink-2">
                        {s.status === "Active" ? (s.className ?? "Not placed") : studentStatusLabel[s.status]}
                      </span>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </Panel>
        )}
        {student.emergencyContacts.length > 0 && (
          <Panel title="Emergency contacts" flush>
            <ul className="divide-y divide-line">
              {student.emergencyContacts.map((c) => (
                <li key={c.id} className="flex items-center justify-between gap-3 px-4 py-2.5 text-sm">
                  <span className="min-w-0"><span className="font-medium text-ink">{c.name}</span> <span className="text-ink-2">· {c.relationship}</span></span>
                  <a href={`tel:${c.phone}`} className="shrink-0 text-ink-2 tabular hover:text-ink">{formatPhone(c.phone)}</a>
                </li>
              ))}
            </ul>
          </Panel>
        )}
      </div>
    </div>
  );
}

// ───────────────────────────── Family ─────────────────────────────

export function FamilyTab({ student }: { student: StudentDetail }) {
  const { can } = useSession();
  const editable = can(P.Students.Update) && (student.status === "Active" || student.status === "Suspended");
  const remove = useRemoveGuardianLink(student.id);
  const [adding, setAdding] = React.useState(false);
  const [editing, setEditing] = React.useState<GuardianLink | null>(null);
  const [removing, setRemoving] = React.useState<GuardianLink | null>(null);
  const [contactsOpen, setContactsOpen] = React.useState(false);

  return (
    <div className="grid gap-6">
      <Panel title="Parents & guardians" description="Who the child lives with, who pays, and who may collect them."
        actions={editable && <Button size="sm" variant="secondary" onClick={() => setAdding(true)}><Plus /> Add parent or guardian</Button>} flush>
        {student.guardians.length === 0 ? (
          <EmptyState compact title="No parent or guardian recorded" description="Add at least one so the school can reach the family." />
        ) : (
          <ul className="divide-y divide-line">
            {student.guardians.map((g) => (
              <li key={g.linkId} className="flex flex-wrap items-start gap-x-6 gap-y-3 px-4 py-4">
                <div className="flex min-w-60 flex-1 items-start gap-3">
                  <RecordAvatar name={g.fullName} fileId={null} size={36} />
                  <div className="grid min-w-0 gap-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <Link href={`/guardians/${g.guardianId}`} className="font-medium text-ink hover:underline">{g.fullName}</Link>
                      <Badge>{g.relationship}</Badge>
                    </div>
                    <div className="flex flex-wrap gap-x-4 gap-y-0.5 text-sm text-ink-2">
                      <a href={`tel:${g.phone}`} className="tabular hover:text-ink">{formatPhone(g.phone)}</a>
                      {g.email && <a href={`mailto:${g.email}`} className="hover:text-ink">{g.email}</a>}
                      {g.occupation && <span>{g.occupation}</span>}
                    </div>
                    <div className="flex flex-wrap gap-1.5 pt-1">
                      {guardianRoles(g).map((r) => <Badge key={r} tone={r === "Primary contact" ? "accent" : "neutral"}>{r}</Badge>)}
                    </div>
                  </div>
                </div>
                <div className="grid gap-0.5 text-sm">
                  <span className="text-xs text-ink-3">Family portal</span>
                  {!g.portalAccess ? (
                    <span className="text-ink-2">No access to this child</span>
                  ) : g.hasLogin ? (
                    <StatusBadge tone="success">Signed up</StatusBadge>
                  ) : (
                    <StatusBadge tone="neutral">No login yet</StatusBadge>
                  )}
                </div>
                {editable && (
                  <div className="ml-auto flex items-center gap-1">
                    <Button size="sm" variant="ghost" onClick={() => setEditing(g)}><Pencil /> Edit</Button>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button size="icon-sm" variant="ghost" aria-label={`More for ${g.fullName}`}><MoreHorizontal /></Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent>
                        <DropdownMenuItem tone="danger" onSelect={() => setRemoving(g)}><UserMinus /> Remove from this student</DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </Panel>

      <Panel title="Emergency contacts" description="Called, in order, when no parent can be reached."
        actions={editable && <Button size="sm" variant="secondary" onClick={() => setContactsOpen(true)}><Pencil /> Edit</Button>} flush>
        {student.emergencyContacts.length === 0 ? (
          <p className="px-4 py-4 text-sm text-ink-2">None besides the parents.</p>
        ) : (
          <ol className="divide-y divide-line">
            {student.emergencyContacts.map((c, i) => (
              <li key={c.id} className="flex flex-wrap items-center gap-x-6 gap-y-1 px-4 py-3 text-sm">
                <span className="w-5 text-ink-3 tabular">{i + 1}.</span>
                <span className="min-w-48 flex-1"><span className="font-medium text-ink">{c.name}</span> <span className="text-ink-2">· {c.relationship}</span></span>
                <a href={`tel:${c.phone}`} className="text-ink-2 tabular hover:text-ink">{formatPhone(c.phone)}</a>
              </li>
            ))}
          </ol>
        )}
      </Panel>

      <AddGuardianDialog studentId={student.id} familyName={student.lastName} hasPrimary={student.guardians.some((g) => g.isPrimary)}
        open={adding} onOpenChange={setAdding} />
      <EditGuardianLinkDialog studentId={student.id} link={editing} open={editing !== null} onOpenChange={(o) => !o && setEditing(null)} />
      <EmergencyContactsDialog studentId={student.id} contacts={student.emergencyContacts} open={contactsOpen} onOpenChange={setContactsOpen} />
      <ConfirmDialog open={removing !== null} onOpenChange={(o) => !o && setRemoving(null)} tone="danger"
        title={`Remove ${removing?.fullName} from ${student.firstName}'s record?`}
        description="They stay on record as a guardian of any other children, and can be added back later."
        confirmLabel="Remove" loading={remove.isPending}
        onConfirm={async () => {
          if (!removing) return;
          try {
            await remove.mutateAsync(removing.linkId);
            toast.success("Guardian removed");
            setRemoving(null);
          } catch (error) {
            toast.error(error, "The guardian couldn't be removed");
          }
        }} />
    </div>
  );
}

// ───────────────────────────── History ─────────────────────────────

export function HistoryTab({ student }: { student: StudentDetail }) {
  return (
    <div className="grid gap-6">
      <Panel title="Placements" description="Every class the student has been in, by academic year." flush>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[40rem] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs text-ink-2">
                <th className="px-4 py-2 font-medium">Year</th>
                <th className="px-4 py-2 font-medium">Class</th>
                <th className="px-4 py-2 font-medium">Campus</th>
                <th className="px-4 py-2 text-right font-medium">Roll</th>
                <th className="px-4 py-2 font-medium">From – to</th>
                <th className="px-4 py-2 font-medium">Outcome</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {student.enrollments.map((e) => (
                <tr key={e.id}>
                  <td className="px-4 py-2.5 text-ink tabular">{e.academicYearName}</td>
                  <td className="px-4 py-2.5 font-medium text-ink">{e.className}</td>
                  <td className="px-4 py-2.5 text-ink-2">{e.campusName}</td>
                  <td className="px-4 py-2.5 text-right text-ink-2 tabular">{e.rollNumber ?? "—"}</td>
                  <td className="px-4 py-2.5 text-ink-2 tabular">{formatDate(e.startDate)} – {e.endDate ? formatDate(e.endDate) : "now"}</td>
                  <td className="px-4 py-2.5">
                    <StatusBadge tone={e.status === "Active" ? "success" : "neutral"}>{enrollmentStatusLabel[e.status]}</StatusBadge>
                    {e.reason && <p className="pl-3 text-xs text-ink-2">{e.reason}</p>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Panel>
      <Panel title="Status changes" flush>
        {student.statusHistory.length === 0 ? (
          <p className="px-4 py-4 text-sm text-ink-2">Active since admission on {formatDate(student.admissionDate)}.</p>
        ) : (
          <ol className="divide-y divide-line">
            {student.statusHistory.map((c) => (
              <li key={c.id} className="grid gap-1 px-4 py-3 sm:grid-cols-[9rem_1fr]">
                <span className="text-sm text-ink-2 tabular">{formatDate(c.effectiveDate)}</span>
                <div className="grid gap-0.5">
                  <p className="flex flex-wrap items-center gap-2 text-sm text-ink">
                    <StatusBadge tone={studentStatusTone[c.fromStatus]}>{studentStatusLabel[c.fromStatus]}</StatusBadge>
                    <ArrowRight className="size-3.5 text-ink-3" aria-hidden />
                    <StatusBadge tone={studentStatusTone[c.toStatus]}>{studentStatusLabel[c.toStatus]}</StatusBadge>
                  </p>
                  {c.reason && <p className="text-sm text-ink">{c.reason}</p>}
                  {c.destinationSchool && <p className="text-sm text-ink-2">To {c.destinationSchool}</p>}
                  <p className="text-xs text-ink-3">Recorded {formatDateTime(c.recordedAt)}{c.changedByName ? ` by ${c.changedByName}` : ""}</p>
                </div>
              </li>
            ))}
          </ol>
        )}
      </Panel>
    </div>
  );
}

// ───────────────────────────── Medical ─────────────────────────────

const medicalFields: { key: keyof MedicalProfile; label: string; long?: boolean }[] = [
  { key: "allergies", label: "Allergies", long: true },
  { key: "conditions", label: "Conditions", long: true },
  { key: "medications", label: "Medication", long: true },
  { key: "specialNeeds", label: "Special educational needs", long: true },
  { key: "emergencyInstructions", label: "In an emergency", long: true },
  { key: "doctorName", label: "Family doctor" },
  { key: "doctorPhone", label: "Doctor's phone" },
];

export function MedicalTab({ student }: { student: StudentDetail }) {
  const { can } = useSession();
  const medical = useMedical(student.id, true);
  const [editing, setEditing] = React.useState(false);

  if (medical.isPending) return <Skeleton className="h-72" />;
  if (medical.error) return <ErrorState error={medical.error} onRetry={() => void medical.refetch()} />;
  const m = medical.data;
  return (
    <>
      <Panel title="Medical profile" description="Visible only to staff with access to medical records."
        actions={can(P.Students.ManageMedical) && <Button size="sm" variant="secondary" onClick={() => setEditing(true)}><Pencil /> Edit</Button>}>
        <DetailList items={[
          { label: "Medical alert", value: m.medicalAlert ? <span className="font-medium text-danger">{m.medicalAlert}</span> : null },
          { label: "Blood group", value: m.bloodGroup },
          ...medicalFields.map((f) => ({ label: f.label, value: m[f.key] ? <span className="whitespace-pre-line">{m[f.key]}</span> : null })),
        ]} />
      </Panel>
      <MedicalDialog studentId={student.id} profile={m} open={editing} onOpenChange={setEditing} />
    </>
  );
}

function MedicalDialog({ studentId, profile, open, onOpenChange }: { studentId: string; profile: MedicalProfile; open: boolean; onOpenChange: (o: boolean) => void }) {
  const update = useUpdateMedical(studentId);
  const form = useForm<Record<keyof MedicalProfile, string>>();
  const defaults = Object.fromEntries(Object.entries(profile).map(([k, v]) => [k, v ?? ""])) as Record<keyof MedicalProfile, string>;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} defaults={defaults} size="lg"
      title="Edit medical profile" submitLabel="Save" successMessage="Medical profile saved"
      onSubmit={(v) => update.mutateAsync(Object.fromEntries(Object.entries(v).map(([k, val]) => [k, nullIfEmpty(val)])) as MedicalProfile)}>
      <Field label="Medical alert" htmlFor="md-alert" optional hint="The one line every teacher sees on the student's record.">
        <Input {...form.register("medicalAlert")} />
      </Field>
      <FieldRow cols={3}>
        <Field label="Blood group" htmlFor="md-blood" optional>
          <NativeSelect {...form.register("bloodGroup")}>
            <option value="">Not known</option>
            {["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"].map((b) => <option key={b} value={b}>{b}</option>)}
          </NativeSelect>
        </Field>
        <Field label="Family doctor" htmlFor="md-doctor" optional><Input {...form.register("doctorName")} /></Field>
        <Field label="Doctor's phone" htmlFor="md-doctor-phone" optional><Input type="tel" {...form.register("doctorPhone")} /></Field>
      </FieldRow>
      {medicalFields.filter((f) => f.long).map((f) => (
        <Field key={f.key} label={f.label} htmlFor={`md-${f.key}`} optional>
          <Textarea rows={2} {...form.register(f.key)} />
        </Field>
      ))}
    </FormDialog>
  );
}

// ───────────────────────────── Documents ─────────────────────────────

const uploadCategories = ["birth_certificate", "previous_result", "transfer_certificate", "national_id", "medical", "other"];

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

export function DocumentsTab({ student }: { student: StudentDetail }) {
  const { can } = useSession();
  const manage = can(P.Students.ManageDocuments);
  const canMedical = can(P.Students.ManageMedical);
  const documents = useStudentDocuments(student.id, true);
  const uploader = useUploadStudentFile(student.id);
  const remove = useDeleteFile([studentKeys.documents(student.id), studentKeys.detail(student.id)]);
  const [category, setCategory] = React.useState("birth_certificate");
  const [deleting, setDeleting] = React.useState<{ id: string; fileName: string } | null>(null);
  const input = React.useRef<HTMLInputElement>(null);

  const files = (documents.data ?? []).filter((f) => f.category !== "photo");
  return (
    <>
      <Panel title="Documents" description="Private to staff who can see this student. Files open through your session; links can't be shared."
        actions={manage && (
          <div className="flex items-center gap-2">
            <NativeSelect aria-label="Document type" value={category} onChange={(e) => setCategory(e.target.value)} className="h-7 w-56 text-sm">
              {uploadCategories.filter((c) => c !== "medical" || canMedical).map((c) => <option key={c} value={c}>{documentCategoryLabel[c]}</option>)}
            </NativeSelect>
            <input ref={input} type="file" className="sr-only" aria-label="Choose a document" accept=".pdf,.jpg,.jpeg,.png,.webp,.docx"
              onChange={async (e) => {
                const file = e.target.files?.[0];
                e.target.value = "";
                if (!file) return;
                try {
                  await uploader.mutateAsync({ file, category });
                  toast.success(`${documentCategoryLabel[category]} uploaded`);
                } catch (error) {
                  toast.error(error, "The file couldn't be uploaded");
                }
              }} />
            <Button size="sm" variant="secondary" loading={uploader.isPending} onClick={() => input.current?.click()}><Upload /> Upload</Button>
          </div>
        )} flush>
        {documents.isPending ? (
          <div className="p-4"><Skeleton className="h-24" /></div>
        ) : documents.error ? (
          <ErrorState error={documents.error} onRetry={() => void documents.refetch()} />
        ) : files.length === 0 ? (
          <EmptyState compact icon={<FileText />} title="No documents yet"
            description="Keep the B-Form, previous result card and leaving certificate here. PDF, JPG, PNG or Word, up to 10 MB." />
        ) : (
          <ul className="divide-y divide-line">
            {files.map((f) => (
              <li key={f.id} className="flex flex-wrap items-center gap-x-4 gap-y-1 px-4 py-3">
                <span className={cn("grid size-8 place-items-center rounded-md border border-line bg-sunken text-ink-2 [&_svg]:size-4")}>
                  {f.contentType.startsWith("image/") ? <ImageIcon /> : <FileText />}
                </span>
                <div className="grid min-w-0 flex-1 leading-tight">
                  <span className="truncate text-sm font-medium text-ink">{documentCategoryLabel[f.category] ?? f.category}</span>
                  <span className="truncate text-xs text-ink-2">
                    {f.fileName} · {formatSize(f.sizeBytes)} · {formatDate(f.uploadedAt)}{f.uploadedByName ? ` by ${f.uploadedByName}` : ""}
                  </span>
                </div>
                {f.sensitivity === "Restricted" && <Badge tone="warning">Restricted</Badge>}
                <div className="flex items-center gap-1">
                  <Button size="sm" variant="ghost" onClick={() => void openFile(`/files/${f.id}/content`).catch((e) => toast.error(e, "The file couldn't be opened"))}>
                    <Eye /> View
                  </Button>
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button size="icon-sm" variant="ghost" aria-label={`More for ${f.fileName}`}><MoreHorizontal /></Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent>
                      <DropdownMenuItem onSelect={() => void download(`/files/${f.id}/content`, { download: true }).catch((e) => toast.error(e))}>
                        <Download /> Download
                      </DropdownMenuItem>
                      {manage && (
                        <>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem tone="danger" onSelect={() => setDeleting(f)}><Trash2 /> Delete</DropdownMenuItem>
                        </>
                      )}
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>
              </li>
            ))}
          </ul>
        )}
      </Panel>
      <ConfirmDialog open={deleting !== null} onOpenChange={(o) => !o && setDeleting(null)} tone="danger"
        title="Delete this document?" description={`${deleting?.fileName} will be removed from the record. This is logged in the audit trail.`}
        confirmLabel="Delete" loading={remove.isPending}
        onConfirm={async () => {
          if (!deleting) return;
          try {
            await remove.mutateAsync(deleting.id);
            toast.success("Document deleted");
            setDeleting(null);
          } catch (error) {
            toast.error(error, "The document couldn't be deleted");
          }
        }} />
    </>
  );
}
