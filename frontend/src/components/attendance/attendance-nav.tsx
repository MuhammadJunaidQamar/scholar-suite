"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { cn } from "@/lib/utils";

const tabs = [
  { href: "/attendance", label: "Today", permissions: [P.Attendance.View, P.Attendance.Manage], exact: true },
  { href: "/attendance/leave", label: "Student leave", permissions: [P.Attendance.ManageLeave, P.Attendance.View] },
  { href: "/attendance/corrections", label: "Corrections", permissions: [P.Attendance.Approve, P.Attendance.Record, P.Attendance.Manage] },
  { href: "/attendance/reports", label: "Reports", permissions: [P.Attendance.View, P.Attendance.Manage] },
  { href: "/attendance/staff", label: "Staff register", permissions: [P.Staff.ViewAttendance, P.Staff.RecordAttendance] },
  { href: "/attendance/staff-leave", label: "Staff leave", permissions: [P.Hr.ViewLeave, P.Hr.ApproveLeave] },
];

/** The attendance workspace's page-level tabs, filtered to what the user may open. */
export function AttendanceNav({ className }: { className?: string }) {
  const pathname = usePathname();
  const { can } = useSession();
  const visible = tabs.filter((t) => can(...t.permissions));
  return (
    <nav aria-label="Attendance" className={cn("flex gap-5 overflow-x-auto border-b border-line [scrollbar-width:none]", className)}>
      {visible.map((t) => {
        const active = t.exact ? pathname === t.href || pathname.startsWith("/attendance/register") || pathname.startsWith("/attendance/absentees") : pathname.startsWith(t.href);
        return (
          <Link key={t.href} href={t.href} aria-current={active ? "page" : undefined}
            className={cn("relative -mb-px inline-flex h-9 shrink-0 items-center border-b-2 border-transparent text-sm font-medium whitespace-nowrap text-ink-2 hover:text-ink",
              active && "border-ink text-ink")}>
            {t.label}
          </Link>
        );
      })}
    </nav>
  );
}
