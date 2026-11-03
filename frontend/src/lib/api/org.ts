import { useQuery } from "@tanstack/react-query";
import { api } from "./client";

export type CalendarEventType = "Holiday" | "Event" | "Exam" | "ParentTeacherMeeting" | "Sports" | "SpecialDay" | "Meeting" | "TermDates";

export type OrganizationOverview = {
  today: string;
  currentYear: {
    id: string;
    name: string;
    startDate: string;
    endDate: string;
    currentTerm: { id: string; name: string; sequence: number; startDate: string; endDate: string } | null;
    weekOfTerm: number | null;
  } | null;
  counts: { campuses: number; grades: number; sections: number; subjects: number; rooms: number; members: number };
  upcoming: { id: string; title: string; type: CalendarEventType; startDate: string; endDate: string; isHoliday: boolean }[];
  setup: { key: string; title: string; description: string; done: boolean; href: string }[];
};

export const orgKeys = { overview: ["org", "overview"] as const };

export function useOrganizationOverview() {
  return useQuery({ queryKey: orgKeys.overview, queryFn: () => api.get<OrganizationOverview>("/org/overview") });
}

export const eventTypeLabel: Record<CalendarEventType, string> = {
  Holiday: "Holiday",
  Event: "Event",
  Exam: "Examinations",
  ParentTeacherMeeting: "Parent–teacher meeting",
  Sports: "Sports",
  SpecialDay: "Special day",
  Meeting: "Meeting",
  TermDates: "Term dates",
};
