"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Plus } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import {
  designationCategoryLabel, useDeleteDepartment, useDeleteDesignation, useDepartments, useDesignations, useSaveDepartment, useSaveDesignation,
  type Department, type Designation, type DesignationCategory,
} from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { toast } from "@/components/ui/toaster";
import { RecordList, RecordRow, SectionHeading } from "@/components/ui/record-list";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";

const deptSchema = z.object({ code: z.string().trim().min(1, "Enter a code.").max(20), name: z.string().trim().min(1, "Enter a name.").max(100), description: z.string().max(500) });
const desigSchema = z.object({ name: z.string().trim().min(1, "Enter a name.").max(100), category: z.enum(["Teaching", "NonTeaching", "Administrative", "Support"]) });

function DepartmentDialog({ department, open, onOpenChange }: { department: Department | null; open: boolean; onOpenChange: (o: boolean) => void }) {
  const save = useSaveDepartment();
  const remove = useDeleteDepartment();
  const form = useForm<z.infer<typeof deptSchema>>({ resolver: zodResolver(deptSchema) });
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="sm"
      defaults={{ code: department?.code ?? "", name: department?.name ?? "", description: department?.description ?? "" }}
      title={department ? `Edit ${department.name}` : "New department"} submitLabel="Save department" successMessage="Department saved"
      footerStart={department && <Button type="button" variant="danger-ghost" onClick={async () => { try { await remove.mutateAsync(department.id); toast.success("Department removed"); onOpenChange(false); } catch (e) { toast.error(e); } }}>Remove</Button>}
      onSubmit={(v) => save.mutateAsync({ id: department?.id, body: { ...v, description: nullIfEmpty(v.description) } })}>
      <FieldRow>
        <Field label="Name" htmlFor="d-name" error={form.formState.errors.name?.message}><Input autoFocus {...form.register("name")} /></Field>
        <Field label="Code" htmlFor="d-code" error={form.formState.errors.code?.message}><Input className="uppercase" {...form.register("code")} /></Field>
      </FieldRow>
      <Field label="Description" htmlFor="d-desc" optional><Textarea rows={2} {...form.register("description")} /></Field>
    </FormDialog>
  );
}

function DesignationDialog({ designation, open, onOpenChange }: { designation: Designation | null; open: boolean; onOpenChange: (o: boolean) => void }) {
  const save = useSaveDesignation();
  const remove = useDeleteDesignation();
  const form = useForm<z.infer<typeof desigSchema>>({ resolver: zodResolver(desigSchema) });
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="sm"
      defaults={{ name: designation?.name ?? "", category: designation?.category ?? "Teaching" }}
      title={designation ? `Edit ${designation.name}` : "New designation"} submitLabel="Save designation" successMessage="Designation saved"
      footerStart={designation && <Button type="button" variant="danger-ghost" onClick={async () => { try { await remove.mutateAsync(designation.id); toast.success("Designation removed"); onOpenChange(false); } catch (e) { toast.error(e); } }}>Remove</Button>}
      onSubmit={(v) => save.mutateAsync({ id: designation?.id, body: v })}>
      <Field label="Title" htmlFor="ds-name" error={form.formState.errors.name?.message}><Input autoFocus placeholder="e.g. Senior Teacher" {...form.register("name")} /></Field>
      <Field label="Category" htmlFor="ds-cat"><NativeSelect {...form.register("category")}>{Object.entries(designationCategoryLabel).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</NativeSelect></Field>
    </FormDialog>
  );
}

export default function DepartmentsPage() {
  const { can } = useSession();
  const editable = can(P.School.Manage);
  const departments = useDepartments();
  const designations = useDesignations();
  const [dept, setDept] = React.useState<{ open: boolean; item: Department | null }>({ open: false, item: null });
  const [desig, setDesig] = React.useState<{ open: boolean; item: Designation | null }>({ open: false, item: null });
  const categories = Object.keys(designationCategoryLabel) as DesignationCategory[];

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Departments & designations" }]} title="Departments & designations"
        description="How staff are organised (departments) and what their positions are called (designations)." />
      <div className="grid gap-10 xl:grid-cols-2">
        <section>
          <SectionHeading title="Departments" actions={editable && <Button size="sm" variant="secondary" onClick={() => setDept({ open: true, item: null })}><Plus /> Department</Button>} />
          {departments.isPending ? <Skeleton className="h-64" /> : departments.error ? <ErrorState error={departments.error} /> : (
            <RecordList>
              {departments.data.map((d) => (
                <RecordRow key={d.id} onClick={editable ? () => setDept({ open: true, item: d }) : undefined}
                  leading={<span className="w-12 shrink-0 font-mono text-xs text-ink-2">{d.code}</span>} title={d.name} subtitle={d.description} />
              ))}
            </RecordList>
          )}
        </section>
        <section>
          <SectionHeading title="Designations" actions={editable && <Button size="sm" variant="secondary" onClick={() => setDesig({ open: true, item: null })}><Plus /> Designation</Button>} />
          {designations.isPending ? <Skeleton className="h-64" /> : designations.error ? <ErrorState error={designations.error} /> : (
            <div className="grid gap-5">
              {categories.map((category) => {
                const list = designations.data.filter((d) => d.category === category);
                if (list.length === 0) return null;
                return (
                  <div key={category} className="grid gap-1.5">
                    <p className="text-sm text-ink-2">{designationCategoryLabel[category]}</p>
                    <RecordList>
                      {list.map((d) => <RecordRow key={d.id} onClick={editable ? () => setDesig({ open: true, item: d }) : undefined} title={d.name} />)}
                    </RecordList>
                  </div>
                );
              })}
            </div>
          )}
        </section>
      </div>
      <DepartmentDialog open={dept.open} department={dept.item} onOpenChange={(o) => setDept((s) => ({ ...s, open: o }))} />
      <DesignationDialog open={desig.open} designation={desig.item} onOpenChange={(o) => setDesig((s) => ({ ...s, open: o }))} />
    </Page>
  );
}
