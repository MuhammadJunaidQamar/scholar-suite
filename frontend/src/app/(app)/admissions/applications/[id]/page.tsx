"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ArrowRight, CalendarClock, Eye, FileText, Globe, Image as ImageIcon, MoreHorizontal, Pencil, Upload } from "lucide-react";
import { ApiError, openFile } from "@/lib/api/client";
import { formatDate, formatDateTime, todayIso } from "@/lib/format";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useAcademicYears } from "@/lib/api/org";
import { ageFrom, documentCategoryLabel, formatNationalId, formatPhone } from "@/lib/api/students";
import {
  applicationStatusLabel,
  applicationStatusTone,
  assessmentOutcomeLabel,
  assessmentTypeLabel,
  useApplication,
  useApplicationAction,
  useApplicationDocuments,
  useUploadApplicationFile,
  type ApplicationDetail,
  type Assessment,
} from "@/lib/api/admissions";
import { Breadcrumbs, Page, Panel } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Badge, DetailList, Skeleton, StatusBadge } from "@/components/ui/display";
import { NativeSelect } from "@/components/ui/input";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import {
  DecisionDialog,
  EditApplicationDialog,
  EnrollDialog,
  RecordResultDialog,
  ScheduleAssessmentDialog,
  WithdrawDialog,
  type Decision,
} from "@/components/admissions/application-dialogs";

type DialogState =
  | { kind: "decide"; decision: Decision }
  | { kind: "schedule" }
  | { kind: "result"; assessment: Assessment }
  | { kind: "withdraw" }
  | { kind: "enroll" }
  | { kind: "edit" }
  | null;

const closed = ["Rejected", "Withdrawn", "Enrolled"];

/** What to do next, in words, with the buttons for it. */
function NextStep({ a, open }: { a: ApplicationDetail; open: (d: Exclude<DialogState, null>) => void }) {
  const { can } = useSession();
  const act = useApplicationAction(a.id);
  const allowed = (s: string) => a.allowedTransitions.includes(s as never);
  const review = can(P.Admissions.Review);
  const decide = can(P.Admissions.Decide);
  const pending = a.assessments.find((x) => x.outcome === "Pending");
  const latest = [...a.assessments].reverse().find((x) => x.outcome !== "Pending");

  const run = async (action: "review" | "accept") => {
    try {
      await act.mutateAsync({ action });
      toast.success(action === "review" ? "Review started" : "Marked as accepted");
    } catch (error) {
      toast.error(error);
    }
  };

  let text: React.ReactNode;
  const buttons: React.ReactNode[] = [];
  const needsDecision = ["UnderReview", "Assessed", "Waitlisted", "Expired"].includes(a.status);
  const decisionButtons = () => {
    if (!decide) return;
    if (allowed("Offered")) buttons.push(<Button key="offer" variant="primary" onClick={() => open({ kind: "decide", decision: "offer" })}>Offer a place</Button>);
    if (allowed("Waitlisted")) buttons.push(<Button key="wait" onClick={() => open({ kind: "decide", decision: "waitlist" })}>Waiting list</Button>);
    if (allowed("Rejected")) buttons.push(<Button key="reject" variant="ghost" onClick={() => open({ kind: "decide", decision: "reject" })}>Not offer</Button>);
  };

  switch (a.status) {
    case "Draft":
      text = "Saved but not yet submitted.";
      break;
    case "Submitted":
      text = "A new application. Look it over, then book an assessment or decide.";
      if (review) buttons.push(<Button key="review" variant="primary" loading={act.isPending} onClick={() => void run("review")}>Start review</Button>);
      if (review) buttons.push(<Button key="book" onClick={() => open({ kind: "schedule" })}>Book assessment</Button>);
      break;
    case "UnderReview":
      text = "Under review. Book an entrance test or interview, or decide now.";
      if (review) buttons.push(<Button key="book" onClick={() => open({ kind: "schedule" })}>Book assessment</Button>);
      decisionButtons();
      break;
    case "AssessmentScheduled":
      text = pending
        ? <>{assessmentTypeLabel[pending.type]} on <strong className="font-medium">{formatDateTime(pending.scheduledAt)}</strong>{pending.location ? ` · ${pending.location}` : ""}. Record the result once it&apos;s done.</>
        : "An assessment is booked.";
      if (review && pending) buttons.push(<Button key="result" variant="primary" onClick={() => open({ kind: "result", assessment: pending })}>Record result</Button>);
      break;
    case "Assessed":
      text = latest
        ? <>{assessmentTypeLabel[latest.type]}: <strong className="font-medium">{assessmentOutcomeLabel[latest.outcome]}</strong>{latest.score !== null ? ` — ${latest.score}${latest.maxScore ? `/${latest.maxScore}` : ""}` : ""}. Decide next.</>
        : "Assessed. Decide next.";
      decisionButtons();
      if (review) buttons.push(<Button key="book" variant={decide ? "ghost" : "secondary"} onClick={() => open({ kind: "schedule" })}>Book another</Button>);
      break;
    case "Waitlisted":
      text = <>Number <strong className="font-medium">{a.waitlistRank ?? "—"}</strong> on the {a.gradeName} waiting list. Offer a place when a seat opens.</>;
      decisionButtons();
      break;
    case "Offered":
      text = <>A place was offered{a.offerExpiresOn ? <>; the family should reply by <strong className="font-medium">{formatDate(a.offerExpiresOn)}</strong></> : ""}.</>;
      if (can(P.Admissions.Update)) buttons.push(<Button key="accept" loading={act.isPending} onClick={() => void run("accept")}>Family accepted</Button>);
      if (can(P.Admissions.Enroll)) buttons.push(<Button key="enroll" variant="primary" onClick={() => open({ kind: "enroll" })}>Enrol</Button>);
      break;
    case "Accepted":
      text = "The family accepted the place. Enrol to create the student record.";
      if (can(P.Admissions.Enroll)) buttons.push(<Button key="enroll" variant="primary" onClick={() => open({ kind: "enroll" })}>Enrol</Button>);
      break;
    case "Expired":
      text = "The offer lapsed without a reply.";
      decisionButtons();
      break;
    case "Enrolled":
      text = "Enrolled. The student record holds everything from here on.";
      if (a.studentId) buttons.push(<Button key="student" variant="primary" asChild><Link href={`/students/${a.studentId}`}>Open student record <ArrowRight /></Link></Button>);
      break;
    case "Rejected":
      text = <>Not offered{a.decisionNote ? <>: <span className="text-ink">{a.decisionNote}</span></> : ""}.</>;
      break;
    case "Withdrawn":
      text = "Withdrawn by the family.";
      break;
  }

  // When this person can't take the next step, say who does, rather than showing nothing.
  const waitingOn = needsDecision && !decide
    ? "The decision is made by staff who can decide admissions, usually the principal."
    : (a.status === "Offered" || a.status === "Accepted") && !can(P.Admissions.Enroll)
      ? "The admissions office records the family's reply and enrols the student."
      : (a.status === "Submitted" || a.status === "AssessmentScheduled") && !review
        ? "The admissions team reviews applications and records assessment results."
        : null;

  return (
    <Panel title="Next step">
      <div className="grid gap-3">
        <p className="text-sm text-ink-2">{text}</p>
        {buttons.length === 0 && waitingOn && <p className="text-sm text-ink-3">{waitingOn}</p>}
        {buttons.length > 0 && <div className="flex flex-wrap gap-2">{buttons}</div>}
      </div>
    </Panel>
  );
}

const uploadCategories = ["birth_certificate", "previous_result", "national_id", "photo", "other"];

function Documents({ a }: { a: ApplicationDetail }) {
  const { can } = useSession();
  const documents = useApplicationDocuments(a.id);
  const uploader = useUploadApplicationFile(a.id);
  const [category, setCategory] = React.useState("birth_certificate");
  const input = React.useRef<HTMLInputElement>(null);
  const editable = can(P.Admissions.Update) && !closed.includes(a.status);
  return (
    <Panel title="Documents" description={a.status === "Enrolled" ? "Moved to the student's record on enrolment." : "Move to the student's record on enrolment."}
      actions={editable && (
        <div className="flex items-center gap-2">
          <NativeSelect aria-label="Document type" value={category} onChange={(e) => setCategory(e.target.value)} className="h-7 w-52 text-sm">
            {uploadCategories.map((c) => <option key={c} value={c}>{documentCategoryLabel[c]}</option>)}
          </NativeSelect>
          <input ref={input} type="file" className="sr-only" aria-label="Choose a document" accept=".pdf,.jpg,.jpeg,.png,.webp"
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
        <div className="p-4"><Skeleton className="h-16" /></div>
      ) : documents.error ? (
        <ErrorState error={documents.error} onRetry={() => void documents.refetch()} />
      ) : (documents.data ?? []).length === 0 ? (
        <p className="px-4 py-4 text-sm text-ink-2">{a.status === "Enrolled" ? "See the student's Documents tab." : "None yet. B-Form and the last result card are the usual two."}</p>
      ) : (
        <ul className="divide-y divide-line">
          {documents.data!.map((f) => (
            <li key={f.id} className="flex items-center gap-3 px-4 py-2.5">
              <span className="grid size-8 place-items-center rounded-md border border-line bg-sunken text-ink-2 [&_svg]:size-4">
                {f.contentType.startsWith("image/") ? <ImageIcon /> : <FileText />}
              </span>
              <span className="grid min-w-0 flex-1 leading-tight">
                <span className="truncate text-sm font-medium text-ink">{documentCategoryLabel[f.category] ?? f.category}</span>
                <span className="truncate text-xs text-ink-2">{f.fileName} · {formatDate(f.uploadedAt)}</span>
              </span>
              <Button size="sm" variant="ghost" onClick={() => void openFile(`/files/${f.id}/content`).catch((e) => toast.error(e, "The file couldn't be opened"))}>
                <Eye /> View
              </Button>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}

export default function ApplicationPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { can } = useSession();
  const application = useApplication(id);
  const years = useAcademicYears();
  const [dialog, setDialog] = React.useState<DialogState>(null);
  const close = (o: boolean) => !o && setDialog(null);

  if (application.isPending) {
    return <Page><Skeleton className="mt-7 h-4 w-48" /><Skeleton className="mt-6 h-14 w-[28rem]" /><Skeleton className="mt-8 h-96" /></Page>;
  }
  if (application.error) {
    const missing = application.error instanceof ApiError && application.error.status === 404;
    return (
      <Page>
        {missing ? (
          <EmptyState title="Application not found" description="It may be for a campus you don't have access to."
            action={<Button asChild><Link href="/admissions">Back to admissions</Link></Button>} />
        ) : (
          <ErrorState error={application.error} onRetry={() => void application.refetch()} />
        )}
      </Page>
    );
  }

  const a = application.data;
  const isOpen = !closed.includes(a.status);
  const year = years.data?.find((y) => y.id === a.academicYearId);
  const enrollDate = year && year.startDate > todayIso() ? year.startDate : todayIso();
  const address = [a.address.line1, a.address.line2, a.address.city, a.address.province].filter(Boolean).join(", ");

  return (
    <Page>
      <Breadcrumbs className="pt-6" items={[{ label: "Admissions", href: "/admissions" }, { label: a.applicationNumber }]} />
      <header className="flex flex-wrap items-start gap-x-5 gap-y-4 pt-4 pb-6">
        <div className="grid min-w-0 flex-1 gap-1.5">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <h1 className="font-display text-2xl text-ink">{a.applicantName}</h1>
            <StatusBadge tone={applicationStatusTone[a.status]} variant="soft">{applicationStatusLabel[a.status]}</StatusBadge>
          </div>
          <p className="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm text-ink-2">
            <span className="font-mono text-ink">{a.applicationNumber}</span>
            <span aria-hidden className="text-ink-3">·</span><span>{a.gradeName}, {a.academicYearName}</span>
            <span aria-hidden className="text-ink-3">·</span><span>{a.campusName}</span>
            <span aria-hidden className="text-ink-3">·</span>
            <span className="inline-flex items-center gap-1">{a.source === "Online" && <Globe className="size-3.5 text-ink-3" aria-hidden />}{a.source === "Online" ? "Applied online" : "Entered at the office"}</span>
            {a.submittedAt && <><span aria-hidden className="text-ink-3">·</span><span>Received {formatDate(a.submittedAt)}</span></>}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {isOpen && can(P.Admissions.Update) && <Button variant="secondary" onClick={() => setDialog({ kind: "edit" })}><Pencil /> Edit</Button>}
          {isOpen && can(P.Admissions.Update) && a.allowedTransitions.includes("Withdrawn") && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="secondary" size="icon" aria-label="More actions"><MoreHorizontal /></Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent>
                <DropdownMenuItem tone="danger" onSelect={() => setDialog({ kind: "withdraw" })}>Withdraw application…</DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>
      </header>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_23rem]">
        <div className="grid content-start gap-6">
          <Panel title="Child">
            <DetailList columns={2} items={[
              { label: "Date of birth", value: <span className="tabular">{formatDate(a.dateOfBirth)} <span className="text-ink-2">· {ageFrom(a.dateOfBirth)} years</span></span> },
              { label: "Gender", value: a.gender === "Female" ? "Girl" : a.gender === "Male" ? "Boy" : "Other" },
              { label: "B-Form number", value: a.nationalId ? <span className="tabular">{formatNationalId(a.nationalId)}</span> : null },
              { label: "Previous school", value: a.previousSchoolName ? `${a.previousSchoolName}${a.previousSchoolClass ? ` · ${a.previousSchoolClass}` : ""}` : null },
              { label: "Last result", value: a.previousResult },
              { label: "Address", value: address || null },
            ]} />
          </Panel>
          <Panel title="Parent">
            <DetailList columns={2} items={[
              { label: "Name", value: <>{a.guardianFirstName} {a.guardianLastName} <span className="text-ink-2">· {a.guardianRelationship}</span></> },
              { label: "Mobile", value: <a href={`tel:${a.guardianPhone}`} className="tabular hover:underline">{formatPhone(a.guardianPhone)}</a> },
              { label: "Email", value: a.guardianEmail },
              { label: "CNIC", value: a.guardianNationalId ? <span className="tabular">{formatNationalId(a.guardianNationalId)}</span> : null },
              { label: "Occupation", value: a.guardianOccupation },
            ]} />
            {a.notes && <p className="mt-3 rounded-md bg-sunken/70 px-3 py-2 text-sm whitespace-pre-line text-ink">{a.notes}</p>}
          </Panel>
          <Panel title="Assessments" flush
            actions={isOpen && can(P.Admissions.Review) && a.status !== "Offered" && a.status !== "Accepted" && (
              <Button size="sm" variant="secondary" onClick={() => setDialog({ kind: "schedule" })}><CalendarClock /> Book</Button>
            )}>
            {a.assessments.length === 0 ? (
              <p className="px-4 py-4 text-sm text-ink-2">None booked.</p>
            ) : (
              <ul className="divide-y divide-line">
                {a.assessments.map((x) => (
                  <li key={x.id} className="flex flex-wrap items-center gap-x-6 gap-y-1 px-4 py-3">
                    <span className="grid min-w-48 flex-1 leading-tight">
                      <span className="text-sm font-medium text-ink">{assessmentTypeLabel[x.type]}</span>
                      <span className="text-xs text-ink-2">{formatDateTime(x.scheduledAt)}{x.location ? ` · ${x.location}` : ""}</span>
                    </span>
                    {x.outcome === "Pending" ? (
                      can(P.Admissions.Review) && isOpen
                        ? <Button size="sm" variant="secondary" onClick={() => setDialog({ kind: "result", assessment: x })}>Record result</Button>
                        : <span className="text-sm text-ink-2">Result pending</span>
                    ) : (
                      <span className="grid text-right leading-tight">
                        <StatusBadge tone={x.outcome === "Passed" ? "success" : "neutral"}>{assessmentOutcomeLabel[x.outcome]}</StatusBadge>
                        {x.score !== null && <span className="text-xs text-ink-2 tabular">{x.score}{x.maxScore ? ` / ${x.maxScore}` : ""}</span>}
                      </span>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </Panel>
          <Documents a={a} />
        </div>

        <div className="grid content-start gap-6">
          <NextStep a={a} open={setDialog} />
          <Panel title="History" flush>
            <ol className="divide-y divide-line">
              {a.history.map((h, i) => (
                <li key={i} className="grid gap-0.5 px-4 py-2.5">
                  <span className="flex flex-wrap items-center gap-1.5 text-sm text-ink">
                    <StatusBadge tone={applicationStatusTone[h.toStatus]}>{applicationStatusLabel[h.toStatus]}</StatusBadge>
                  </span>
                  {h.note && <span className="pl-3 text-sm text-ink-2">{h.note}</span>}
                  <span className="pl-3 text-xs text-ink-3">{formatDateTime(h.changedAt)}{h.changedByName ? ` · ${h.changedByName}` : a.source === "Online" && h.toStatus === "Submitted" ? " · online form" : ""}</span>
                </li>
              ))}
            </ol>
          </Panel>
          {a.inquiryId && <Badge className="w-fit">Started as an inquiry</Badge>}
        </div>
      </div>

      {dialog?.kind === "decide" && <DecisionDialog application={a} open onOpenChange={close} initial={dialog.decision} />}
      <ScheduleAssessmentDialog application={a} open={dialog?.kind === "schedule"} onOpenChange={close} />
      <RecordResultDialog application={a} assessment={dialog?.kind === "result" ? dialog.assessment : null} open={dialog?.kind === "result"} onOpenChange={close} />
      <WithdrawDialog application={a} open={dialog?.kind === "withdraw"} onOpenChange={close} />
      <EditApplicationDialog application={a} open={dialog?.kind === "edit"} onOpenChange={close} />
      <EnrollDialog application={a} open={dialog?.kind === "enroll"} onOpenChange={close} defaultDate={enrollDate}
        onEnrolled={(studentId) => {
          toast.success(`${a.firstName} is enrolled`, "Their student record is ready.");
          router.push(`/students/${studentId}`);
        }} />
    </Page>
  );
}
