"use client";

import Link from "next/link";
import { ArrowRight, Check } from "lucide-react";
import { cn } from "@/lib/utils";
import { formatDate, formatNumber, formatRelative, getFormatContext, parseDateOnly } from "@/lib/format";
import { useSession } from "@/lib/auth/session";
import { eventTypeLabel, useOrganizationOverview, type OrganizationOverview } from "@/lib/api/org";
import { changedFields, describeGroup, groupAudit, useAudit } from "@/lib/api/audit";
import { P } from "@/lib/permissions";
import { Avatar, Skeleton } from "@/components/ui/display";
import { Page } from "@/components/shell/page";

import { ErrorState } from "@/components/ui/states";

function greeting(today: Date) {
  const hour = Number(new Intl.DateTimeFormat("en-GB", { hour: "numeric", hourCycle: "h23", timeZone: getFormatContext().timeZone }).format(today));
  if (hour < 12) return "Good morning";
  if (hour < 17) return "Good afternoon";
  return "Good evening";
}

function daysUntil(today: string, date: string) {
  return Math.round((parseDateOnly(date).getTime() - parseDateOnly(today).getTime()) / 86_400_000);
}

function Almanac({ overview }: { overview: OrganizationOverview }) {
  const year = overview.currentYear;
  const term = year?.currentTerm;
  const termProgress = term
    ? Math.min(1, Math.max(0, (parseDateOnly(overview.today).getTime() - parseDateOnly(term.startDate).getTime()) /
        (parseDateOnly(term.endDate).getTime() - parseDateOnly(term.startDate).getTime())))
    : null;
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-base text-ink-2">
      <span>{formatDate(overview.today, { weekday: true })}</span>
      {year && <span className="text-ink-3">·</span>}
      {year && <span>{year.name}</span>}
      {term && (
        <>
          <span className="text-ink-3">·</span>
          <span>
            {term.name}, week {overview.currentYear?.weekOfTerm}
          </span>
          {termProgress !== null && (
            <span className="flex items-center gap-2" aria-label={`${Math.round(termProgress * 100)}% of the term elapsed`}>
              <span className="h-1 w-24 overflow-hidden rounded-full bg-line">
                <span className="block h-full rounded-full bg-accent" style={{ width: `${termProgress * 100}%` }} />
              </span>
              <span className="text-sm text-ink-3 tabular">ends {formatDate(term.endDate)}</span>
            </span>
          )}
        </>
      )}
      {year && !term && <span className="text-ink-3">· between terms</span>}
    </div>
  );
}

function Figures({ counts }: { counts: OrganizationOverview["counts"] }) {
  const figures = [
    { label: "Campuses", value: counts.campuses, href: "/settings/campuses" },
    { label: "Grades", value: counts.grades, href: "/settings/grades" },
    { label: "Sections this year", value: counts.sections, href: "/settings/grades" },
    { label: "Subjects", value: counts.subjects, href: "/settings/subjects" },
    { label: "Rooms", value: counts.rooms, href: "/settings/rooms" },
    { label: "People with access", value: counts.members, href: "/settings/users" },
  ];
  return (
    <dl className="grid grid-cols-2 border-y border-line sm:grid-cols-3 lg:grid-cols-6">
      {figures.map((figure, index) => (
        <div key={figure.label} className={cn("grid gap-1 py-5 pr-4", index > 0 && "lg:border-l lg:border-line lg:pl-5")}>
          <dt className="text-sm text-ink-2">{figure.label}</dt>
          <dd className="font-display text-3xl text-ink tabular">{formatNumber(figure.value)}</dd>
        </div>
      ))}
    </dl>
  );
}

function Upcoming({ overview }: { overview: OrganizationOverview }) {
  return (
    <section className="grid content-start gap-3">
      <div className="flex items-baseline justify-between">
        <h2 className="text-md font-semibold">Coming up</h2>
        <span className="text-sm text-ink-3">School calendar</span>
      </div>
      {overview.upcoming.length === 0 ? (
        <p className="border-t border-line py-6 text-sm text-ink-2">Nothing scheduled. Add holidays and events in Settings › Calendar.</p>
      ) : (
        <ol className="border-t border-line">
          {overview.upcoming.map((event) => {
            const days = daysUntil(overview.today, event.startDate);
            const start = parseDateOnly(event.startDate);
            return (
              <li key={event.id} className="grid grid-cols-[3.25rem_1fr_auto] items-center gap-4 border-b border-line py-3">
                <div className="grid justify-items-center rounded-md border border-line bg-surface py-1 leading-none">
                  <span className="text-2xs font-medium tracking-wide text-ink-3 uppercase">
                    {start.toLocaleString("en-GB", { month: "short", timeZone: "UTC" })}
                  </span>
                  <span className="font-display text-xl text-ink tabular">{start.getUTCDate()}</span>
                </div>
                <div className="grid min-w-0 gap-0.5">
                  <span className="truncate text-base font-medium text-ink">{event.title}</span>
                  <span className="text-sm text-ink-2">
                    {eventTypeLabel[event.type]}
                    {event.endDate !== event.startDate && ` · until ${formatDate(event.endDate)}`}
                    {event.isHoliday && " · school closed"}
                  </span>
                </div>
                <span className="text-sm whitespace-nowrap text-ink-3 tabular">
                  {days <= 0 ? "Now" : days === 1 ? "Tomorrow" : `In ${days} days`}
                </span>
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}

function Activity() {
  const audit = useAudit({ pageSize: 60, changesOnly: true });
  return (
    <section className="grid content-start gap-3">
      <div className="flex items-baseline justify-between">
        <h2 className="text-md font-semibold">Recent changes</h2>
        <span className="text-sm text-ink-3">Audit trail</span>
      </div>
      {audit.isPending ? (
        <div className="grid gap-2 border-t border-line pt-3">{[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-10" />)}</div>
      ) : audit.error ? (
        <ErrorState error={audit.error} onRetry={() => void audit.refetch()} />
      ) : audit.data.items.length === 0 ? (
        <p className="border-t border-line py-6 text-sm text-ink-2">No changes recorded yet.</p>
      ) : (
        <ol className="border-t border-line">
          {groupAudit(audit.data.items).slice(0, 7).map((group) => {
            const detail = group.count > 1
              ? group.names.length > 0 ? `${group.names.join(", ")}${group.count > group.names.length ? "…" : ""}` : null
              : changedFields(group.first) ?? group.names[0] ?? null;
            return (
              <li key={group.key} className="flex items-center gap-3 border-b border-line py-2.5">
                <Avatar name={group.first.userName ?? "System"} size={26} />
                <div className="grid min-w-0 flex-1 gap-0.5">
                  <span className="truncate text-base text-ink">{describeGroup(group)}</span>
                  {detail && <span className="truncate text-sm text-ink-2">{detail}</span>}
                </div>
                <span className="text-sm whitespace-nowrap text-ink-3">{formatRelative(group.first.occurredAt)}</span>
              </li>
            );
          })}
        </ol>
      )}
    </section>
  );
}

function setupComplete(overview: OrganizationOverview) {
  return overview.setup.every((s) => s.done);
}

function Setup({ overview }: { overview: OrganizationOverview }) {
  const done = overview.setup.filter((s) => s.done).length;
  if (done === overview.setup.length) return null;
  return (
    <section className="grid content-start gap-3">
      <div className="flex items-baseline justify-between">
        <h2 className="text-md font-semibold">Finish setting up</h2>
        <span className="text-sm text-ink-3 tabular">{done} of {overview.setup.length} done</span>
      </div>
      <ol className="border-t border-line">
        {overview.setup.map((step, index) => (
          <li key={step.key} className="border-b border-line">
            <Link href={step.href} className="group flex items-center gap-3 py-3">
              <span className={cn("grid size-6 shrink-0 place-items-center rounded-full border text-xs tabular",
                step.done ? "border-accent bg-accent text-accent-ink" : "border-line-strong text-ink-2")}>
                {step.done ? <Check className="size-3.5" strokeWidth={3} /> : index + 1}
              </span>
              <span className="grid min-w-0 flex-1 gap-0.5">
                <span className={cn("text-base font-medium", step.done ? "text-ink-2" : "text-ink")}>{step.title}</span>
                <span className="text-sm text-ink-2">{step.description}</span>
              </span>
              <ArrowRight className="size-4 text-ink-3 opacity-0 transition-opacity group-hover:opacity-100" />
            </Link>
          </li>
        ))}
      </ol>
    </section>
  );
}

export default function OverviewPage() {
  const { me, can } = useSession();
  const overview = useOrganizationOverview();
  const showActivity = can(P.Audit.View);

  return (
    <Page>
      <header className="grid gap-2 pt-8 pb-6">
        <h1 className="font-display text-3xl text-ink">
          {greeting(new Date())}, {me.user.firstName}
        </h1>
        {overview.data ? <Almanac overview={overview.data} /> : <Skeleton className="h-5 w-96" />}
      </header>
      {overview.isPending ? (
        <div className="grid gap-6">
          <Skeleton className="h-24" />
          <div className="grid gap-10 lg:grid-cols-2"><Skeleton className="h-72" /><Skeleton className="h-72" /></div>
        </div>
      ) : overview.error ? (
        <ErrorState error={overview.error} onRetry={() => void overview.refetch()} />
      ) : (
        <div className="grid gap-10">
          <Figures counts={overview.data.counts} />
          {(() => {
            const side = !setupComplete(overview.data) ? <Setup overview={overview.data} /> : showActivity ? <Activity /> : null;
            return (
              <div className={cn("grid gap-10", side && "lg:grid-cols-2")}>
                <Upcoming overview={overview.data} />
                {side}
              </div>
            );
          })()}
        </div>
      )}
    </Page>
  );
}
