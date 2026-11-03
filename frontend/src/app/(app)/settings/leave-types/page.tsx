"use client";

import * as React from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Plus } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useDeleteLeaveType, useLeaveTypes, useSaveLeaveType, type LeaveType } from "@/lib/api/attendance";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Badge, Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { Field, FieldRow } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/toggles";
import { toast } from "@/components/ui/toaster";
import { RecordList, RecordRow, SectionHeading } from "@/components/ui/record-list";
import { FormDialog } from "@/components/forms/form-dialog";

const schema = z.object({
  code: z.string().trim().min(1, "Enter a short code.").max(10).regex(/^[A-Za-z0-9]+$/, "Letters and digits only."),
  name: z.string().trim().min(1, "Enter a name.").max(60),
  annualDays: z.string().refine((v) => v === "" || (Number(v) >= 0 && Number(v) <= 365), "Between 0 and 365, or leave blank for no limit."),
  isPaid: z.boolean(),
  requiresDocument: z.boolean(),
  isActive: z.boolean(),
});
type Values = z.infer<typeof schema>;

function LeaveTypeDialog({ type, open, onOpenChange, order }: { type: LeaveType | null; open: boolean; onOpenChange: (o: boolean) => void; order: number }) {
  const save = useSaveLeaveType();
  const remove = useDeleteLeaveType();
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="sm" title={type ? `Edit ${type.name}` : "New leave type"}
      defaults={{ code: type?.code ?? "", name: type?.name ?? "", annualDays: type?.annualDays?.toString() ?? "", isPaid: type?.isPaid ?? true, requiresDocument: type?.requiresDocument ?? false, isActive: type?.isActive ?? true }}
      submitLabel="Save leave type" successMessage="Leave type saved"
      footerStart={type && <Button type="button" variant="danger-ghost" onClick={async () => { try { await remove.mutateAsync(type.id); toast.success("Leave type removed"); onOpenChange(false); } catch (e) { toast.error(e); } }}>Remove</Button>}
      onSubmit={(v) => save.mutateAsync({ id: type?.id, body: { code: v.code, name: v.name, annualDays: v.annualDays === "" ? null : Number(v.annualDays), isPaid: v.isPaid, requiresDocument: v.requiresDocument, isActive: v.isActive, sortOrder: type?.sortOrder ?? order } })}>
      <FieldRow>
        <Field label="Name" htmlFor="lt-name" error={form.formState.errors.name?.message}><Input id="lt-name" autoFocus placeholder="e.g. Casual leave" {...form.register("name")} /></Field>
        <Field label="Code" htmlFor="lt-code" error={form.formState.errors.code?.message}><Input id="lt-code" className="uppercase" placeholder="CL" {...form.register("code")} /></Field>
      </FieldRow>
      <Field label="Days a year" htmlFor="lt-days" optional hint="Blank means no yearly limit (e.g. unpaid leave). The leave year is the academic year." error={form.formState.errors.annualDays?.message}>
        <Input id="lt-days" type="number" min={0} max={365} step="0.5" className="max-w-32" {...form.register("annualDays")} />
      </Field>
      {(["isPaid", "requiresDocument", "isActive"] as const).map((key) => (
        <Controller key={key} control={form.control} name={key} render={({ field }) => (
          <label className="flex items-center gap-2.5 text-sm text-ink">
            <Switch checked={field.value} onCheckedChange={field.onChange} />
            {key === "isPaid" ? "Paid leave" : key === "requiresDocument" ? "Needs a supporting document" : "Can be requested"}
          </label>
        )} />
      ))}
    </FormDialog>
  );
}

export default function LeaveTypesPage() {
  const { can } = useSession();
  const editable = can(P.Hr.Configure);
  const types = useLeaveTypes(true);
  const [dialog, setDialog] = React.useState<{ open: boolean; item: LeaveType | null }>({ open: false, item: null });
  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Leave types" }]} title="Leave types"
        description="The kinds of staff leave and how many days a year each allows. Balances count working days only." />
      <section className="max-w-3xl">
        <SectionHeading title="Staff leave" actions={editable && <Button size="sm" variant="secondary" onClick={() => setDialog({ open: true, item: null })}><Plus /> Leave type</Button>} />
        {types.isPending ? <Skeleton className="h-64" /> : types.error ? <ErrorState error={types.error} /> : (
          <RecordList>
            {types.data.map((t) => (
              <RecordRow key={t.id} muted={!t.isActive} onClick={editable ? () => setDialog({ open: true, item: t }) : undefined}
                leading={<span className="grid size-9 place-items-center rounded-md border border-line bg-sunken font-mono text-xs text-ink-2">{t.code}</span>}
                title={t.name}
                badges={<>{!t.isPaid && <Badge>Unpaid</Badge>}{t.requiresDocument && <Badge>Document needed</Badge>}{!t.isActive && <Badge>Switched off</Badge>}</>}
                subtitle={t.annualDays === null ? "No yearly limit" : `${t.annualDays} days a year`} />
            ))}
          </RecordList>
        )}
      </section>
      <LeaveTypeDialog type={dialog.item} open={dialog.open} onOpenChange={(open) => setDialog((d) => ({ ...d, open }))} order={(types.data?.length ?? 0) + 1} />
    </Page>
  );
}
