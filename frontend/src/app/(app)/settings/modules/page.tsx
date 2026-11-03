"use client";

import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { useModules, useUpdateModules } from "@/lib/api/org";
import { Page, PageHeader } from "@/components/shell/page";
import { Badge, Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { Switch } from "@/components/ui/toggles";
import { toast } from "@/components/ui/toaster";

const descriptions: Record<string, { title: string; text: string }> = {
  academics: { title: "Academics", text: "Classes, timetables, homework and lesson plans." },
  attendance: { title: "Attendance", text: "Daily or period registers, leave and absence alerts." },
  communication: { title: "Communication", text: "Announcements, messages and notifications." },
  admissions: { title: "Admissions", text: "Inquiries, applications, tests, offers and enrolment." },
  fees: { title: "Fees & accounts", text: "Fee structures, challans, collections, concessions and the ledger." },
  exams: { title: "Examinations", text: "Exam sessions, marks entry, results and report cards." },
  hr: { title: "Staff & HR", text: "Staff records, leave, recruitment and performance." },
  payroll: { title: "Payroll", text: "Salary structures, monthly payroll and payslips. Needs Staff & HR." },
  library: { title: "Library", text: "Catalogue, circulation, reservations and fines." },
  transport: { title: "Transport", text: "Vehicles, routes, stops and student allocations." },
  inventory: { title: "Inventory & assets", text: "Stock, purchases, issues and the asset register." },
  welfare: { title: "Welfare & discipline", text: "Restricted incident, counselling and clinic records." },
  events: { title: "Events & facilities", text: "Events, parent–teacher meetings and room bookings." },
  hostel: { title: "Hostel", text: "Boarding houses, rooms, beds, residents and attendance." },
  cafeteria: { title: "Cafeteria", text: "Menus, meal plans, subscriptions and orders." },
};

export default function ModulesPage() {
  const { can } = useSession();
  const editable = can(P.School.Manage);
  const modules = useModules();
  const update = useUpdateModules();

  const toggle = async (key: string, on: boolean) => {
    const enabled = new Set(modules.data!.filter((m) => m.enabled && !m.core).map((m) => m.key));
    if (on) enabled.add(key);
    else enabled.delete(key);
    if (on && key === "payroll") enabled.add("hr");
    if (!on && key === "hr") enabled.delete("payroll");
    try {
      await update.mutateAsync([...enabled]);
      toast.success(`${descriptions[key]?.title ?? key} ${on ? "switched on" : "switched off"}`,
        on ? "It now appears in navigation for people with access." : "Its data is kept and returns if you switch it back on.");
    } catch (error) {
      toast.error(error, "The module couldn't be changed");
    }
  };

  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Modules" }]} title="Modules"
        description="Switch off what the school doesn't use to keep everyone's navigation short. Nothing is deleted." />
      {modules.isPending ? <Skeleton className="h-96" /> : modules.error ? <ErrorState error={modules.error} onRetry={() => void modules.refetch()} /> : (
        <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
          {modules.data.map((m) => (
            <li key={m.key} className="flex items-center gap-4 px-4 py-3.5">
              <div className="grid flex-1 gap-0.5">
                <span className="flex items-center gap-2 text-base font-medium">
                  {descriptions[m.key]?.title ?? m.key}
                  {m.core && <Badge>Always on</Badge>}
                </span>
                <span className="text-sm text-ink-2">{descriptions[m.key]?.text}</span>
              </div>
              <Switch aria-label={descriptions[m.key]?.title ?? m.key} checked={m.enabled} disabled={m.core || !editable || update.isPending}
                onCheckedChange={(v) => void toggle(m.key, v)} />
            </li>
          ))}
        </ul>
      )}
    </Page>
  );
}
