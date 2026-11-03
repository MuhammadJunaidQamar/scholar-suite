"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, usePathname, useRouter, useSearchParams } from "next/navigation";
import { ArrowRightLeft, Camera, HeartPulse, MoreHorizontal, Pencil, Trash2, UserRoundCog } from "lucide-react";
import { ApiError } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { formatDate } from "@/lib/format";
import {
  ageFrom,
  studentStatusLabel,
  studentStatusTone,
  useDeleteStudent,
  useStudent,
  useUploadStudentFile,
} from "@/lib/api/students";
import { Breadcrumbs, Page } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Skeleton, StatusBadge, Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { RecordAvatar } from "@/components/records/record-avatar";
import { ChangeStatusDialog, MoveSectionDialog } from "@/components/students/student-dialogs";
import { EditStudentDialog } from "@/components/students/edit-student-dialog";
import { DocumentsTab, FamilyTab, HistoryTab, MedicalTab, OverviewTab } from "@/components/students/profile-tabs";

const tabs = ["overview", "family", "history", "medical", "documents"] as const;
type Tab = (typeof tabs)[number];

export default function StudentProfilePage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const { can } = useSession();
  const student = useStudent(id);
  const photo = useUploadStudentFile(id);
  const remove = useDeleteStudent();
  const [editing, setEditing] = React.useState(false);
  const [moving, setMoving] = React.useState(false);
  const [statusOpen, setStatusOpen] = React.useState(false);
  const [deleting, setDeleting] = React.useState(false);
  const photoInput = React.useRef<HTMLInputElement>(null);

  const canMedical = can(P.Students.ViewMedical);
  const canDocuments = can(P.Students.ViewDocuments);
  const visibleTabs = tabs.filter((t) => (t !== "medical" || canMedical) && (t !== "documents" || canDocuments));
  const requested = searchParams.get("tab") as Tab | null;
  const tab: Tab = requested && visibleTabs.includes(requested) ? requested : "overview";
  const setTab = (next: string) => {
    const params = new URLSearchParams(searchParams.toString());
    if (next === "overview") params.delete("tab");
    else params.set("tab", next);
    router.replace(params.size ? `${pathname}?${params}` : pathname, { scroll: false });
  };

  if (student.isPending) {
    return (
      <Page>
        <Skeleton className="mt-7 h-4 w-40" />
        <div className="mt-6 flex items-center gap-4"><Skeleton className="size-20 rounded-lg" /><div className="grid gap-2"><Skeleton className="h-7 w-64" /><Skeleton className="h-4 w-80" /></div></div>
        <Skeleton className="mt-8 h-96" />
      </Page>
    );
  }
  if (student.error) {
    const missing = student.error instanceof ApiError && student.error.status === 404;
    return (
      <Page>
        {missing ? (
          <EmptyState title="Student not found" description="The record may have been deleted, or it belongs to a campus you don't have access to."
            action={<Button asChild><Link href="/students">Back to students</Link></Button>} />
        ) : (
          <ErrorState error={student.error} onRetry={() => void student.refetch()} />
        )}
      </Page>
    );
  }

  const s = student.data;
  const current = s.status === "Active" || s.status === "Suspended";
  const canUpdate = can(P.Students.Update);
  const meta = [
    s.currentEnrollment?.className,
    s.currentEnrollment?.rollNumber && `Roll ${s.currentEnrollment.rollNumber}`,
    s.campusName,
    `${ageFrom(s.dateOfBirth)} years`,
  ].filter(Boolean);

  return (
    <Page>
      <Breadcrumbs className="pt-6" items={[{ label: "Students", href: "/students" }, { label: s.fullName }]} />

      <header className="flex flex-wrap items-start gap-x-5 gap-y-4 pt-4 pb-5">
        <div className="relative">
          <RecordAvatar name={s.fullName} fileId={s.photoFileId} size={76} square className="rounded-lg" />
          {canUpdate && (
            <>
              <input ref={photoInput} type="file" accept="image/jpeg,image/png,image/webp" className="sr-only" aria-label="Choose a photo"
                onChange={async (e) => {
                  const file = e.target.files?.[0];
                  e.target.value = "";
                  if (!file) return;
                  try {
                    await photo.mutateAsync({ file, category: "photo" });
                    toast.success("Photo updated");
                  } catch (error) {
                    toast.error(error, "The photo couldn't be uploaded");
                  }
                }} />
              <Button size="icon-xs" variant="secondary" loading={photo.isPending} aria-label={s.photoFileId ? "Change photo" : "Add photo"}
                className="absolute -right-1.5 -bottom-1.5 rounded-full" onClick={() => photoInput.current?.click()}>
                <Camera />
              </Button>
            </>
          )}
        </div>
        <div className="grid min-w-0 flex-1 gap-1.5">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <h1 className="font-display text-2xl text-ink">{s.fullName}</h1>
            <StatusBadge tone={studentStatusTone[s.status]} variant="soft">
              {studentStatusLabel[s.status]}
              {!current && s.statusChangedOn ? ` · ${formatDate(s.statusChangedOn)}` : ""}
            </StatusBadge>
          </div>
          <p className="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm text-ink-2">
            <span className="font-mono text-ink">{s.admissionNumber}</span>
            {meta.map((m) => <React.Fragment key={String(m)}><span aria-hidden className="text-ink-3">·</span><span>{m}</span></React.Fragment>)}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {canUpdate && <Button variant="secondary" onClick={() => setEditing(true)}><Pencil /> Edit</Button>}
          {canUpdate && current && <Button variant="secondary" onClick={() => setMoving(true)}><ArrowRightLeft /> Move section</Button>}
          {(can(P.Students.ManageStatus) || can(P.Students.Delete)) && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="secondary" size="icon" aria-label="More actions"><MoreHorizontal /></Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent>
                {can(P.Students.ManageStatus) && s.status !== "Alumni" && (
                  <DropdownMenuItem onSelect={() => setStatusOpen(true)}><UserRoundCog /> Change status…</DropdownMenuItem>
                )}
                {can(P.Students.Delete) && (
                  <>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem tone="danger" onSelect={() => setDeleting(true)}><Trash2 /> Delete record…</DropdownMenuItem>
                  </>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>
      </header>

      {s.medicalAlert && (
        <div role="note" className="mb-5 flex items-start gap-2.5 rounded-md border border-danger/30 bg-danger-soft px-3.5 py-2.5 text-sm">
          <HeartPulse className="mt-0.5 size-4 shrink-0 text-danger" aria-hidden />
          <p><span className="font-semibold text-danger">Medical alert</span> <span className="text-ink">{s.medicalAlert}</span></p>
        </div>
      )}

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList className="mb-6">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="family">Family <span className="text-ink-3 tabular">{s.guardians.length}</span></TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
          {canMedical && <TabsTrigger value="medical">Medical</TabsTrigger>}
          {canDocuments && <TabsTrigger value="documents">Documents <span className="text-ink-3 tabular">{s.documentCount}</span></TabsTrigger>}
        </TabsList>
        <TabsContent value="overview"><OverviewTab student={s} onShowFamily={() => setTab("family")} /></TabsContent>
        <TabsContent value="family"><FamilyTab student={s} /></TabsContent>
        <TabsContent value="history"><HistoryTab student={s} /></TabsContent>
        {canMedical && <TabsContent value="medical"><MedicalTab student={s} /></TabsContent>}
        {canDocuments && <TabsContent value="documents"><DocumentsTab student={s} /></TabsContent>}
      </Tabs>

      <EditStudentDialog student={s} open={editing} onOpenChange={setEditing} />
      <MoveSectionDialog students={[{ id: s.id, fullName: s.fullName, sectionId: s.currentEnrollment?.sectionId ?? null }]} open={moving} onOpenChange={setMoving} />
      <ChangeStatusDialog student={s} open={statusOpen} onOpenChange={setStatusOpen} />
      <ConfirmDialog open={deleting} onOpenChange={setDeleting} tone="danger"
        title={`Delete ${s.fullName}'s record?`}
        description="Only for records created by mistake. A student who has left should be withdrawn instead, which keeps their history. Records with attendance, fees or results can't be deleted."
        confirmLabel="Delete record" loading={remove.isPending}
        onConfirm={async () => {
          try {
            await remove.mutateAsync(s.id);
            toast.success("Record deleted");
            router.replace("/students");
          } catch (error) {
            toast.error(error, "The record couldn't be deleted");
          }
        }} />
    </Page>
  );
}
