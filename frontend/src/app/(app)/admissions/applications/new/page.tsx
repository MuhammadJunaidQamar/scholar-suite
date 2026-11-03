"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCampuses } from "@/lib/api/access";
import { useAcademicYears, useGrades } from "@/lib/api/org";
import { useCreateApplication } from "@/lib/api/admissions";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { NativeSelect } from "@/components/ui/input";
import { Field, FieldRow, FormSection } from "@/components/ui/field";
import { EmptyState } from "@/components/ui/states";
import { toast } from "@/components/ui/toaster";
import {
  ApplicantAddressFields,
  ApplicantChildFields,
  ApplicantNotesField,
  ApplicantParentFields,
  ApplicantSchoolingFields,
  applicantInput,
  applicantShape,
  emptyApplicant,
  useApplicantFormAdapter,
} from "@/components/admissions/applicant-form";

const schema = z.object({
  ...applicantShape,
  academicYearId: z.string().min(1, "Choose the session."),
  gradeId: z.string().min(1, "Choose the class."),
  campusId: z.string().min(1, "Choose the campus."),
});
type Values = z.infer<typeof schema>;

export default function NewApplicationPage() {
  const router = useRouter();
  const search = useSearchParams();
  const { can } = useSession();
  const create = useCreateApplication();
  const years = useAcademicYears();
  const grades = useGrades();
  const campuses = useCampuses();
  const inquiryId = search.get("inquiry");

  const openYears = (years.data ?? []).filter((y) => y.status !== "Closed").sort((a, b) => a.startDate.localeCompare(b.startDate));
  const defaultYear = openYears.find((y) => y.status === "Planned") ?? openYears.find((y) => y.isCurrent) ?? openYears[0];
  const activeCampuses = (campuses.data ?? []).filter((c) => c.isActive);

  // Prefilled from an inquiry when converting one (name, parent and phone arrive as query parameters).
  const [first, ...rest] = (search.get("student") ?? "").split(" ");
  const [guardianFirst, ...guardianRest] = (search.get("guardian") ?? "").split(" ");
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    values: {
      ...emptyApplicant,
      firstName: first ?? "", lastName: rest.join(" "),
      guardianFirstName: guardianFirst ?? "", guardianLastName: guardianRest.join(" "), guardianPhone: search.get("phone") ?? "",
      academicYearId: defaultYear?.id ?? "", gradeId: search.get("grade") ?? "",
      campusId: search.get("campus") ?? activeCampuses.find((c) => c.isMain)?.id ?? activeCampuses[0]?.id ?? "",
    },
    resetOptions: { keepDirtyValues: true },
  });
  const applicant = useApplicantFormAdapter(form);
  const errors = form.formState.errors;

  if (!can(P.Admissions.Create)) {
    return <Page><EmptyState title="You can't record applications" description="Ask an administrator for the admissions permission." /></Page>;
  }

  const submit = form.handleSubmit(async (v) => {
    try {
      const created = await create.mutateAsync({
        academicYearId: v.academicYearId, campusId: v.campusId, gradeId: v.gradeId, applicant: applicantInput(v), inquiryId, submit: true,
      });
      toast.success(`Application ${created.applicationNumber} recorded`, created.applicantName);
      router.push(`/admissions/applications/${created.id}`);
    } catch (error) {
      toast.error(error, "The application couldn't be saved");
    }
  }, () => toast.warning("Some details need attention", "Check the highlighted fields."));

  return (
    <Page>
      <form onSubmit={submit} noValidate>
        <PageHeader breadcrumbs={[{ label: "Admissions", href: "/admissions" }, { label: "New application" }]} title="New application"
          description={inquiryId ? "Converting an inquiry: check the details carried over and fill in the rest." : "For a family applying at the office. Families can also apply online."} />

        <FormSection title="Applying for" description="The session admissions are open for, and the class the child would join.">
          <FieldRow cols={3}>
            <Field label="Session" htmlFor="na-year" error={errors.academicYearId?.message}>
              <NativeSelect {...form.register("academicYearId")}>
                {openYears.map((y) => <option key={y.id} value={y.id}>{y.name}{y.status === "Planned" ? " (next)" : y.isCurrent ? " (current)" : ""}</option>)}
              </NativeSelect>
            </Field>
            <Field label="Class" htmlFor="na-grade" error={errors.gradeId?.message}>
              <NativeSelect {...form.register("gradeId")}>
                <option value="">Choose a class</option>
                {(grades.data ?? []).filter((g) => g.isActive).map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
              </NativeSelect>
            </Field>
            <Field label="Campus" htmlFor="na-campus" error={errors.campusId?.message}>
              <NativeSelect {...form.register("campusId")}>
                {activeCampuses.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </NativeSelect>
            </Field>
          </FieldRow>
        </FormSection>
        <FormSection title="Child" description="As on the B-Form or birth certificate.">
          <ApplicantChildFields form={applicant} idPrefix="na" />
          <ApplicantSchoolingFields form={applicant} idPrefix="na" />
        </FormSection>
        <FormSection title="Parent or guardian" description="Who the school will contact about this application.">
          <ApplicantParentFields form={applicant} idPrefix="na" />
          <ApplicantAddressFields form={applicant} idPrefix="na" />
        </FormSection>
        <FormSection title="Notes">
          <ApplicantNotesField form={applicant} idPrefix="na" label="Notes for the admissions team" />
        </FormSection>

        <div className="sticky bottom-0 z-10 -mx-4 mt-4 flex items-center justify-end gap-2 border-t border-line bg-canvas/95 px-4 py-3 backdrop-blur sm:-mx-6 sm:px-6 lg:-mx-8 lg:px-8">
          <Button type="button" variant="secondary" asChild><Link href="/admissions">Cancel</Link></Button>
          <Button type="submit" variant="primary" loading={create.isPending}>Record application</Button>
        </div>
      </form>
    </Page>
  );
}
