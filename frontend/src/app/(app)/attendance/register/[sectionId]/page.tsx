"use client";

import * as React from "react";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { useBellSchedules, useRegisterSheet } from "@/lib/api/attendance";
import { todayIso } from "@/lib/format";
import { Page, PageHeader } from "@/components/shell/page";
import { Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { Segmented } from "@/components/ui/toggles";
import { DayStepper } from "@/components/attendance/day-stepper";
import { RegisterSheetView } from "@/components/attendance/register-sheet";

export default function RegisterPage() {
  const { sectionId } = useParams<{ sectionId: string }>();
  const search = useSearchParams();
  const router = useRouter();
  const date = search.get("date") ?? todayIso();
  const periodParam = search.get("period");
  const period = periodParam ? Number(periodParam) : null;
  const subjectId = search.get("subject");
  const sheet = useRegisterSheet({ sectionId, date, period, subjectId });
  const bells = useBellSchedules(sheet.data?.mode === "PeriodWise");

  const go = (next: { date?: string; period?: number | null }) => {
    const params = new URLSearchParams();
    params.set("date", next.date ?? date);
    const p = next.period === undefined ? period : next.period;
    if (p) params.set("period", String(p));
    if (subjectId && p) params.set("subject", subjectId);
    router.replace(`/attendance/register/${sectionId}?${params.toString()}`);
  };

  const s = sheet.data;
  const periods = (bells.data?.find((b) => b.campusId === s?.campusId)?.periods ?? []).filter((p) => p.number !== null);
  const periodOptions = [
    { value: "day", label: "Roll call" },
    ...(periods.length > 0 ? periods.map((p) => ({ value: String(p.number), label: `P${p.number}`, title: `${p.name} · ${p.startTime.slice(0, 5)}–${p.endTime.slice(0, 5)}` }))
      : Array.from({ length: 8 }, (_, i) => ({ value: String(i + 1), label: `P${i + 1}`, title: `Period ${i + 1}` }))),
  ];

  const description = s
    ? [
        s.period ? (s.periodLabel ?? `Period ${s.period}`) : "Daily roll call",
        s.subjectName,
        s.campusName,
        s.classTeacherName && `Class teacher ${s.classTeacherName}`,
      ].filter(Boolean).join(" · ")
    : undefined;

  return (
    <Page width="narrow" className="max-w-5xl">
      <PageHeader
        breadcrumbs={[{ label: "Attendance", href: "/attendance" }, { label: s?.className ?? "Register" }]}
        title={s?.className ?? <Skeleton className="h-8 w-48" />}
        description={description}
        actions={
          <>
            {s?.mode === "PeriodWise" && (
              <Segmented size="sm" aria-label="Register" value={period ? String(period) : "day"}
                onChange={(v) => go({ period: v === "day" ? null : Number(v) })} options={periodOptions} />
            )}
            <DayStepper value={date} onChange={(d) => go({ date: d })} />
          </>
        }
      />
      {sheet.isPending ? (
        <div className="grid gap-3">
          <Skeleton className="h-11" />
          <Skeleton className="h-96" />
        </div>
      ) : sheet.error ? (
        <ErrorState error={sheet.error} onRetry={() => void sheet.refetch()} />
      ) : (
        <RegisterSheetView sheet={sheet.data} />
      )}
    </Page>
  );
}
