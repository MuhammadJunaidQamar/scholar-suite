import type { LucideIcon } from "lucide-react";
import {
  Award,
  Banknote,
  Boxes,
  Briefcase,
  Bus,
  CalendarCheck2,
  CalendarDays,
  ChartColumn,
  GraduationCap,
  HandCoins,
  HeartPulse,
  Hotel,
  Landmark,
  LayoutDashboard,
  Library,
  Megaphone,
  NotebookPen,
  Receipt,
  Settings,
  UserPlus,
  UsersRound,
  Utensils,
  CalendarRange,
} from "lucide-react";
import { P } from "@/lib/permissions";

export type NavItem = {
  label: string;
  href: string;
  icon: LucideIcon;
  /** Visible when the user holds any of these. Empty = everyone in the portal. */
  permissions?: string[];
  /** Optional module that must be enabled for the school. */
  module?: string;
  /** Routes that also mark this item active. */
  match?: string[];
  /** Hidden until the module's screens exist — navigation never leads to placeholders. */
  ready?: boolean;
  keywords?: string;
};

export type NavGroup = { id: string; label?: string; items: NavItem[] };

export const adminNavigation: NavGroup[] = [
  {
    id: "home",
    items: [{ label: "Overview", href: "/", icon: LayoutDashboard, ready: true, keywords: "dashboard home" }],
  },
  {
    id: "people",
    label: "People",
    items: [
      { label: "Students", href: "/students", icon: GraduationCap, permissions: [P.Students.View], ready: true, keywords: "pupils learners" },
      { label: "Guardians", href: "/guardians", icon: UsersRound, permissions: [P.Guardians.View], keywords: "parents families" },
      { label: "Admissions", href: "/admissions", icon: UserPlus, permissions: [P.Admissions.View], module: "admissions", keywords: "inquiries applications enrol" },
      { label: "Staff", href: "/staff", icon: Briefcase, permissions: [P.Staff.View], module: "hr", keywords: "teachers employees hr" },
    ],
  },
  {
    id: "teaching",
    label: "Teaching",
    items: [
      { label: "Attendance", href: "/attendance", icon: CalendarCheck2, permissions: [P.Attendance.View, P.Attendance.Manage], keywords: "register absent present" },
      { label: "Timetable", href: "/timetable", icon: CalendarRange, permissions: [P.Timetable.View], keywords: "schedule periods" },
      { label: "Assignments", href: "/assignments", icon: NotebookPen, permissions: [P.Assignments.View], keywords: "homework" },
      { label: "Examinations", href: "/exams", icon: Award, permissions: [P.Exams.View], module: "exams", keywords: "results marks report cards" },
    ],
  },
  {
    id: "finance",
    label: "Finance",
    items: [
      { label: "Fees", href: "/fees", icon: Receipt, permissions: [P.Fees.View], module: "fees", keywords: "challans invoices vouchers" },
      { label: "Collections", href: "/fees/collect", icon: HandCoins, permissions: [P.Fees.Collect], module: "fees", keywords: "payments receipts cash" },
      { label: "Accounts", href: "/finance", icon: Landmark, permissions: [P.Finance.ViewLedger], module: "fees", keywords: "ledger journal bank" },
      { label: "Payroll", href: "/payroll", icon: Banknote, permissions: [P.Payroll.View], module: "payroll", keywords: "salaries payslips" },
    ],
  },
  {
    id: "operations",
    label: "Operations",
    items: [
      { label: "Communication", href: "/communication", icon: Megaphone, permissions: [P.Communication.ViewAnnouncements], keywords: "announcements notices sms" },
      { label: "Calendar", href: "/calendar", icon: CalendarDays, permissions: [P.Events.View, P.Academics.View], keywords: "events holidays" },
      { label: "Library", href: "/library", icon: Library, permissions: [P.Library.View], module: "library", keywords: "books" },
      { label: "Transport", href: "/transport", icon: Bus, permissions: [P.Transport.View], module: "transport", keywords: "routes vehicles" },
      { label: "Inventory", href: "/inventory", icon: Boxes, permissions: [P.Inventory.View], module: "inventory", keywords: "stock assets" },
      { label: "Welfare", href: "/welfare", icon: HeartPulse, permissions: [P.Welfare.ViewDiscipline, P.Welfare.ViewMedical], module: "welfare", keywords: "discipline medical counselling" },
      { label: "Hostel", href: "/hostel", icon: Hotel, permissions: [P.Hostel.View], module: "hostel" },
      { label: "Cafeteria", href: "/cafeteria", icon: Utensils, permissions: [P.Cafeteria.View], module: "cafeteria" },
    ],
  },
  {
    id: "insight",
    label: "Insight",
    items: [{ label: "Reports", href: "/reports", icon: ChartColumn, permissions: [P.Reports.View], keywords: "analytics" }],
  },
];

export const settingsItem: NavItem = {
  label: "Settings",
  href: "/settings",
  icon: Settings,
  permissions: [P.School.View, P.Academics.View, P.Users.View, P.Roles.View],
  ready: true,
  keywords: "configuration school users roles",
};

export function isActive(item: NavItem, pathname: string) {
  if (item.href === "/") return pathname === "/";
  return [item.href, ...(item.match ?? [])].some((h) => pathname === h || pathname.startsWith(`${h}/`));
}

export function visibleNavigation(groups: NavGroup[], can: (...p: string[]) => boolean, hasModule: (m: string) => boolean) {
  return groups
    .map((group) => ({
      ...group,
      items: group.items.filter(
        (item) => item.ready && can(...(item.permissions ?? [])) && (!item.module || hasModule(item.module)),
      ),
    }))
    .filter((group) => group.items.length > 0);
}

/** Longest matching item wins, so /fees/collect highlights Collections rather than Fees. */
export function activeHref(groups: NavGroup[], pathname: string): string | undefined {
  const all = groups.flatMap((g) => g.items).concat(settingsItem);
  return all
    .filter((item) => isActive(item, pathname))
    .sort((a, b) => b.href.length - a.href.length)[0]?.href;
}
