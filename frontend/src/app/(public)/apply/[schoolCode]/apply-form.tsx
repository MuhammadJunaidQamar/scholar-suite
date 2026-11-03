"use client";

import * as React from "react";
import { useParams } from "next/navigation";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { CircleAlert, CircleCheck, Mail, Phone } from "lucide-react";
import { ApiError } from "@/lib/api/client";
import { productName } from "@/lib/brand";
import { submitPublicApplication, usePublicSchool, type PublicReceipt, type PublicSchool } from "@/lib/api/admissions";
import { Button } from "@/components/ui/button";
import { NativeSelect } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Checkbox } from "@/components/ui/toggles";
import { Skeleton } from "@/components/ui/display";
import { applyFieldErrors } from "@/components/forms/form-dialog";
import { SchoolCrest, crestUrl } from "@/components/shell/sidebar";
import {
  ApplicantAddressFields,
  ApplicantChildFields,
  ApplicantNotesField,
  ApplicantParentFields,
  ApplicantSchoolingFields,
  RequiredDocuments,
  applicantInput,
  applicantShape,
  emptyApplicant,
  useApplicantFormAdapter,
} from "@/components/admissions/applicant-form";

const schema = z.object({
  ...applicantShape,
  gradeId: z.string().min(1, "Choose the class."),
  campusId: z.string().min(1, "Choose the campus."),
  confirmed: z.literal(true, "Please confirm the details are correct."),
});
type Values = z.infer<typeof schema>;

/** The school's colours on this page only (the page is the school's, not the product's). */
function useSchoolBrand(color: string | null | undefined) {
  React.useEffect(() => {
    if (!color || !/^#[0-9a-f]{6}$/i.test(color)) return;
    const root = document.documentElement;
    const previous = root.style.getPropertyValue("--brand");
    root.style.setProperty("--brand", color);
    return () => {
      if (previous) root.style.setProperty("--brand", previous);
      else root.style.removeProperty("--brand");
    };
  }, [color]);
}

function Crest({ school }: { school: PublicSchool }) {
  return <SchoolCrest name={school.shortName ?? school.name} size={56} logo={school.logoFileId ? crestUrl(school.code, school.logoFileId) : null} />;
}

function Section({ title, description, children }: { title: string; description?: string; children: React.ReactNode }) {
  return (
    <section className="grid gap-4 border-t border-line py-7 first:border-t-0 first:pt-0">
      <div className="grid gap-1">
        <h2 className="text-md font-semibold text-ink">{title}</h2>
        {description && <p className="text-sm text-ink-2">{description}</p>}
      </div>
      {children}
    </section>
  );
}

function Contact({ school }: { school: PublicSchool }) {
  if (!school.phone && !school.email) return null;
  return (
    <p className="flex flex-wrap gap-x-5 gap-y-1 text-sm text-ink-2">
      {school.phone && <a href={`tel:${school.phone}`} className="inline-flex items-center gap-1.5 hover:text-ink"><Phone className="size-3.5" aria-hidden />{school.phone}</a>}
      {school.email && <a href={`mailto:${school.email}`} className="inline-flex items-center gap-1.5 hover:text-ink"><Mail className="size-3.5" aria-hidden />{school.email}</a>}
    </p>
  );
}

function ApplicationForm({ school, onReceived }: { school: PublicSchool; onReceived: (r: PublicReceipt) => void }) {
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { ...emptyApplicant, gradeId: "", campusId: school.campuses.length === 1 ? school.campuses[0]!.id : "", confirmed: false as never },
  });
  const applicant = useApplicantFormAdapter(form);
  const confirmed = useWatch({ control: form.control, name: "confirmed" });
  const [problem, setProblem] = React.useState<string | null>(null);
  const errors = form.formState.errors;

  const submit = form.handleSubmit(async (v) => {
    setProblem(null);
    try {
      onReceived(await submitPublicApplication(school.code, { gradeId: v.gradeId, campusId: v.campusId, applicant: applicantInput(v) }));
      window.scrollTo({ top: 0 });
    } catch (error) {
      if (applyFieldErrors(error, form)) return;
      setProblem(error instanceof ApiError ? error.message : "The application couldn't be sent. Check your connection and try again.");
    }
  });

  return (
    <form onSubmit={submit} noValidate>
      <Section title="Applying for" description={`Admissions for the ${school.academicYearName} session.`}>
        <FieldRow>
          <Field label="Class" htmlFor="pa-grade" error={errors.gradeId?.message}>
            <NativeSelect {...form.register("gradeId")}>
              <option value="">Choose a class</option>
              {school.grades.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
            </NativeSelect>
          </Field>
          {school.campuses.length > 1 && (
            <Field label="Campus" htmlFor="pa-campus" error={errors.campusId?.message}>
              <NativeSelect {...form.register("campusId")}>
                <option value="">Choose a campus</option>
                {school.campuses.map((c) => <option key={c.id} value={c.id}>{c.name}{c.city ? `, ${c.city}` : ""}</option>)}
              </NativeSelect>
            </Field>
          )}
        </FieldRow>
      </Section>
      <Section title="Your child" description="As written on the B-Form or birth certificate.">
        <ApplicantChildFields form={applicant} idPrefix="pa" />
        <ApplicantSchoolingFields form={applicant} idPrefix="pa" />
      </Section>
      <Section title="Parent or guardian" description="The school will contact you on this number about the application.">
        <ApplicantParentFields form={applicant} idPrefix="pa" />
        <ApplicantAddressFields form={applicant} idPrefix="pa" />
      </Section>
      <Section title="Anything else">
        <ApplicantNotesField form={applicant} idPrefix="pa" label="Siblings at the school, special needs, or questions" />
        {school.requiredDocuments.length > 0 && (
          <div className="grid gap-2 rounded-md border border-line bg-sunken/50 px-4 py-3">
            <p className="text-sm font-medium text-ink">Please bring these when you visit</p>
            <RequiredDocuments documents={school.requiredDocuments} />
          </div>
        )}
        <label className="flex items-start gap-2.5 text-sm text-ink">
          <Checkbox className="mt-0.5" checked={confirmed === true}
            onCheckedChange={(v) => form.setValue("confirmed", (v === true) as never, { shouldValidate: form.formState.isSubmitted })} />
          <span>The details above are correct, and {school.shortName ?? school.name} may contact me about this application.</span>
        </label>
        {errors.confirmed && <p className="text-sm text-danger">{errors.confirmed.message}</p>}
      </Section>
      {problem && (
        <p role="alert" className="mb-4 flex gap-2 rounded-md border border-danger/30 bg-danger-soft px-3.5 py-2.5 text-sm text-ink">
          <CircleAlert className="mt-0.5 size-4 shrink-0 text-danger" aria-hidden />{problem}
        </p>
      )}
      <Button type="submit" variant="primary" size="lg" className="w-full sm:w-auto" loading={form.formState.isSubmitting}>Send application</Button>
    </form>
  );
}

export function PublicApplySkeleton() {
  return (
    <div className="min-h-dvh bg-canvas">
      <div className="mx-auto grid w-full max-w-3xl gap-4 px-4 pt-10 pb-16 sm:px-6">
        <Skeleton className="h-14 w-72" />
        <Skeleton className="mt-6 h-96" />
      </div>
    </div>
  );
}

/** The online application form for one school, reached from a link the school shares (e.g. /apply/greenfield). */
export function PublicApply() {
  const { schoolCode } = useParams<{ schoolCode: string }>();
  const school = usePublicSchool(schoolCode);
  const [receipt, setReceipt] = React.useState<PublicReceipt | null>(null);
  useSchoolBrand(school.data?.brandColor);

  const missing = school.error instanceof ApiError && school.error.status === 404;
  return (
    <div className="min-h-dvh bg-canvas">
      <div className="mx-auto w-full max-w-3xl px-4 pt-10 pb-16 sm:px-6">
        {school.isPending ? (
          <div className="grid gap-4"><Skeleton className="h-14 w-72" /><Skeleton className="mt-6 h-96" /></div>
        ) : missing || !school.data ? (
          <div className="grid gap-2 py-24 text-center">
            <h1 className="font-display text-2xl text-ink">{missing ? "We couldn't find that school" : "This page couldn't be loaded"}</h1>
            <p className="text-ink-2">{missing ? "Check the link the school gave you." : "Please try again in a moment."}</p>
          </div>
        ) : (
          <>
            <header className="flex items-center gap-4 border-b border-line pb-6">
              <Crest school={school.data} />
              <div className="grid min-w-0 gap-0.5">
                <h1 className="font-display text-2xl text-ink">{school.data.name}</h1>
                <p className="text-sm text-ink-2">{school.data.motto ?? "Admissions"}</p>
              </div>
            </header>

            {receipt ? (
              <div className="grid gap-5 py-10">
                <CircleCheck className="size-8 text-success" aria-hidden />
                <div className="grid gap-1.5">
                  <h2 className="font-display text-2xl text-ink">Application received</h2>
                  <p className="text-ink-2">{receipt.schoolName} has {receipt.applicantName}&apos;s application. The admissions office will contact you about the next step.</p>
                </div>
                <div className="grid w-fit gap-1 rounded-lg border border-line bg-surface px-5 py-4">
                  <span className="text-xs text-ink-2">Your application number</span>
                  <span className="font-mono text-2xl text-ink">{receipt.applicationNumber}</span>
                  <span className="text-xs text-ink-2">Keep it to hand when you call or visit.</span>
                </div>
                <Contact school={school.data} />
              </div>
            ) : !school.data.acceptingApplications ? (
              <div className="grid gap-3 py-10">
                <h2 className="font-display text-xl text-ink">Online applications are closed at the moment</h2>
                <p className="text-ink-2">Please contact the admissions office to ask about a place.</p>
                <Contact school={school.data} />
              </div>
            ) : (
              <div className="grid gap-6 pt-7">
                <div className="grid gap-1.5">
                  <h2 className="font-display text-xl text-ink">Apply for admission</h2>
                  <p className="text-ink-2">It takes about five minutes. One form per child.</p>
                </div>
                <ApplicationForm school={school.data} onReceived={setReceipt} />
              </div>
            )}
          </>
        )}
        <p className="mt-16 text-center text-xs text-ink-3">{productName}</p>
      </div>
    </div>
  );
}
