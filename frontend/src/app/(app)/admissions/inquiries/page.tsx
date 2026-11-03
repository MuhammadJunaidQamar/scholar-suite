"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import type { ColumnDef, VisibilityState } from "@tanstack/react-table";
import { ArrowRight, Check, MessageSquarePlus, Pencil, PhoneCall, RotateCcw, X } from "lucide-react";
import { cn } from "@/lib/utils";
import { formatDate, todayIso } from "@/lib/format";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCampuses } from "@/lib/api/access";
import { useGrades } from "@/lib/api/org";
import { formatPhone } from "@/lib/api/students";
import {
  inquirySourceLabel,
  inquiryStatusLabel,
  useInquiries,
  useInquiryAction,
  useSaveInquiry,
  type Inquiry,
  type InquirySource,
  type InquiryStatus,
} from "@/lib/api/admissions";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/display";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { EmptyState } from "@/components/ui/states";
import { DropdownMenuItem, DropdownMenuSeparator } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";
import { DataTable, type Density } from "@/components/data-table/data-table";
import { ClearFiltersButton, FacetFilter, TableSearch, TableToolbar, ViewMenu, useTablePreference } from "@/components/data-table/toolbar";
import { useListParams } from "@/components/data-table/use-list-params";
import { AdmissionsNav } from "@/components/admissions/admissions-nav";
import { mobileField } from "@/components/students/student-form";

const statusTone: Record<InquiryStatus, "info" | "neutral" | "success" | "accent"> = {
  New: "info", Contacted: "neutral", ApplicationIssued: "accent", Converted: "success", Closed: "neutral",
};

function buildColumns(today: string): ColumnDef<Inquiry, unknown>[] {
  return [
    {
      id: "student",
      header: "Child",
      meta: { sortKey: "student", pinned: true },
      cell: ({ row }) => (
        <div className="grid min-w-0 py-1 leading-tight">
          <span className="truncate font-medium text-ink">{row.original.studentName}</span>
          <span className="truncate text-xs text-ink-2">{row.original.gradeName ?? "Class not decided"}{row.original.campusName ? ` · ${row.original.campusName}` : ""}</span>
        </div>
      ),
    },
    {
      id: "parent",
      header: "Parent",
      meta: { label: "Parent" },
      cell: ({ row }) => (
        <div className="grid leading-tight">
          <span className="truncate text-ink">{row.original.guardianName}</span>
          <a href={`tel:${row.original.phone}`} onClick={(e) => e.stopPropagation()} className="truncate text-xs text-ink-2 tabular hover:text-ink">{formatPhone(row.original.phone)}</a>
        </div>
      ),
    },
    { id: "source", header: "Came via", meta: { label: "Came via", hideBelow: "lg" }, cell: ({ row }) => <span className="text-ink-2">{inquirySourceLabel[row.original.source]}</span> },
    {
      id: "followUp",
      header: "Follow up",
      meta: { sortKey: "followUp", label: "Follow up" },
      cell: ({ row }) => {
        const due = row.original.followUpOn;
        if (!due) return <span className="text-ink-3">—</span>;
        const open = row.original.status === "New" || row.original.status === "Contacted";
        const overdue = open && due < today;
        return (
          <span className={cn("tabular", overdue ? "font-medium text-danger" : due === today && open ? "font-medium text-ink" : "text-ink-2")}>
            {due === today ? "Today" : formatDate(due)}{overdue && " · overdue"}
          </span>
        );
      },
    },
    { id: "status", header: "Status", meta: { label: "Status" }, cell: ({ row }) => <StatusBadge tone={statusTone[row.original.status]}>{inquiryStatusLabel[row.original.status]}</StatusBadge> },
    {
      id: "notes",
      header: "Notes",
      meta: { label: "Notes", hideBelow: "lg" },
      cell: ({ row }) => <span className="line-clamp-2 max-w-72 text-ink-2">{row.original.closedReason ?? row.original.notes ?? ""}</span>,
    },
  ];
}

const inquirySchema = z.object({
  studentName: z.string().trim().min(1, "Enter the child's name.").max(150),
  dateOfBirth: z.string(),
  guardianName: z.string().trim().min(1, "Enter the parent's name.").max(150),
  phone: mobileField,
  email: z.union([z.literal(""), z.email("Enter a valid email address.")]),
  source: z.enum(["WalkIn", "Phone", "Website", "Referral", "SocialMedia", "Event", "Other"]),
  campusId: z.string(),
  gradeId: z.string(),
  followUpOn: z.string(),
  notes: z.string().trim().max(1000),
});
type InquiryValues = z.infer<typeof inquirySchema>;

function InquiryDialog({ inquiry, open, onOpenChange }: { inquiry: Inquiry | null; open: boolean; onOpenChange: (o: boolean) => void }) {
  const save = useSaveInquiry();
  const grades = useGrades();
  const campuses = useCampuses();
  const form = useForm<InquiryValues>({ resolver: zodResolver(inquirySchema) });
  const errors = form.formState.errors;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="lg"
      defaults={{
        studentName: inquiry?.studentName ?? "", dateOfBirth: inquiry?.dateOfBirth ?? "", guardianName: inquiry?.guardianName ?? "", phone: inquiry?.phone ?? "",
        email: inquiry?.email ?? "", source: inquiry?.source ?? "WalkIn", campusId: inquiry?.campusId ?? "", gradeId: inquiry?.gradeId ?? "",
        followUpOn: inquiry?.followUpOn ?? "", notes: inquiry?.notes ?? "",
      }}
      title={inquiry ? "Edit inquiry" : "New inquiry"} description="A family asking about admission. Turn it into an application when they're ready."
      submitLabel={inquiry ? "Save" : "Add inquiry"} successMessage={inquiry ? "Inquiry saved" : "Inquiry added"}
      onSubmit={(v) => save.mutateAsync({
        id: inquiry?.id,
        body: {
          studentName: v.studentName, dateOfBirth: nullIfEmpty(v.dateOfBirth), guardianName: v.guardianName, phone: v.phone, email: nullIfEmpty(v.email),
          source: v.source as InquirySource, campusId: nullIfEmpty(v.campusId), gradeId: nullIfEmpty(v.gradeId), followUpOn: nullIfEmpty(v.followUpOn), notes: nullIfEmpty(v.notes),
        },
      })}>
      <FieldRow>
        <Field label="Child's name" htmlFor="iq-student" error={errors.studentName?.message}><Input {...form.register("studentName")} /></Field>
        <Field label="Date of birth" htmlFor="iq-dob" optional><Input type="date" {...form.register("dateOfBirth")} /></Field>
      </FieldRow>
      <FieldRow>
        <Field label="Parent's name" htmlFor="iq-parent" error={errors.guardianName?.message}><Input {...form.register("guardianName")} /></Field>
        <Field label="Mobile number" htmlFor="iq-phone" error={errors.phone?.message}><Input type="tel" placeholder="0300 1234567" {...form.register("phone")} /></Field>
      </FieldRow>
      <FieldRow cols={3}>
        <Field label="Interested in" htmlFor="iq-grade" optional>
          <NativeSelect {...form.register("gradeId")}>
            <option value="">Not decided</option>
            {(grades.data ?? []).filter((g) => g.isActive).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
          </NativeSelect>
        </Field>
        <Field label="Campus" htmlFor="iq-campus" optional>
          <NativeSelect {...form.register("campusId")}>
            <option value="">Either</option>
            {(campuses.data ?? []).filter((c) => c.isActive).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </NativeSelect>
        </Field>
        <Field label="Came via" htmlFor="iq-source">
          <NativeSelect {...form.register("source")}>
            {(Object.keys(inquirySourceLabel) as InquirySource[]).map((s) => <option key={s} value={s}>{inquirySourceLabel[s]}</option>)}
          </NativeSelect>
        </Field>
      </FieldRow>
      <FieldRow>
        <Field label="Email" htmlFor="iq-email" optional error={errors.email?.message}><Input type="email" {...form.register("email")} /></Field>
        <Field label="Follow up on" htmlFor="iq-follow" optional><Input type="date" {...form.register("followUpOn")} /></Field>
      </FieldRow>
      <Field label="Notes" htmlFor="iq-notes" optional><Textarea rows={2} {...form.register("notes")} /></Field>
    </FormDialog>
  );
}

function CloseInquiryDialog({ inquiry, onOpenChange }: { inquiry: Inquiry | null; onOpenChange: (o: boolean) => void }) {
  const action = useInquiryAction();
  const form = useForm<{ reason: string }>();
  return (
    <FormDialog open={inquiry !== null} onOpenChange={onOpenChange} form={form} size="sm" defaults={{ reason: "" }}
      title="Close this inquiry?" description={inquiry ? `${inquiry.studentName} · ${inquiry.guardianName}` : undefined}
      submitLabel="Close inquiry" successMessage="Inquiry closed"
      onSubmit={(v) => action.mutateAsync({ id: inquiry!.id, action: "close", reason: nullIfEmpty(v.reason) })}>
      <Field label="Reason" htmlFor="ci-reason" optional><Input placeholder="e.g. Not interested, fees, distance" {...form.register("reason")} /></Field>
    </FormDialog>
  );
}

export default function InquiriesPage() {
  const router = useRouter();
  const { can } = useSession();
  const today = todayIso();
  const { params, update, clearFilters, apiQuery, activeFilterCount } = useListParams({ sort: "followUp", dir: "asc" });
  const inquiries = useInquiries(apiQuery);
  const grades = useGrades();
  const action = useInquiryAction();
  const [density, setDensity] = useTablePreference<Density>("inquiries.density", "comfortable");
  const [visibility, setVisibility] = useTablePreference<VisibilityState>("inquiries.columns", {});
  const [editing, setEditing] = React.useState<Inquiry | "new" | null>(null);
  const [closing, setClosing] = React.useState<Inquiry | null>(null);
  const columns = React.useMemo(() => buildColumns(today), [today]);
  const canCreate = can(P.Admissions.Create);
  const canUpdate = can(P.Admissions.Update);

  const convert = (i: Inquiry) => {
    const q = new URLSearchParams({ inquiry: i.id, student: i.studentName, guardian: i.guardianName, phone: i.phone });
    if (i.gradeId) q.set("grade", i.gradeId);
    if (i.campusId) q.set("campus", i.campusId);
    router.push(`/admissions/applications/new?${q}`);
  };
  const run = async (i: Inquiry, kind: "contacted" | "reopen") => {
    try {
      await action.mutateAsync({ id: i.id, action: kind });
      toast.success(kind === "contacted" ? "Marked as contacted" : "Inquiry reopened");
    } catch (error) {
      toast.error(error);
    }
  };

  return (
    <Page>
      <PageHeader title="Admissions" description="Families who have asked about a place. Keep follow-ups on time and turn them into applications."
        actions={canCreate && <Button variant="primary" onClick={() => setEditing("new")}><MessageSquarePlus /> New inquiry</Button>}
        meta={<AdmissionsNav />} />
      <div className="rounded-lg border border-line bg-surface">
        <TableToolbar>
          <TableSearch value={params.search} onChange={(search) => update({ search })} placeholder="Child, parent or mobile" />
          <FacetFilter title="Status" single selected={params.filters.status ?? []}
            options={(Object.keys(inquiryStatusLabel) as InquiryStatus[]).map((s) => ({ value: s, label: inquiryStatusLabel[s] }))}
            onChange={(v) => update({ filters: { status: v } })} />
          <FacetFilter title="Follow-up due" single selected={params.filters.followUpDue ?? []}
            options={[{ value: "true", label: "Due today or overdue" }]}
            onChange={(v) => update({ filters: { followUpDue: v } })} />
          <FacetFilter title="Class" single selected={params.filters.gradeId ?? []} searchable
            options={(grades.data ?? []).filter((g) => g.isActive).map((g) => ({ value: g.id, label: g.name }))}
            onChange={(v) => update({ filters: { gradeId: v } })} />
          <ClearFiltersButton count={activeFilterCount} onClear={clearFilters} />
          <div className="ml-auto">
            <ViewMenu columns={columns} visibility={visibility} onVisibilityChange={setVisibility} density={density} onDensityChange={setDensity} />
          </div>
        </TableToolbar>
        <DataTable caption="Inquiries" columns={columns} rows={inquiries.data?.items} total={inquiries.data?.total ?? 0} params={params}
          onParamsChange={(patch) => update(patch, false)} getRowId={(i) => i.id} loading={inquiries.isPending} fetching={inquiries.isFetching}
          error={inquiries.error} onRetry={() => void inquiries.refetch()} filtered={activeFilterCount > 0} density={density}
          columnVisibility={visibility} onColumnVisibilityChange={setVisibility}
          onRowClick={canUpdate ? (i) => setEditing(i) : undefined}
          empty={<EmptyState icon={<PhoneCall />} title="No open inquiries" description="Record families who call or visit, so nobody waits for a call back." />}
          rowActions={(i) => (
            <>
              {i.applicationId ? (
                <DropdownMenuItem onSelect={() => router.push(`/admissions/applications/${i.applicationId}`)}><ArrowRight /> Open application</DropdownMenuItem>
              ) : canCreate && i.status !== "Closed" && (
                <DropdownMenuItem onSelect={() => convert(i)}><ArrowRight /> Turn into application</DropdownMenuItem>
              )}
              {canUpdate && i.status === "New" && <DropdownMenuItem onSelect={() => void run(i, "contacted")}><Check /> Mark contacted</DropdownMenuItem>}
              {canUpdate && <DropdownMenuItem onSelect={() => setEditing(i)}><Pencil /> Edit</DropdownMenuItem>}
              {canUpdate && <DropdownMenuSeparator />}
              {canUpdate && i.status === "Closed" && <DropdownMenuItem onSelect={() => void run(i, "reopen")}><RotateCcw /> Reopen</DropdownMenuItem>}
              {canUpdate && i.status !== "Closed" && i.status !== "Converted" && (
                <DropdownMenuItem tone="danger" onSelect={() => setClosing(i)}><X /> Close…</DropdownMenuItem>
              )}
            </>
          )} />
      </div>
      <InquiryDialog inquiry={editing === "new" ? null : editing} open={editing !== null} onOpenChange={(o) => !o && setEditing(null)} />
      <CloseInquiryDialog inquiry={closing} onOpenChange={(o) => !o && setClosing(null)} />
    </Page>
  );
}
