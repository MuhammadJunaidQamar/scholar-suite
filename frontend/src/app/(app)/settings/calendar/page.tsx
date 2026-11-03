"use client";

import * as React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { CalendarDays, Plus } from "lucide-react";
import { cn } from "@/lib/utils";
import { formatDate, parseDateOnly } from "@/lib/format";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useCampuses } from "@/lib/api/access";
import {
  eventTypeLabel, useAcademicYears, useCalendar, useDeleteCalendarEvent, useSaveCalendarEvent, type CalendarEvent, type CalendarEventType, type EventAudience,
} from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Input, NativeSelect, Textarea } from "@/components/ui/input";
import { Field, FieldRow } from "@/components/ui/field";
import { Badge, Skeleton, StatusBadge, type Tone } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { Switch } from "@/components/ui/toggles";
import { toast } from "@/components/ui/toaster";
import { FormDialog, nullIfEmpty } from "@/components/forms/form-dialog";

const types = Object.keys(eventTypeLabel) as CalendarEventType[];
const audiences: Record<EventAudience, string> = { Everyone: "Everyone", Staff: "Staff only", Students: "Students", Guardians: "Families" };
const tone: Record<CalendarEventType, Tone> = {
  Holiday: "danger", Exam: "warning", ParentTeacherMeeting: "info", Sports: "success", Event: "accent", SpecialDay: "accent", Meeting: "neutral", TermDates: "neutral",
};

const schema = z.object({
  title: z.string().trim().min(1, "Enter a title.").max(200),
  type: z.enum(["Holiday", "Event", "Exam", "ParentTeacherMeeting", "Sports", "SpecialDay", "Meeting", "TermDates"]),
  startDate: z.string().min(1, "Choose a date."),
  endDate: z.string().min(1, "Choose a date."),
  startTime: z.string(),
  endTime: z.string(),
  isHoliday: z.boolean(),
  audience: z.enum(["Everyone", "Staff", "Students", "Guardians"]),
  campusId: z.string(),
  location: z.string().max(200),
  description: z.string().max(4000),
}).refine((v) => v.endDate >= v.startDate, { message: "Ends before it starts.", path: ["endDate"] });
type Values = z.infer<typeof schema>;

function EventDialog({ event, open, onOpenChange }: { event: CalendarEvent | null; open: boolean; onOpenChange: (o: boolean) => void }) {
  const save = useSaveCalendarEvent();
  const remove = useDeleteCalendarEvent();
  const campuses = useCampuses();
  const form = useForm<Values>({ resolver: zodResolver(schema) });
  const type = form.watch("type");
  const e = form.formState.errors;
  return (
    <FormDialog open={open} onOpenChange={onOpenChange} form={form}
      defaults={{ title: event?.title ?? "", type: event?.type ?? "Event", startDate: event?.startDate ?? "", endDate: event?.endDate ?? "",
        startTime: event?.startTime?.slice(0, 5) ?? "", endTime: event?.endTime?.slice(0, 5) ?? "", isHoliday: event?.isHoliday ?? false,
        audience: event?.audience ?? "Everyone", campusId: event?.campusId ?? "", location: event?.location ?? "", description: event?.description ?? "" }}
      title={event ? "Edit event" : "New event"} description="Holidays close the school: no attendance is expected and the days don't count against students."
      submitLabel={event ? "Save event" : "Add to calendar"} successMessage={event ? "Event saved" : "Added to the calendar"}
      footerStart={event && (
        <Button type="button" variant="danger-ghost" onClick={async () => {
          try { await remove.mutateAsync(event.id); toast.success("Event removed"); onOpenChange(false); } catch (err) { toast.error(err); }
        }}>Remove</Button>
      )}
      onSubmit={(v) => save.mutateAsync({ id: event?.id, body: {
        ...v, startTime: v.startTime || null, endTime: v.endTime || null, campusId: v.campusId || null,
        location: nullIfEmpty(v.location), description: nullIfEmpty(v.description), isHoliday: v.type === "Holiday" ? true : v.isHoliday,
      } })}>
      <Field label="Title" htmlFor="ev-title" error={e.title?.message}><Input autoFocus placeholder="e.g. Annual Sports Day" {...form.register("title")} /></Field>
      <FieldRow>
        <Field label="Type" htmlFor="ev-type"><NativeSelect {...form.register("type")}>{types.map((t) => <option key={t} value={t}>{eventTypeLabel[t]}</option>)}</NativeSelect></Field>
        <Field label="For" htmlFor="ev-audience"><NativeSelect {...form.register("audience")}>{Object.entries(audiences).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</NativeSelect></Field>
      </FieldRow>
      <FieldRow cols={4}>
        <Field label="From" htmlFor="ev-start" error={e.startDate?.message}><Input type="date" {...form.register("startDate")} /></Field>
        <Field label="To" htmlFor="ev-end" error={e.endDate?.message}><Input type="date" {...form.register("endDate")} /></Field>
        <Field label="Starts" htmlFor="ev-st" optional><Input type="time" {...form.register("startTime")} /></Field>
        <Field label="Ends" htmlFor="ev-et" optional><Input type="time" {...form.register("endTime")} /></Field>
      </FieldRow>
      <FieldRow>
        <Field label="Campus" htmlFor="ev-campus">
          <NativeSelect {...form.register("campusId")}><option value="">All campuses</option>{campuses.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</NativeSelect>
        </Field>
        <Field label="Location" htmlFor="ev-loc" optional><Input {...form.register("location")} /></Field>
      </FieldRow>
      <Field label="Details" htmlFor="ev-desc" optional><Textarea rows={3} {...form.register("description")} /></Field>
      {type !== "Holiday" && (
        <Field inline label="School is closed" hint="Mark if no classes are held (e.g. a local holiday on an event day)." htmlFor="ev-closed">
          <Switch checked={form.watch("isHoliday")} onCheckedChange={(v) => form.setValue("isHoliday", v, { shouldDirty: true })} />
        </Field>
      )}
    </FormDialog>
  );
}

export default function CalendarSettingsPage() {
  const { can } = useSession();
  const editable = can(P.Academics.Manage, P.Events.Manage);
  const years = useAcademicYears();
  const [yearId, setYearId] = React.useState("");
  const year = years.data?.find((y) => y.id === yearId) ?? years.data?.find((y) => y.isCurrent) ?? years.data?.[0];
  const events = useCalendar({ from: year?.startDate, to: year?.endDate });
  const [dialog, setDialog] = React.useState<{ open: boolean; event: CalendarEvent | null }>({ open: false, event: null });

  const months = React.useMemo(() => {
    const groups = new Map<string, CalendarEvent[]>();
    for (const event of events.data ?? []) {
      const key = event.startDate.slice(0, 7);
      groups.set(key, [...(groups.get(key) ?? []), event]);
    }
    return [...groups.entries()];
  }, [events.data]);
  const holidays = (events.data ?? []).filter((e) => e.isHoliday)
    .reduce((n, e) => n + Math.round((parseDateOnly(e.endDate).getTime() - parseDateOnly(e.startDate).getTime()) / 86_400_000) + 1, 0);

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Calendar" }]} title="Calendar"
        description="Holidays, examinations and school events for the year."
        actions={editable && <Button variant="primary" onClick={() => setDialog({ open: true, event: null })}><Plus /> New event</Button>}
        meta={year && (
          <>
            <NativeSelect aria-label="Academic year" className="h-7 w-auto text-sm" value={year.id} onChange={(e) => setYearId(e.target.value)}>
              {years.data?.map((y) => <option key={y.id} value={y.id}>{y.name}</option>)}
            </NativeSelect>
            <span className="tabular">{events.data?.length ?? 0} entries · {holidays} days closed</span>
          </>
        )} />
      {events.isPending || years.isPending ? <Skeleton className="h-96" /> : events.error ? <ErrorState error={events.error} onRetry={() => void events.refetch()} /> :
        months.length === 0 ? <EmptyState icon={<CalendarDays />} title="Nothing on the calendar" description="Add public holidays, vacations and examination weeks first." /> : (
          <div className="grid gap-8">
            {months.map(([month, list]) => (
              <section key={month} className="grid gap-2 md:grid-cols-[9rem_1fr]">
                <h2 className="font-display text-lg text-ink">{parseDateOnly(`${month}-01`).toLocaleString("en-GB", { month: "long", year: "numeric", timeZone: "UTC" })}</h2>
                <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
                  {list.map((event) => {
                    const multi = event.endDate !== event.startDate;
                    return (
                      <li key={event.id} onClick={editable ? () => setDialog({ open: true, event }) : undefined}
                        className={cn("grid grid-cols-[6.5rem_1fr_auto] items-center gap-3 px-4 py-2.5", editable && "cursor-pointer hover:bg-sunken/60")}>
                        <span className="text-sm text-ink-2 tabular">
                          {formatDate(event.startDate).replace(/ \d{4}$/, "")}{multi && ` – ${formatDate(event.endDate).replace(/ \d{4}$/, "")}`}
                        </span>
                        <span className="grid min-w-0 gap-0.5">
                          <span className="flex items-center gap-2 truncate text-base font-medium">
                            {event.title}{event.campusName && <Badge>{event.campusName}</Badge>}
                          </span>
                          {event.description && <span className="truncate text-sm text-ink-2">{event.description}</span>}
                        </span>
                        <StatusBadge tone={tone[event.type]}>{event.isHoliday && event.type !== "Holiday" ? "Closed" : eventTypeLabel[event.type]}</StatusBadge>
                      </li>
                    );
                  })}
                </ul>
              </section>
            ))}
          </div>
        )}
      <EventDialog open={dialog.open} event={dialog.event} onOpenChange={(o) => setDialog((d) => ({ ...d, open: o }))} />
    </Page>
  );
}
