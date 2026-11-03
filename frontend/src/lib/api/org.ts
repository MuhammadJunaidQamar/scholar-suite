import { useMutation, useQuery, useQueryClient, type QueryKey } from "@tanstack/react-query";
import { api, upload, type Paged } from "./client";

// ───────── Types (mirror Scholar.Application.Organization DTOs) ─────────

export type CalendarEventType = "Holiday" | "Event" | "Exam" | "ParentTeacherMeeting" | "Sports" | "SpecialDay" | "Meeting" | "TermDates";
export type EventAudience = "Everyone" | "Staff" | "Students" | "Guardians";
export type SchoolStage = "EarlyYears" | "Primary" | "Middle" | "Secondary" | "HigherSecondary";
export type RoomType = "Classroom" | "Laboratory" | "ComputerLab" | "Library" | "Hall" | "Sports" | "Office" | "Staffroom" | "Other";
export type SubjectType = "Core" | "Elective" | "Language" | "CoCurricular";
export type DesignationCategory = "Teaching" | "NonTeaching" | "Administrative" | "Support";
export type ResetPolicy = "Never" | "Yearly" | "Monthly";
export type DayOfWeek = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

export type Address = {
  line1: string | null;
  line2: string | null;
  city: string | null;
  province: string | null;
  postalCode: string | null;
  countryCode: string | null;
};

export type SchoolSettings = {
  workingDays: DayOfWeek[];
  attendance: { mode: "Daily" | "PeriodWise"; lockAfterHours: number; notifyGuardiansOnAbsence: boolean; notifyGuardiansOnLate: boolean; excusedCountsAsPresent: boolean; minimumPercentage: number };
  finance: { defaultDueDay: number; lateFeeMode: "None" | "FixedOnce" | "PerDay"; lateFeeAmount: number; lateFeeGraceDays: number; lateFeeCap: number | null; allowPartialPayments: boolean; concessionsRequireApproval: boolean; refundsRequireApproval: boolean; challanCopies: number; challanInstructions: string | null };
  admissions: { onlineApplicationsEnabled: boolean; requireEntranceTest: boolean; requireInterview: boolean; requireAdmissionFeeBeforeEnrollment: boolean; offerValidityDays: number; requiredDocuments: string[] };
  academics: { passPercentage: number; showPositions: boolean; marksDecimals: number };
};

export type SchoolProfile = {
  id: string;
  code: string;
  name: string;
  shortName: string | null;
  legalName: string | null;
  motto: string | null;
  registrationNumber: string | null;
  email: string | null;
  phone: string | null;
  website: string | null;
  address: Address;
  timezone: string;
  currencyCode: string;
  locale: string;
  brandColor: string | null;
  logoFileId: string | null;
  enabledModules: string[];
  settings: SchoolSettings;
};

export type ModuleState = { key: string; enabled: boolean; core: boolean };
export type Sequence = { key: string; prefix: string; pattern: string; resetPolicy: ResetPolicy; nextValue: number; preview: string };

export type Term = { id: string; name: string; sequence: number; startDate: string; endDate: string };
export type AcademicYear = {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  status: "Planned" | "Active" | "Closed";
  isCurrent: boolean;
  terms: Term[];
  sectionCount: number;
};

export type CampusDetail = {
  id: string;
  code: string;
  name: string;
  address: Address;
  phone: string | null;
  email: string | null;
  isMain: boolean;
  isActive: boolean;
  sectionCount: number;
  roomCount: number;
};

export type Grade = {
  id: string;
  name: string;
  code: string;
  levelOrder: number;
  stage: SchoolStage;
  nextGradeId: string | null;
  nextGradeName: string | null;
  isActive: boolean;
  sectionCount: number;
};

export type Section = {
  id: string;
  academicYearId: string;
  campusId: string;
  campusName: string;
  gradeId: string;
  gradeName: string;
  gradeOrder: number;
  name: string;
  displayName: string;
  capacity: number;
  roomId: string | null;
  roomName: string | null;
  subjectGroupId: string | null;
  isActive: boolean;
  /** Students currently placed in the section. */
  enrolled: number;
};

export type Subject = {
  id: string;
  code: string;
  name: string;
  shortName: string | null;
  type: SubjectType;
  subjectGroupId: string | null;
  subjectGroupName: string | null;
  color: string | null;
  isActive: boolean;
  gradeCount: number;
};

export type SubjectGroup = { id: string; name: string; description: string | null; subjectCount: number };

export type CurriculumEntry = {
  id: string;
  gradeId: string;
  subjectId: string;
  subjectName: string;
  subjectCode: string;
  subjectColor: string | null;
  isMandatory: boolean;
  periodsPerWeek: number;
  subjectGroupId: string | null;
};

export type Room = {
  id: string;
  campusId: string;
  campusName: string;
  code: string;
  name: string;
  type: RoomType;
  building: string | null;
  floor: string | null;
  capacity: number;
  isBookable: boolean;
  isActive: boolean;
};

export type CalendarEvent = {
  id: string;
  title: string;
  description: string | null;
  type: CalendarEventType;
  startDate: string;
  endDate: string;
  startTime: string | null;
  endTime: string | null;
  isHoliday: boolean;
  audience: EventAudience;
  campusId: string | null;
  campusName: string | null;
  location: string | null;
};

export type GradingBand = { minPercent: number; maxPercent: number; grade: string; gradePoint: number | null; remark: string | null; isPassing: boolean };
export type GradingScale = { id: string; name: string; type: "Percentage" | "GradePoint"; isDefault: boolean; bands: GradingBand[] };
export type Department = { id: string; code: string; name: string; description: string | null };
export type Designation = { id: string; name: string; category: DesignationCategory };
export type CopyResult = { created: number; skipped: number };

export type OrganizationOverview = {
  today: string;
  currentYear: (Omit<AcademicYear, "terms" | "sectionCount" | "status" | "isCurrent"> & { currentTerm: Term | null; weekOfTerm: number | null }) | null;
  counts: { campuses: number; grades: number; sections: number; subjects: number; rooms: number; members: number };
  upcoming: { id: string; title: string; type: CalendarEventType; startDate: string; endDate: string; isHoliday: boolean }[];
  setup: { key: string; title: string; description: string; done: boolean; href: string }[];
};

// ───────── Labels ─────────

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

export const stageLabel: Record<SchoolStage, string> = {
  EarlyYears: "Early years",
  Primary: "Primary",
  Middle: "Middle",
  Secondary: "Secondary",
  HigherSecondary: "Higher secondary",
};

export const roomTypeLabel: Record<RoomType, string> = {
  Classroom: "Classroom",
  Laboratory: "Laboratory",
  ComputerLab: "Computer lab",
  Library: "Library",
  Hall: "Hall",
  Sports: "Sports",
  Office: "Office",
  Staffroom: "Staffroom",
  Other: "Other",
};

export const subjectTypeLabel: Record<SubjectType, string> = { Core: "Core", Elective: "Elective", Language: "Language", CoCurricular: "Co-curricular" };

export const designationCategoryLabel: Record<DesignationCategory, string> = {
  Teaching: "Teaching",
  NonTeaching: "Non-teaching",
  Administrative: "Administrative",
  Support: "Support",
};

// ───────── Generic hooks ─────────

export const orgKeys = {
  overview: ["org", "overview"] as const,
  school: ["org", "school"] as const,
  modules: ["org", "modules"] as const,
  sequences: ["org", "sequences"] as const,
  campuses: ["org", "campus-management"] as const,
  years: ["org", "academic-years"] as const,
  grades: ["org", "grades"] as const,
  sections: (query: object) => ["org", "sections", query] as const,
  subjects: ["org", "subjects"] as const,
  groups: ["org", "subject-groups"] as const,
  curriculum: (query: object) => ["org", "curriculum", query] as const,
  rooms: (query: object) => ["org", "rooms", query] as const,
  calendar: (query: object) => ["org", "calendar", query] as const,
  grading: ["org", "grading-scales"] as const,
  departments: ["org", "departments"] as const,
  designations: ["org", "designations"] as const,
};

function useList<T>(key: QueryKey, path: string, query?: Record<string, unknown>, enabled = true) {
  return useQuery({
    queryKey: key,
    queryFn: ({ signal }) => api.get<T>(path, query as Record<string, string> | undefined, signal),
    enabled,
  });
}

/** Create/update against `base` (POST) or `base/{id}` (PUT); invalidates the given keys (prefix match). */
function useSaveMutation<TBody, TResult>(base: string, invalidate: QueryKey[]) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id?: string | null; body: TBody }) =>
      id ? api.put<TResult>(`${base}/${id}`, body) : api.post<TResult>(base, body),
    onSuccess: () => invalidate.forEach((key) => void queryClient.invalidateQueries({ queryKey: key })),
  });
}

function useDeleteMutation(base: string, invalidate: QueryKey[]) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`${base}/${id}`),
    onSuccess: () => invalidate.forEach((key) => void queryClient.invalidateQueries({ queryKey: key })),
  });
}

function useActionMutation<TResult = unknown>(build: (id: string) => string, invalidate: QueryKey[]) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body?: unknown }) => api.post<TResult>(build(id), body ?? {}),
    onSuccess: () => invalidate.forEach((key) => void queryClient.invalidateQueries({ queryKey: key })),
  });
}

const touchesOverview: QueryKey[] = [orgKeys.overview, ["auth", "me"]];

// ───────── Resources ─────────

export const useOrganizationOverview = () => useList<OrganizationOverview>(orgKeys.overview, "/org/overview");

export const useSchoolProfile = () => useList<SchoolProfile>(orgKeys.school, "/org/school");
export function useUpdateSchoolProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: Omit<SchoolProfile, "id" | "code" | "logoFileId" | "enabledModules" | "settings">) =>
      api.put<SchoolProfile>("/org/school/profile", body),
    onSuccess: (profile) => {
      queryClient.setQueryData(orgKeys.school, profile);
      touchesOverview.forEach((key) => void queryClient.invalidateQueries({ queryKey: key }));
    },
  });
}
/** Uploads (or replaces) the school crest; the sidebar and public pages pick it up. */
export function useUploadSchoolLogo() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append("file", file);
      return upload<{ id: string }>("/org/school/logo", form);
    },
    onSuccess: () => [orgKeys.school, ["auth", "me"]].forEach((key) => void queryClient.invalidateQueries({ queryKey: key })),
  });
}

export function useRemoveSchoolLogo() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (fileId: string) => api.delete(`/files/${fileId}`),
    onSuccess: () => [orgKeys.school, ["auth", "me"]].forEach((key) => void queryClient.invalidateQueries({ queryKey: key })),
  });
}

export function useUpdatePolicies() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: SchoolSettings) => api.put<SchoolSettings>("/org/school/policies", body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: orgKeys.school }),
  });
}

export const useModules = () => useList<ModuleState[]>(orgKeys.modules, "/org/school/modules");
export function useUpdateModules() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (enabled: string[]) => api.put<ModuleState[]>("/org/school/modules", { enabled }),
    onSuccess: (modules) => {
      queryClient.setQueryData(orgKeys.modules, modules);
      void queryClient.invalidateQueries({ queryKey: ["auth", "me"] });
    },
  });
}

export const useSequences = () => useList<Sequence[]>(orgKeys.sequences, "/org/school/sequences");
export function useUpdateSequence() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ key, body }: { key: string; body: Pick<Sequence, "prefix" | "pattern" | "resetPolicy"> & { nextNumber?: number } }) =>
      api.put<Sequence>(`/org/school/sequences/${key}`, body),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: orgKeys.sequences }),
  });
}

export const useCampusDetails = () => useList<CampusDetail[]>(orgKeys.campuses, "/org/campus-management");
export const useSaveCampus = () => useSaveMutation<unknown, CampusDetail>("/org/campus-management", [orgKeys.campuses, ["org", "campuses"], ...touchesOverview]);
export const useMakeMainCampus = () => useActionMutation((id) => `/org/campus-management/${id}/make-main`, [orgKeys.campuses, ["org", "campuses"]]);

export const useAcademicYears = () => useList<AcademicYear[]>(orgKeys.years, "/org/academic-years");
export const useSaveAcademicYear = () => useSaveMutation<unknown, AcademicYear>("/org/academic-years", [orgKeys.years, ...touchesOverview]);
export const useDeleteAcademicYear = () => useDeleteMutation("/org/academic-years", [orgKeys.years, ...touchesOverview]);
export const useMakeCurrentYear = () => useActionMutation((id) => `/org/academic-years/${id}/make-current`, [orgKeys.years, ["org"], ...touchesOverview]);
export const useCloseYear = () => useActionMutation((id) => `/org/academic-years/${id}/close`, [orgKeys.years]);
export function useSaveTerm() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ yearId, termId, body }: { yearId: string; termId?: string; body: { name: string; startDate: string; endDate: string } }) =>
      termId ? api.put<AcademicYear>(`/org/academic-years/${yearId}/terms/${termId}`, body) : api.post<AcademicYear>(`/org/academic-years/${yearId}/terms`, body),
    onSuccess: () => [orgKeys.years, ...touchesOverview].forEach((key) => void queryClient.invalidateQueries({ queryKey: key })),
  });
}
export function useDeleteTerm() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ yearId, termId }: { yearId: string; termId: string }) => api.delete<AcademicYear>(`/org/academic-years/${yearId}/terms/${termId}`),
    onSuccess: () => [orgKeys.years, ...touchesOverview].forEach((key) => void queryClient.invalidateQueries({ queryKey: key })),
  });
}

export const useGrades = () => useList<Grade[]>(orgKeys.grades, "/org/grades");
export const useSaveGrade = () => useSaveMutation<unknown, Grade>("/org/grades", [orgKeys.grades, ...touchesOverview]);
export const useDeleteGrade = () => useDeleteMutation("/org/grades", [orgKeys.grades, ...touchesOverview]);
export function useReorderGrades() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (orderedIds: string[]) => api.put<Grade[]>("/org/grades/order", { orderedIds }),
    onSuccess: (grades) => queryClient.setQueryData(orgKeys.grades, grades),
  });
}

export const useSections = (query: { academicYearId?: string; campusId?: string; gradeId?: string }) =>
  useList<Section[]>(orgKeys.sections(query), "/org/sections", query, query.academicYearId !== "");
export const useSaveSection = () => useSaveMutation<unknown, Section>("/org/sections", [["org", "sections"], orgKeys.grades, orgKeys.years, ...touchesOverview]);
export const useDeleteSection = () => useDeleteMutation("/org/sections", [["org", "sections"], orgKeys.grades, orgKeys.years, ...touchesOverview]);
export function useCopySections() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { fromAcademicYearId: string; toAcademicYearId: string }) => api.post<CopyResult>("/org/sections/copy", body),
    onSuccess: () => [["org", "sections"], orgKeys.years, orgKeys.grades].forEach((key) => void queryClient.invalidateQueries({ queryKey: key })),
  });
}

export const useSubjects = () => useList<Subject[]>(orgKeys.subjects, "/org/subjects");
export const useSaveSubject = () => useSaveMutation<unknown, Subject>("/org/subjects", [orgKeys.subjects, orgKeys.groups, ...touchesOverview]);
export const useDeleteSubject = () => useDeleteMutation("/org/subjects", [orgKeys.subjects, ...touchesOverview]);
export const useSubjectGroups = () => useList<SubjectGroup[]>(orgKeys.groups, "/org/subjects/groups");
export const useSaveSubjectGroup = () => useSaveMutation<unknown, SubjectGroup>("/org/subjects/groups", [orgKeys.groups, orgKeys.subjects]);

export const useCurriculum = (query: { academicYearId?: string; gradeId?: string }) =>
  useList<CurriculumEntry[]>(orgKeys.curriculum(query), "/org/curriculum", query, Boolean(query.gradeId));
export function useSetCurriculum() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { academicYearId: string; gradeId: string; items: { subjectId: string; isMandatory: boolean; periodsPerWeek: number; subjectGroupId: string | null }[] }) =>
      api.put<CurriculumEntry[]>("/org/curriculum", body),
    onSuccess: () => [["org", "curriculum"], orgKeys.subjects, ...touchesOverview].forEach((key) => void queryClient.invalidateQueries({ queryKey: key })),
  });
}

export const useRooms = (query: { campusId?: string } = {}) => useList<Room[]>(orgKeys.rooms(query), "/org/rooms", query);
export const useSaveRoom = () => useSaveMutation<unknown, Room>("/org/rooms", [["org", "rooms"], orgKeys.campuses, ...touchesOverview]);
export const useDeleteRoom = () => useDeleteMutation("/org/rooms", [["org", "rooms"], orgKeys.campuses, ...touchesOverview]);

export const useCalendar = (query: { from?: string; to?: string; type?: string }) =>
  useList<CalendarEvent[]>(orgKeys.calendar(query), "/org/calendar", query);
export const useSaveCalendarEvent = () => useSaveMutation<unknown, CalendarEvent>("/org/calendar", [["org", "calendar"], ...touchesOverview]);
export const useDeleteCalendarEvent = () => useDeleteMutation("/org/calendar", [["org", "calendar"], ...touchesOverview]);

export const useGradingScales = () => useList<GradingScale[]>(orgKeys.grading, "/org/grading-scales");
export const useSaveGradingScale = () => useSaveMutation<unknown, GradingScale>("/org/grading-scales", [orgKeys.grading]);
export const useDeleteGradingScale = () => useDeleteMutation("/org/grading-scales", [orgKeys.grading]);
export const useMakeDefaultScale = () => useActionMutation((id) => `/org/grading-scales/${id}/make-default`, [orgKeys.grading]);

export const useDepartments = () => useList<Department[]>(orgKeys.departments, "/org/departments");
export const useSaveDepartment = () => useSaveMutation<unknown, Department>("/org/departments", [orgKeys.departments]);
export const useDeleteDepartment = () => useDeleteMutation("/org/departments", [orgKeys.departments]);
export const useDesignations = () => useList<Designation[]>(orgKeys.designations, "/org/designations");
export const useSaveDesignation = () => useSaveMutation<unknown, Designation>("/org/designations", [orgKeys.designations]);
export const useDeleteDesignation = () => useDeleteMutation("/org/designations", [orgKeys.designations]);

export type { Paged };
