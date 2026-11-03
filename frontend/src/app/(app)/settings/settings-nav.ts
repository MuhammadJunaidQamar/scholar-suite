import {
  BadgeCheck,
  BellRing,
  CalendarClock,
  UsersRound,
  Scale,
  Building2,
  CalendarDays,
  DoorOpen,
  GraduationCap,
  Hash,
  History,
  Layers,
  LibraryBig,
  School,
  Shapes,
  ShieldCheck,
  ToggleRight,
  Users,
} from "lucide-react";
import { P } from "@/lib/permissions";
import type { NavItem } from "@/components/shell/nav";

export type SettingsItem = NavItem & { description: string };
export type SettingsGroup = { id: string; label: string; items: SettingsItem[] };

export const settingsNavigation: SettingsGroup[] = [
  {
    id: "school",
    label: "School",
    items: [
      { label: "School profile", href: "/settings/school", icon: School, permissions: [P.School.View], ready: true, description: "Name, crest, accent colour, contact details and locale." },
      { label: "Campuses", href: "/settings/campuses", icon: Building2, permissions: [P.School.View], ready: true, description: "Branches and their addresses." },
      { label: "Policies", href: "/settings/policies", icon: Scale, permissions: [P.School.View], ready: true, description: "Working days and the rules for attendance, fees, admissions and results." },
      { label: "Modules", href: "/settings/modules", icon: ToggleRight, permissions: [P.School.View], ready: true, description: "Switch optional areas such as transport or hostel on and off." },
      { label: "Numbering", href: "/settings/numbering", icon: Hash, permissions: [P.School.View], ready: true, description: "Formats for admission numbers, challans and receipts." },
    ],
  },
  {
    id: "academic",
    label: "Academic structure",
    items: [
      { label: "Academic years", href: "/settings/academic-years", icon: CalendarDays, permissions: [P.Academics.View], ready: true, description: "Years, terms and which one is current." },
      { label: "Grades & sections", href: "/settings/grades", icon: GraduationCap, permissions: [P.Academics.View], ready: true, description: "Levels of study, promotion order and this year's sections." },
      { label: "Subjects & curriculum", href: "/settings/subjects", icon: LibraryBig, permissions: [P.Academics.View], ready: true, description: "Subjects, streams and what each grade studies." },
      { label: "Rooms", href: "/settings/rooms", icon: DoorOpen, permissions: [P.Academics.View], ready: true, description: "Classrooms, labs and bookable spaces." },
      { label: "Calendar", href: "/settings/calendar", icon: Layers, permissions: [P.Academics.View], ready: true, description: "Holidays, examinations and school events." },
      { label: "Grading", href: "/settings/grading", icon: BadgeCheck, permissions: [P.Academics.View], ready: true, description: "Grading scales and pass marks." },
      { label: "Departments & designations", href: "/settings/departments", icon: Shapes, permissions: [P.School.View], ready: true, description: "How staff are organised." },
      { label: "Teaching assignments", href: "/settings/teaching", icon: UsersRound, permissions: [P.Academics.View, P.Staff.View], ready: true, description: "Class teachers and subject teachers for every section." },
      { label: "Bell times", href: "/settings/bell-times", icon: BellRing, permissions: [P.Academics.View, P.Timetable.View, P.Attendance.View], ready: true, description: "Each campus's periods and breaks." },
      { label: "Leave types", href: "/settings/leave-types", icon: CalendarClock, permissions: [P.Hr.Configure, P.Hr.ViewLeave, P.Hr.ApproveLeave], ready: true, description: "Kinds of staff leave and yearly allowances." },
    ],
  },
  {
    id: "access",
    label: "Access",
    items: [
      { label: "Users", href: "/settings/users", icon: Users, permissions: [P.Users.View], ready: true, description: "Who can sign in, and with which roles." },
      { label: "Roles & permissions", href: "/settings/roles", icon: ShieldCheck, permissions: [P.Roles.View], ready: true, description: "What each role may see and do." },
      { label: "Audit log", href: "/settings/audit", icon: History, permissions: [P.Audit.View], ready: true, description: "A record of every change." },
    ],
  },
];
