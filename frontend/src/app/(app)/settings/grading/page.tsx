"use client";

import * as React from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { BadgeCheck, Plus, Star, Trash2 } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useDeleteGradingScale, useGradingScales, useMakeDefaultScale, useSaveGradingScale, type GradingScale } from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input, NativeSelect } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Badge, Skeleton } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Checkbox } from "@/components/ui/toggles";
import { toast } from "@/components/ui/toaster";
import { FormDialog } from "@/components/forms/form-dialog";

const band = z.object({
  minPercent: z.coerce.number<number>().min(0).max(100),
  maxPercent: z.coerce.number<number>().min(0).max(100),
  grade: z.string().trim().min(1, "Grade?").max(10),
  gradePoint: z.string(),
  remark: z.string().max(60),
  isPassing: z.boolean(),
});
const schema = z.object({
  name: z.string().trim().min(1, "Enter a name.").max(100),
  type: z.enum(["Percentage", "GradePoint"]),
  bands: z.array(band).min(1, "Add at least one band."),
}).superRefine((v, ctx) => {
  const sorted = [...v.bands].map((b, i) => ({ ...b, i })).sort((a, b) => a.minPercent - b.minPercent);
  sorted.forEach((b, k) => {
    if (b.minPercent > b.maxPercent) ctx.addIssue({ code: "custom", path: ["bands", b.i, "maxPercent"], message: "Below the minimum." });
    const prev = sorted[k - 1];
    if (prev && b.minPercent <= prev.maxPercent) ctx.addIssue({ code: "custom", path: ["bands", b.i, "minPercent"], message: `Overlaps ${prev.grade}.` });
  });
});
type Values = z.infer<typeof schema>;

function ScaleDialog({ scale, open, onOpenChange }: { scale: GradingScale | null; open: boolean; onOpenChange: (o: boolean) => void }) {
  const save = useSaveGradingScale();
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  const bands = useFieldArray({ control: form.control, name: "bands" });
  const e = form.formState.errors;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form} size="lg"
      defaults={{
        name: scale?.name ?? "", type: scale?.type ?? "Percentage",
        bands: (scale?.bands ?? [{ minPercent: 33, maxPercent: 100, grade: "P", gradePoint: null, remark: "Pass", isPassing: true }, { minPercent: 0, maxPercent: 32.99, grade: "F", gradePoint: null, remark: "Fail", isPassing: false }])
          .map((b) => ({ ...b, gradePoint: b.gradePoint?.toString() ?? "", remark: b.remark ?? "" })),
      }}
      title={scale ? `Edit ${scale.name}` : "New grading scale"} description="Bands from highest to lowest. Gaps (like 79.995%) fall into the band below."
      submitLabel="Save scale" successMessage="Grading scale saved"
      onSubmit={(v) => save.mutateAsync({ id: scale?.id, body: {
        name: v.name, type: v.type,
        bands: v.bands.map((b) => ({ ...b, gradePoint: b.gradePoint === "" ? null : Number(b.gradePoint), remark: b.remark || null })),
      } })}>
      <FieldRow>
        <Field label="Name" htmlFor="gs-name" error={e.name?.message}><Input autoFocus {...form.register("name")} /></Field>
        <Field label="Shows" htmlFor="gs-type">
          <NativeSelect {...form.register("type")}><option value="Percentage">Grades from percentages</option><option value="GradePoint">Grades with grade points (GPA)</option></NativeSelect>
        </Field>
      </FieldRow>
      <div className="overflow-hidden rounded-lg border border-line">
        <table className="w-full text-left text-sm">
          <thead className="bg-sunken/60 text-xs text-ink-2">
            <tr><th className="px-3 py-2 font-medium">Grade</th><th className="px-2 py-2 font-medium">From %</th><th className="px-2 py-2 font-medium">To %</th>
              <th className="px-2 py-2 font-medium">Points</th><th className="px-2 py-2 font-medium">Remark</th><th className="px-2 py-2 font-medium">Pass</th><th /></tr>
          </thead>
          <tbody>
            {bands.fields.map((field, index) => {
              const be = e.bands?.[index];
              return (
                <tr key={field.id} className="border-t border-line align-top">
                  <td className="px-3 py-1.5"><Input inputSize="sm" className="w-16" aria-label="Grade" aria-invalid={!!be?.grade} {...form.register(`bands.${index}.grade`)} /></td>
                  <td className="px-2 py-1.5"><Input inputSize="sm" type="number" step="0.01" className="w-20" aria-label="From" aria-invalid={!!be?.minPercent} title={be?.minPercent?.message} {...form.register(`bands.${index}.minPercent`)} /></td>
                  <td className="px-2 py-1.5"><Input inputSize="sm" type="number" step="0.01" className="w-20" aria-label="To" aria-invalid={!!be?.maxPercent} title={be?.maxPercent?.message} {...form.register(`bands.${index}.maxPercent`)} /></td>
                  <td className="px-2 py-1.5"><Input inputSize="sm" type="number" step="0.1" className="w-16" aria-label="Grade points" {...form.register(`bands.${index}.gradePoint`)} /></td>
                  <td className="px-2 py-1.5"><Input inputSize="sm" aria-label="Remark" {...form.register(`bands.${index}.remark`)} /></td>
                  <td className="px-2 py-2.5"><Checkbox aria-label="Passing" checked={form.watch(`bands.${index}.isPassing`)} onCheckedChange={(v) => form.setValue(`bands.${index}.isPassing`, v === true, { shouldDirty: true })} /></td>
                  <td className="px-2 py-1.5"><Button type="button" size="icon-xs" variant="ghost" aria-label="Remove band" onClick={() => bands.remove(index)}><Trash2 /></Button></td>
                </tr>
              );
            })}
          </tbody>
        </table>
        {e.bands && <p className="border-t border-line px-3 py-2 text-xs text-danger">{e.bands.root?.message ?? e.bands.message ?? "Check the highlighted bands."}</p>}
      </div>
      <div><Button type="button" size="sm" variant="ghost" onClick={() => bands.append({ minPercent: 0, maxPercent: 0, grade: "", gradePoint: "", remark: "", isPassing: true })}><Plus /> Add band</Button></div>
    </FormDialog>
  );
}

export default function GradingPage() {
  const { can } = useSession();
  const editable = can(P.Academics.Manage);
  const scales = useGradingScales();
  const makeDefault = useMakeDefaultScale();
  const remove = useDeleteGradingScale();
  const [dialog, setDialog] = React.useState<{ open: boolean; scale: GradingScale | null }>({ open: false, scale: null });

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Grading" }]} title="Grading"
        description="How percentages become grades on results and report cards. Each exam session can use a different scale."
        actions={editable && <Button variant="primary" onClick={() => setDialog({ open: true, scale: null })}><Plus /> New scale</Button>} />
      {scales.isPending ? <Skeleton className="h-80" /> : scales.error ? <ErrorState error={scales.error} onRetry={() => void scales.refetch()} /> :
        scales.data.length === 0 ? <EmptyState icon={<BadgeCheck />} title="No grading scales" /> : (
          <div className="grid gap-5 lg:grid-cols-2">
            {scales.data.map((scale) => (
              <section key={scale.id} className="rounded-lg border border-line bg-surface">
                <header className="flex items-center gap-2 border-b border-line px-4 py-3">
                  <h2 className="flex-1 text-md font-semibold">{scale.name}</h2>
                  {scale.isDefault && <Badge tone="accent">Default</Badge>}
                  {editable && <Button size="sm" variant="ghost" onClick={() => setDialog({ open: true, scale })}>Edit</Button>}
                </header>
                <table className="w-full text-left text-sm">
                  <tbody>
                    {scale.bands.map((b) => (
                      <tr key={b.grade} className="border-b border-line last:border-b-0">
                        <td className="w-16 px-4 py-2 font-display text-lg">{b.grade}</td>
                        <td className="px-2 py-2 text-ink-2 tabular">{b.minPercent}% – {b.maxPercent}%</td>
                        <td className="px-2 py-2 text-ink-2">{b.remark}</td>
                        <td className="px-4 py-2 text-right text-ink-3 tabular">{b.gradePoint !== null ? `${b.gradePoint} pts` : ""}{!b.isPassing && <span className="ml-2 text-danger">Fail</span>}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {editable && !scale.isDefault && (
                  <footer className="flex gap-1 border-t border-line px-2 py-1.5">
                    <Button size="sm" variant="ghost" onClick={async () => { try { await makeDefault.mutateAsync({ id: scale.id }); toast.success(`${scale.name} is now the default`); } catch (err) { toast.error(err); } }}>
                      <Star /> Make default
                    </Button>
                    <Button size="sm" variant="danger-ghost" onClick={async () => { try { await remove.mutateAsync(scale.id); toast.success("Scale removed"); } catch (err) { toast.error(err); } }}>
                      <Trash2 /> Remove
                    </Button>
                  </footer>
                )}
              </section>
            ))}
          </div>
        )}
      <ScaleDialog open={dialog.open} scale={dialog.scale} onOpenChange={(o) => setDialog((d) => ({ ...d, open: o }))} />
    </Page>
  );
}
