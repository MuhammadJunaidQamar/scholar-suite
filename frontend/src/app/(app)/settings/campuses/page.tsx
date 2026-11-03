"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Building2, Pencil, Plus, Star } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { pluralize } from "@/lib/utils";
import { useCampusDetails, useMakeMainCampus, useSaveCampus, type CampusDetail } from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Badge, Skeleton } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { DropdownMenuItem } from "@/components/ui/menu";
import { Switch } from "@/components/ui/toggles";
import { toast } from "@/components/ui/toaster";
import { RecordList, RecordRow } from "@/components/ui/record-list";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";

const schema = z.object({
  code: z.string().trim().min(1, "Enter a short code.").max(20).regex(/^[A-Za-z0-9-]+$/, "Letters, digits or '-' only."),
  name: z.string().trim().min(2, "Enter the campus name.").max(150),
  line1: z.string().max(200), line2: z.string().max(200), city: z.string().max(100), province: z.string().max(100),
  phone: z.string().max(40),
  email: z.union([z.literal(""), z.email("Enter a valid email address.")]),
  isActive: z.boolean(),
});
type Values = z.infer<typeof schema>;

function CampusDialog({ campus, open, onOpenChange }: { campus: CampusDetail | null; open: boolean; onOpenChange: (o: boolean) => void }) {
  const save = useSaveCampus();
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  const defaults: Values = {
    code: campus?.code ?? "", name: campus?.name ?? "", line1: campus?.address.line1 ?? "", line2: campus?.address.line2 ?? "",
    city: campus?.address.city ?? "", province: campus?.address.province ?? "", phone: campus?.phone ?? "", email: campus?.email ?? "",
    isActive: campus?.isActive ?? true,
  };
  const e = form.formState.errors;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} defaults={defaults}
      title={campus ? `Edit ${campus.name}` : "New campus"} description="A branch with its own students, sections and cash register."
      submitLabel={campus ? "Save campus" : "Add campus"} successMessage={campus ? "Campus saved" : "Campus added"}
      onSubmit={(v) => save.mutateAsync({ id: campus?.id, body: {
        code: v.code, name: v.name, phone: nullIfEmpty(v.phone), email: nullIfEmpty(v.email), isActive: v.isActive,
        address: { line1: nullIfEmpty(v.line1), line2: nullIfEmpty(v.line2), city: nullIfEmpty(v.city), province: nullIfEmpty(v.province), postalCode: campus?.address.postalCode ?? null, countryCode: campus?.address.countryCode ?? "PK" },
      } })}>
      <FieldRow>
        <Field label="Name" htmlFor="c-name" error={e.name?.message}><Input autoFocus {...form.register("name")} /></Field>
        <Field label="Code" htmlFor="c-code" error={e.code?.message} hint="Printed on challans, e.g. DHA."><Input className="uppercase" {...form.register("code")} /></Field>
      </FieldRow>
      <Field label="Address" htmlFor="c-line1" optional><Input {...form.register("line1")} /></Field>
      <FieldRow>
        <Field label="Area" htmlFor="c-line2" optional><Input {...form.register("line2")} /></Field>
        <Field label="City" htmlFor="c-city" optional><Input {...form.register("city")} /></Field>
      </FieldRow>
      <FieldRow>
        <Field label="Phone" htmlFor="c-phone" optional><Input type="tel" {...form.register("phone")} /></Field>
        <Field label="Email" htmlFor="c-email" optional error={e.email?.message}><Input type="email" {...form.register("email")} /></Field>
      </FieldRow>
      {!campus?.isMain && (
        <Field inline label="Active" hint="Inactive campuses are hidden from new admissions and timetables." htmlFor="c-active">
          <Switch checked={form.watch("isActive")} onCheckedChange={(v) => form.setValue("isActive", v, { shouldDirty: true })} />
        </Field>
      )}
    </FormDialog>
  );
}

export default function CampusesPage() {
  const { can } = useSession();
  const editable = can(P.School.Manage);
  const campuses = useCampusDetails();
  const makeMain = useMakeMainCampus();
  const [editing, setEditing] = React.useState<CampusDetail | null>(null);
  const [open, setOpen] = React.useState(false);
  const edit = (campus: CampusDetail | null) => { setEditing(campus); setOpen(true); };

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Campuses" }]} title="Campuses"
        description="Each campus keeps its own sections, rooms and collections; staff can be limited to one campus."
        actions={editable && <Button variant="primary" onClick={() => edit(null)}><Plus /> New campus</Button>} />
      {campuses.isPending ? <Skeleton className="h-40" /> : campuses.error ? <ErrorState error={campuses.error} onRetry={() => void campuses.refetch()} /> :
        campuses.data.length === 0 ? <EmptyState icon={<Building2 />} title="No campuses" /> : (
          <RecordList>
            {campuses.data.map((c) => (
              <RecordRow key={c.id} onClick={editable ? () => edit(c) : undefined} muted={!c.isActive}
                leading={<span className="grid size-9 shrink-0 place-items-center rounded-md border border-line bg-canvas font-mono text-xs text-ink-2">{c.code}</span>}
                title={c.name}
                badges={<>{c.isMain && <Badge tone="accent">Main campus</Badge>}{!c.isActive && <Badge>Inactive</Badge>}</>}
                subtitle={[c.address.line1, c.address.line2, c.address.city].filter(Boolean).join(", ") || "No address yet"}
                facts={[pluralize(c.sectionCount, "section"), pluralize(c.roomCount, "room")]}
                actions={editable ? (
                  <>
                    <DropdownMenuItem onSelect={() => edit(c)}><Pencil /> Edit</DropdownMenuItem>
                    {!c.isMain && c.isActive && (
                      <DropdownMenuItem onSelect={async () => {
                        try { await makeMain.mutateAsync({ id: c.id }); toast.success(`${c.name} is now the main campus`); } catch (err) { toast.error(err); }
                      }}><Star /> Make main campus</DropdownMenuItem>
                    )}
                  </>
                ) : undefined}
              />
            ))}
          </RecordList>
        )}
      <CampusDialog campus={editing} open={open} onOpenChange={setOpen} />
    </Page>
  );
}
