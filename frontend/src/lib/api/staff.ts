import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryKey } from "@tanstack/react-query";
import { api, download, type Paged } from "./client";
import type { AccountHandover } from "./access";
import type { Address } from "./org";
import type { Gender } from "./students";

type Tone = "neutral" | "accent" | "success" | "warning" | "danger" | "info";

// ───────── Types (mirror Scholar.Application.Staffing DTOs) ─────────

export type StaffType = "Teaching" | "NonTeaching" | "Administrative" | "Support";
export type EmploymentType = "Permanent" | "Contract" | "Probation" | "PartTime" | "Visiting";
export type StaffStatus = "Active" | "OnLeave" | "Suspended" | "Resigned" | "Terminated" | "Retired";
export type StaffScope = "current" | "left" | "all";

export type StaffListItem = {
  id: string;
  employeeNumber: string;
  fullName: string;
  gender: Gender;
  photoFileId: string | null;
  staffType: StaffType;
  status: StaffStatus;
  campusId: string;
  campusName: string;
  departmentName: string | null;
  designationName: string | null;
  phone: string | null;
  email: string | null;
  joinDate: string;
  hasLogin: boolean;
  /** Sections the person is class teacher of this year. */
  classTeacherOf: string | null;
};

export type StaffOption = {
  id: string;
  fullName: string;
  employeeNumber: string;
  campusId: string;
  designationName: string | null;
  staffType: StaffType;
  classTeacherCount: number;
  subjectCount: number;
};

export type StaffAssignment = { sectionId: string; className: string; gradeOrder: number; subjectId: string | null; subjectName: string | null };

export type StaffAccount = {
  userId: string;
  userName: string;
  email: string | null;
  status: "Active" | "Invited" | "Disabled";
  accessSuspended: boolean;
  roles: string[];
  lastLoginAt: string | null;
};

export type StaffDetail = {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  gender: Gender;
  dateOfBirth: string | null;
  nationalId: string | null;
  phone: string | null;
  email: string | null;
  address: Address;
  photoFileId: string | null;
  campusId: string;
  campusName: string;
  departmentId: string | null;
  departmentName: string | null;
  designationId: string | null;
  designationName: string | null;
  staffType: StaffType;
  employmentType: EmploymentType;
  joinDate: string;
  status: StaffStatus;
  exitDate: string | null;
  exitReason: string | null;
  account: StaffAccount | null;
  classTeacherOf: StaffAssignment[];
  teaches: StaffAssignment[];
  version: number;
};

export type SaveStaff = {
  firstName: string;
  lastName: string;
  gender: Gender;
  dateOfBirth: string | null;
  nationalId: string | null;
  phone: string | null;
  email: string | null;
  address: Address | null;
  campusId: string;
  departmentId: string | null;
  designationId: string | null;
  staffType: StaffType;
  employmentType: EmploymentType;
  joinDate: string;
  employeeNumber?: string | null;
  version?: number;
};

export type SubjectTeacher = { subjectId: string; subjectName: string; subjectCode: string; offered: boolean; staffId: string | null; staffName: string | null };

export type SectionTeaching = {
  sectionId: string;
  className: string;
  campusId: string;
  campusName: string;
  gradeOrder: number;
  classTeacherId: string | null;
  classTeacherName: string | null;
  subjects: SubjectTeacher[];
};

// ───────── Labels ─────────

export const staffTypeLabel: Record<StaffType, string> = {
  Teaching: "Teaching",
  NonTeaching: "Non-teaching",
  Administrative: "Administration",
  Support: "Support",
};

export const employmentTypeLabel: Record<EmploymentType, string> = {
  Permanent: "Permanent",
  Contract: "Contract",
  Probation: "Probation",
  PartTime: "Part-time",
  Visiting: "Visiting",
};

export const staffStatusLabel: Record<StaffStatus, string> = {
  Active: "Active",
  OnLeave: "On long leave",
  Suspended: "Suspended",
  Resigned: "Resigned",
  Terminated: "Terminated",
  Retired: "Retired",
};

export const staffStatusTone: Record<StaffStatus, Tone> = {
  Active: "success",
  OnLeave: "info",
  Suspended: "warning",
  Resigned: "neutral",
  Terminated: "danger",
  Retired: "neutral",
};

export const leavingStatuses: StaffStatus[] = ["Resigned", "Terminated", "Retired"];

// ───────── Keys & hooks ─────────

export const staffKeys = {
  all: ["staff"] as const,
  list: (query: object) => ["staff", "list", query] as const,
  detail: (id: string) => ["staff", "detail", id] as const,
  options: (campusId?: string | null) => ["staff", "options", campusId ?? "all"] as const,
  teaching: (query: object) => ["org", "teaching", query] as const,
};

type Query = Record<string, string | number | string[] | undefined>;

function useInvalidate() {
  const queryClient = useQueryClient();
  return (...keys: QueryKey[]) => keys.forEach((key) => void queryClient.invalidateQueries({ queryKey: key }));
}

// Staff changes show up in teacher pickers, section lists (class teacher) and attendance (who takes registers).
const staffTouches: QueryKey[] = [staffKeys.all, ["org", "teaching"], ["org", "sections"], ["attendance"]];

export const useStaffList = (query: Query) =>
  useQuery({
    queryKey: staffKeys.list(query),
    queryFn: ({ signal }) => api.get<Paged<StaffListItem>>("/staff", query, signal),
    placeholderData: keepPreviousData,
  });

export const useStaffMember = (id: string) =>
  useQuery({ queryKey: staffKeys.detail(id), queryFn: ({ signal }) => api.get<StaffDetail>(`/staff/${id}`, undefined, signal) });

export const useStaffOptions = (campusId?: string | null, enabled = true) =>
  useQuery({
    queryKey: staffKeys.options(campusId),
    queryFn: ({ signal }) => api.get<StaffOption[]>("/staff/options", campusId ? { campusId } : undefined, signal),
    enabled,
    staleTime: 60_000,
  });

export const exportStaff = (query: Query, format: "csv" | "xlsx") => download("/staff/export", { ...query, format });

export function useSaveStaff() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveStaff }) => (id ? api.put<StaffDetail>(`/staff/${id}`, body) : api.post<StaffDetail>("/staff", body)),
    onSuccess: () => invalidate(...staffTouches),
  });
}

export function useChangeStaffStatus() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: { status: StaffStatus; effectiveDate: string; reason: string | null } }) =>
      api.post<{ staff: StaffDetail; removedAssignments: number }>(`/staff/${id}/status`, body),
    onSuccess: () => invalidate(...staffTouches),
  });
}

export function useDeleteStaff() {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (id: string) => api.delete(`/staff/${id}`), onSuccess: () => invalidate(...staffTouches) });
}

export function useGrantStaffAccess() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, roles }: { id: string; roles: { roleId: string; campusId: string | null }[] }) =>
      api.post<{ staff: StaffDetail; handover: AccountHandover | null }>(`/staff/${id}/account`, { roles }),
    onSuccess: () => invalidate(...staffTouches, ["access"]),
  });
}

export function useUnlinkStaffAccount() {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (id: string) => api.delete<StaffDetail>(`/staff/${id}/account`), onSuccess: () => invalidate(...staffTouches) });
}

export const useTeaching = (query: { academicYearId?: string; campusId?: string }) =>
  useQuery({
    queryKey: staffKeys.teaching(query),
    queryFn: ({ signal }) => api.get<SectionTeaching[]>("/org/teaching", query, signal),
  });

export function useSaveSectionTeachers() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ sectionId, body }: { sectionId: string; body: { classTeacherId: string | null; subjects: { subjectId: string; staffId: string | null }[] } }) =>
      api.put<SectionTeaching>(`/org/teaching/sections/${sectionId}`, body),
    onSuccess: () => invalidate(...staffTouches),
  });
}
