"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { Ellipsis, KeyRound, Pencil, Trash2, UserRoundCog } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { formatNationalId, formatPhone } from "@/lib/api/students";
import {
  employmentTypeLabel,
  staffStatusLabel,
  staffStatusTone,
  staffTypeLabel,
  useDeleteStaff,
  useStaffMember,
  type StaffDetail,
} from "@/lib/api/staff";
import {
  leaveStatusLabel,
  leaveStatusTone,
  staffAttendanceCode,
  staffAttendanceLabel,
  useLeaveBalances,
  useStaffLeave,
  useStaffMonth,
  formatDays,
} from "@/lib/api/attendance";
import { formatDate, formatDateTime, formatPercent, formatRelative, todayIso } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { DetailList, Skeleton, StatusBadge, Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { ConfirmDialog } from "@/components/ui/dialog";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/menu";
import { SectionHeading } from "@/components/ui/record-list";
import { toast } from "@/components/ui/toaster";
import { RecordAvatar } from "@/components/records/record-avatar";
import { StaffFormDialog } from "@/components/staff/staff-form-dialog";
import { ChangeStaffStatusDialog, GrantAccessDialog } from "@/components/staff/staff-dialogs";

export default function StaffProfilePage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const search = useSearchParams();
  const { can } = useSession();
  const staff = useStaffMember(id);
  const [dialog, setDialog] = React.useState<"edit" | "status" | "access" | "delete" | null>(null);
  const remove = useDeleteStaff();
  const tab = search.get("tab") ?? "overview";

  if (staff.isPending) return <Page><div className="grid gap-4 pt-8"><Skeleton className="h-16 w-80" /><Skeleton className="h-80" /></div></Page>;
  if (staff.error) return <Page><ErrorState error={staff.error} onRetry={() => void staff.refetch()} className="pt-16" /></Page>;
  const s = staff.data;
  const canEdit = can(P.Staff.Update);

  return (
    <Page>
      <PageHeader
        breadcrumbs={[{ label: "Staff", href: "/staff" }, { label: s.fullName }]}
        title={
          <span className="flex items-center gap-3">
            <RecordAvatar name={s.fullName} fileId={s.photoFileId} size={44} />
            <span>{s.fullName}</span>
          </span>
        }
        meta={
          <>
            <StatusBadge tone={staffStatusTone[s.status]}>{staffStatusLabel[s.status]}</StatusBadge>
            <span>{s.designationName ?? staffTypeLabel[s.staffType]}{s.departmentName && ` · ${s.departmentName}`}</span>
            <span>{s.campusName}</span>
            <span className="font-mono">{s.employeeNumber}</span>
          </>
        }
        actions={canEdit && (
          <>
            <Button variant="secondary" onClick={() => setDialog("edit")}><Pencil /> Edit</Button>
            <DropdownMenu>
              <DropdownMenuTrigger asChild><Button variant="secondary" size="icon" aria-label="More actions"><Ellipsis /></Button></DropdownMenuTrigger>
              <DropdownMenuContent>
                <DropdownMenuItem onSelect={() => setDialog("status")}><UserRoundCog /> Change status…</DropdownMenuItem>
                {!s.account && s.status !== "Resigned" && s.status !== "Terminated" && s.status !== "Retired" && can(P.Users.Manage) && (
                  <DropdownMenuItem onSelect={() => setDialog("access")}><KeyRound /> Give sign-in access…</DropdownMenuItem>
                )}
                {can(P.Staff.Delete) && (
                  <>
                    <DropdownMenuSeparator />
                    <DropdownMenuItem tone="danger" onSelect={() => setDialog("delete")}><Trash2 /> Delete record…</DropdownMenuItem>
                  </>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </>
        )}
      />
      <Tabs value={tab} onValueChange={(v) => router.replace(`/staff/${id}?tab=${v}`)}>
        <TabsList className="mb-6">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          {(can(P.Staff.ViewAttendance) || can(P.Hr.ViewLeave)) && <TabsTrigger value="attendance">Attendance &amp; leave</TabsTrigger>}
        </TabsList>
        <TabsContent value="overview"><Overview staff={s} onGrant={() => setDialog("access")} canGrant={canEdit && can(P.Users.Manage)} /></TabsContent>
        <TabsContent value="attendance"><AttendanceTab staff={s} /></TabsContent>
      </Tabs>

      <StaffFormDialog staff={s} open={dialog === "edit"} onOpenChange={(o) => !o && setDialog(null)} />
      <ChangeStaffStatusDialog staff={s} open={dialog === "status"} onOpenChange={(o) => !o && setDialog(null)} />
      <GrantAccessDialog staff={s} open={dialog === "access"} onOpenChange={(o) => !o && setDialog(null)} />
      <ConfirmDialog open={dialog === "delete"} onOpenChange={(o) => !o && setDialog(null)} tone="danger" confirmLabel="Delete record"
        title={`Delete ${s.fullName}?`} description="Only for records created by mistake. Someone with classes or attendance on record is marked as left instead."
        loading={remove.isPending}
        onConfirm={async () => {
          try {
            await remove.mutateAsync(s.id);
            toast.success("Staff record deleted");
            router.push("/staff");
          } catch (error) {
            toast.error(error, "The record couldn't be deleted");
            setDialog(null);
          }
        }} />
    </Page>
  );
}

function Overview({ staff: s, onGrant, canGrant }: { staff: StaffDetail; onGrant: () => void; canGrant: boolean }) {
  const address = [s.address.line1, s.address.city, s.address.province].filter(Boolean).join(", ");
  const bySection = s.teaches.reduce<Record<string, { className: string; subjects: string[] }>>((acc, t) => {
    (acc[t.sectionId] ??= { className: t.className, subjects: [] }).subjects.push(t.subjectName ?? "");
    return acc;
  }, {});
  return (
    <div className="grid gap-10 lg:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)]">
      <section className="grid content-start gap-2">
        <SectionHeading title="Details" />
        <DetailList items={[
          { label: "Employee no.", value: <span className="font-mono">{s.employeeNumber}</span> },
          { label: "Staff type", value: staffTypeLabel[s.staffType] },
          { label: "Employment", value: employmentTypeLabel[s.employmentType] },
          { label: "Joined", value: formatDate(s.joinDate) },
          { label: "Left", value: s.exitDate ? `${formatDate(s.exitDate)}${s.exitReason ? ` — ${s.exitReason}` : ""}` : null, hidden: !s.exitDate },
          { label: "Mobile", value: s.phone ? <a href={`tel:${s.phone}`} className="tabular hover:underline">{formatPhone(s.phone)}</a> : null },
          { label: "Email", value: s.email ? <a href={`mailto:${s.email}`} className="hover:underline">{s.email}</a> : null },
          { label: "CNIC", value: s.nationalId ? <span className="tabular">{formatNationalId(s.nationalId)}</span> : null },
          { label: "Date of birth", value: s.dateOfBirth ? formatDate(s.dateOfBirth) : null },
          { label: "Address", value: address || null },
        ]} />
      </section>
      <div className="grid content-start gap-8">
        <section className="grid content-start gap-2">
          <SectionHeading title="Classes" description="This academic year." />
          {s.classTeacherOf.length === 0 && s.teaches.length === 0 ? (
            <p className="rounded-lg border border-line bg-surface px-4 py-5 text-sm text-ink-2">
              Not assigned to any class. Class and subject teachers are set in <Link href="/settings/teaching" className="text-accent hover:underline">Settings → Teaching assignments</Link>.
            </p>
          ) : (
            <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
              {s.classTeacherOf.map((c) => (
                <li key={c.sectionId} className="flex items-center justify-between gap-3 px-4 py-2.5">
                  <span className="grid leading-tight">
                    <Link href={`/attendance/register/${c.sectionId}`} className="font-medium text-ink hover:underline">{c.className}</Link>
                    <span className="text-xs text-ink-2">Class teacher · takes the daily register</span>
                  </span>
                  <StatusBadge tone="accent" variant="soft">Class teacher</StatusBadge>
                </li>
              ))}
              {Object.entries(bySection).map(([sectionId, x]) => (
                <li key={sectionId} className="grid gap-0.5 px-4 py-2.5 leading-tight">
                  <span className="font-medium text-ink">{x.className}</span>
                  <span className="text-xs text-ink-2">{x.subjects.join(", ")}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
        <section className="grid content-start gap-2">
          <SectionHeading title="Sign-in" />
          {s.account ? (
            <DetailList items={[
              { label: "Signs in as", value: <span className="font-mono text-sm">{s.account.userName}</span> },
              { label: "Roles", value: s.account.roles.join(", ") || "—" },
              { label: "Access", value: s.account.accessSuspended ? <StatusBadge tone="warning">Suspended</StatusBadge> : s.account.status === "Invited" ? <StatusBadge tone="info">Not signed in yet</StatusBadge> : <StatusBadge tone="success">Active</StatusBadge> },
              { label: "Last sign-in", value: s.account.lastLoginAt ? <span title={formatDateTime(s.account.lastLoginAt)}>{formatRelative(s.account.lastLoginAt)}</span> : "Never" },
            ]} />
          ) : (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-line bg-surface px-4 py-3">
              <p className="text-sm text-ink-2">No sign-in access. {s.staffType === "Teaching" ? "Teachers need it to take registers and request leave." : ""}</p>
              {canGrant && <Button size="sm" onClick={onGrant}><KeyRound /> Give access</Button>}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

function AttendanceTab({ staff: s }: { staff: StaffDetail }) {
  const { can } = useSession();
  const today = todayIso();
  const [year, month] = today.split("-").map(Number) as [number, number];
  const sheet = useStaffMonth({ campusId: can(P.Staff.ViewAttendance) ? s.campusId : undefined, year, month });
  const hr = can(P.Hr.ViewLeave) || can(P.Hr.ApproveLeave);
  const balances = useLeaveBalances(hr ? s.id : null);
  const leave = useStaffLeave({ staffId: s.id, pageSize: 10 }, hr);
  const row = sheet.data?.staff.find((x) => x.staffId === s.id);

  return (
    <div className="grid gap-10 lg:grid-cols-2">
      {can(P.Staff.ViewAttendance) && (
        <section className="grid content-start gap-3">
          <SectionHeading title={`This month · ${new Intl.DateTimeFormat("en-GB", { month: "long", year: "numeric" }).format(new Date())}`}
            actions={<Link href={`/attendance/staff?view=month&campus=${s.campusId}`} className="text-sm font-medium text-accent hover:underline">Staff month</Link>} />
          {sheet.isPending ? <Skeleton className="h-48" /> : !row ? <p className="text-sm text-ink-2">No attendance recorded this month.</p> : (
            <>
              <p className="text-sm text-ink-2">
                <span className="font-medium text-ink tabular">{formatPercent(row.totals.percentage)}</span> attended · {row.totals.absent} absent · {row.totals.late} late · {row.totals.onLeave} on leave
              </p>
              <div className="grid grid-cols-7 gap-1">
                {["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"].map((d) => <span key={d} className="text-center text-2xs text-ink-3">{d}</span>)}
                {Array.from({ length: (new Date(`${sheet.data!.days[0]!.date}T12:00:00Z`).getUTCDay() + 6) % 7 }, (_, i) => <span key={`pad${i}`} />)}
                {sheet.data!.days.map((d, i) => {
                  const mark = row.marks[i];
                  return (
                    <span key={d.date} title={`${formatDate(d.date, { weekday: true })}${mark ? `: ${staffAttendanceLabel[mark]}` : d.closedReason ? `: ${d.closedReason}` : ""}`}
                      className={cn("grid h-10 place-items-center rounded-md border text-xs tabular",
                        !d.isSchoolDay ? "border-transparent bg-sunken text-ink-3" : "border-line bg-surface text-ink-2",
                        mark === "Absent" && "border-transparent bg-danger-soft font-semibold text-danger",
                        mark === "OnLeave" && "border-transparent bg-info-soft font-medium text-info",
                        (mark === "Late" || mark === "HalfDay") && "border-transparent bg-warning-soft font-medium text-ink",
                        d.date === today && "ring-2 ring-[var(--focus)]")}>
                      <span className="leading-none">{Number(d.date.slice(8))}</span>
                      {mark && mark !== "Present" && <span className="text-[9px] leading-none">{staffAttendanceCode[mark]}</span>}
                    </span>
                  );
                })}
              </div>
            </>
          )}
        </section>
      )}
      {(can(P.Hr.ViewLeave) || can(P.Hr.ApproveLeave)) && (
        <section className="grid content-start gap-3">
          <SectionHeading title="Leave this year" actions={<Link href="/attendance/staff-leave" className="text-sm font-medium text-accent hover:underline">All staff leave</Link>} />
          {balances.data && (
            <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
              {balances.data.map((b) => (
                <li key={b.leaveTypeId} className="flex items-center justify-between gap-3 px-4 py-2 text-sm">
                  <span className="text-ink">{b.name}</span>
                  <span className="text-ink-2 tabular">
                    {formatDays(b.taken)} taken{b.pending > 0 && ` · ${formatDays(b.pending)} pending`}
                    {b.remaining !== null && <span className="text-ink"> · {formatDays(b.remaining)} left</span>}
                  </span>
                </li>
              ))}
            </ul>
          )}
          {leave.data && leave.data.items.length > 0 && (
            <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
              {leave.data.items.map((l) => (
                <li key={l.id} className="flex items-center justify-between gap-3 px-4 py-2 text-sm">
                  <span className="grid min-w-0 leading-tight">
                    <span className="text-ink">{l.leaveTypeName} · {formatDays(l.days)}</span>
                    <span className="truncate text-xs text-ink-2">{formatDate(l.fromDate)}{l.toDate !== l.fromDate && ` – ${formatDate(l.toDate)}`} · {l.reason}</span>
                  </span>
                  <StatusBadge tone={leaveStatusTone[l.status]}>{leaveStatusLabel[l.status]}</StatusBadge>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}
    </div>
  );
}
