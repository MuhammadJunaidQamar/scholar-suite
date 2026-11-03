import {
  BadgeCheck,
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
      { label: "School profile", href: "/settings/school", icon: School, permissions: [P.School.View], description: "Name, crest, accent colour, contact details and locale." },
      { label: "Campuses", href: "/settings/campuses", icon: Building2, permissions: [P.School.View], description: "Branches and their addresses." },
      { label: "Modules", href: "/settings/modules", icon: ToggleRight, permissions: [P.School.View], description: "Switch optional areas such as transport or hostel on and off." },
      { label: "Numbering", href: "/settings/numbering", icon: Hash, permissions: [P.School.View], description: "Formats for admission numbers, challans and receipts." },
    ],
  },
  {
    id: "academic",
    label: "Academic structure",
    items: [
      { label: "Academic years", href: "/settings/academic-years", icon: CalendarDays, permissions: [P.Academics.View], description: "Years, terms and which one is current." },
      { label: "Grades & sections", href: "/settings/grades", icon: GraduationCap, permissions: [P.Academics.View], description: "Levels of study, promotion order and this year's sections." },
      { label: "Subjects & curriculum", href: "/settings/subjects", icon: LibraryBig, permissions: [P.Academics.View], description: "Subjects, streams and what each grade studies." },
      { label: "Rooms", href: "/settings/rooms", icon: DoorOpen, permissions: [P.Academics.View], description: "Classrooms, labs and bookable spaces." },
      { label: "Calendar", href: "/settings/calendar", icon: Layers, permissions: [P.Academics.View], description: "Holidays, examinations and school events." },
      { label: "Grading", href: "/settings/grading", icon: BadgeCheck, permissions: [P.Academics.View], description: "Grading scales and pass marks." },
      { label: "Departments & designations", href: "/settings/departments", icon: Shapes, permissions: [P.School.View], description: "How staff are organised." },
    ],
  },
  {
    id: "access",
    label: "Access",
    items: [
      { label: "Users", href: "/settings/users", icon: Users, permissions: [P.Users.View], ready: true, description: "Who can sign in, and with which roles." },
      { label: "Roles & permissions", href: "/settings/roles", icon: ShieldCheck, permissions: [P.Roles.View], ready: true, description: "What each role may see and do." },
      { label: "Audit log", href: "/settings/audit", icon: History, permissions: [P.Audit.View], description: "A record of every change." },
    ],
  },
];
