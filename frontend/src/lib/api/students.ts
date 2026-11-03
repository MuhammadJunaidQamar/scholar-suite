import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryKey } from "@tanstack/react-query";
import { api, download, upload, type Paged } from "./client";
import type { AccountHandover } from "./access";
import type { Address } from "./org";

// ───────── Types (mirror Scholar.Application.Students DTOs) ─────────

export type Gender = "Female" | "Male" | "Other";
export type StudentStatus = "Active" | "Suspended" | "Inactive" | "Withdrawn" | "TransferredOut" | "Graduated" | "Alumni";
export type EnrollmentStatus = "Active" | "Promoted" | "Retained" | "Transferred" | "Withdrawn" | "Graduated" | "Completed";
export type GuardianRelationship = "Father" | "Mother" | "Guardian" | "Grandfather" | "Grandmother" | "Brother" | "Sister" | "Uncle" | "Aunt" | "Other";
export type StudentScope = "current" | "left" | "all";

export type StudentListItem = {
  id: string;
  admissionNumber: string;
  fullName: string;
  gender: Gender;
  dateOfBirth: string;
  photoFileId: string | null;
  status: StudentStatus;
  campusId: string;
  campusName: string;
  sectionId: string | null;
  className: string | null;
  gradeOrder: number | null;
  guardianName: string | null;
  guardianPhone: string | null;
  admissionDate: string;
  hasMedicalAlert: boolean;
  /** When the current status took effect, e.g. the leaving date. */
  statusChangedOn: string | null;
};

export type GuardianLink = {
  linkId: string;
  guardianId: string;
  fullName: string;
  relationship: GuardianRelationship;
  phone: string;
  email: string | null;
  occupation: string | null;
  isPrimary: boolean;
  isEmergencyContact: boolean;
  livesWithStudent: boolean;
  canPickUp: boolean;
  isFeePayer: boolean;
  portalAccess: boolean;
  canViewAttendance: boolean;
  canViewResults: boolean;
  canViewFees: boolean;
  receivesNotifications: boolean;
  hasLogin: boolean;
};

export type Enrollment = {
  id: string;
  academicYearId: string;
  academicYearName: string;
  sectionId: string;
  className: string;
  campusName: string;
  rollNumber: string | null;
  status: EnrollmentStatus;
  startDate: string;
  endDate: string | null;
  reason: string | null;
};

export type StatusChange = {
  id: string;
  fromStatus: StudentStatus;
  toStatus: StudentStatus;
  effectiveDate: string;
  reason: string | null;
  destinationSchool: string | null;
  changedByName: string | null;
  recordedAt: string;
};

export type EmergencyContact = { id: string; name: string; relationship: string; phone: string; alternatePhone: string | null; priority: number };
export type Sibling = { id: string; fullName: string; admissionNumber: string; className: string | null; photoFileId: string | null; status: StudentStatus };

export type StudentDetail = {
  id: string;
  admissionNumber: string;
  registrationNumber: string | null;
  firstName: string;
  middleName: string | null;
  lastName: string;
  fullName: string;
  gender: Gender;
  dateOfBirth: string;
  placeOfBirth: string | null;
  nationalId: string | null;
  nationality: string | null;
  religion: string | null;
  bloodGroup: string | null;
  motherTongue: string | null;
  photoFileId: string | null;
  email: string | null;
  phone: string | null;
  address: Address;
  previousSchoolName: string | null;
  previousSchoolClass: string | null;
  admissionDate: string;
  status: StudentStatus;
  statusChangedOn: string | null;
  medicalAlert: string | null;
  campusId: string;
  campusName: string;
  hasLogin: boolean;
  currentEnrollment: Enrollment | null;
  enrollments: Enrollment[];
  guardians: GuardianLink[];
  emergencyContacts: EmergencyContact[];
  siblings: Sibling[];
  statusHistory: StatusChange[];
  documentCount: number;
  version: number;
};

export type StudentDetailsInput = {
  firstName: string;
  middleName: string | null;
  lastName: string;
  gender: Gender;
  dateOfBirth: string;
  placeOfBirth: string | null;
  nationalId: string | null;
  nationality: string | null;
  religion: string | null;
  bloodGroup: string | null;
  motherTongue: string | null;
  email: string | null;
  phone: string | null;
  address: Address | null;
  previousSchoolName: string | null;
  previousSchoolClass: string | null;
  medicalAlert: string | null;
  registrationNumber: string | null;
};

export type GuardianInput = {
  firstName: string;
  lastName: string;
  phone: string;
  email: string | null;
  nationalId: string | null;
  occupation: string | null;
  employer: string | null;
  alternatePhone: string | null;
  address: Address | null;
};

export type GuardianPermissions = {
  isPrimary: boolean;
  isFeePayer: boolean;
  isEmergencyContact: boolean;
  livesWithStudent: boolean;
  canPickUp: boolean;
  portalAccess: boolean;
  canViewAttendance: boolean;
  canViewResults: boolean;
  canViewFees: boolean;
  receivesNotifications: boolean;
};

export type GuardianLinkInput = GuardianPermissions & {
  guardianId: string | null;
  newGuardian: GuardianInput | null;
  relationship: GuardianRelationship;
};

export type EmergencyContactInput = { name: string; relationship: string; phone: string; alternatePhone: string | null };

export type CreateStudentRequest = {
  student: StudentDetailsInput;
  placement: { sectionId: string; admissionDate: string; rollNumber: string | null };
  guardians: GuardianLinkInput[];
  emergencyContacts: EmergencyContactInput[] | null;
  admissionNumber: string | null;
  overrideCapacity: boolean;
};

export type MedicalProfile = {
  allergies: string | null;
  conditions: string | null;
  medications: string | null;
  specialNeeds: string | null;
  doctorName: string | null;
  doctorPhone: string | null;
  emergencyInstructions: string | null;
  bloodGroup: string | null;
  medicalAlert: string | null;
};

export type StoredFile = {
  id: string;
  ownerType: string;
  ownerId: string;
  category: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sensitivity: "Normal" | "Restricted";
  notes: string | null;
  uploadedAt: string;
  uploadedBy: string | null;
  uploadedByName: string | null;
};

export type ImportReport = {
  totalRows: number;
  validRows: number;
  errors: { row: number; column: string; message: string }[];
  committed: boolean;
  created: number;
};

export type GuardianChild = {
  studentId: string;
  linkId: string;
  fullName: string;
  admissionNumber: string;
  className: string | null;
  photoFileId: string | null;
  status: StudentStatus;
  relationship: GuardianRelationship;
  isPrimary: boolean;
  isFeePayer: boolean;
  portalAccess: boolean;
};

export type GuardianListItem = {
  id: string;
  fullName: string;
  phone: string;
  email: string | null;
  occupation: string | null;
  hasLogin: boolean;
  childCount: number;
  children: string[];
};

export type GuardianDetail = {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  phone: string;
  alternatePhone: string | null;
  email: string | null;
  nationalId: string | null;
  occupation: string | null;
  employer: string | null;
  address: Address;
  preferredLanguage: string | null;
  notifyBySms: boolean;
  notifyByEmail: boolean;
  notifyByWhatsApp: boolean;
  hasLogin: boolean;
  loginName: string | null;
  children: GuardianChild[];
};

export type SaveGuardianRequest = Omit<GuardianDetail, "id" | "fullName" | "hasLogin" | "loginName" | "children" | "address"> & {
  address: Address | null;
};

export type GuardianMatch = { id: string; fullName: string; phone: string; email: string | null; children: string[] };
/** Handover is null when the guardian already had an account (e.g. as a member of staff). */
export type PortalAccessResult = { loginName: string; handover: AccountHandover | null };
export type SearchResult = { kind: "student" | "guardian" | "application" | string; id: string; title: string; subtitle: string | null; href: string; photoFileId: string | null };

// ───────── Labels ─────────

export const studentStatusLabel: Record<StudentStatus, string> = {
  Active: "Active",
  Suspended: "Suspended",
  Inactive: "Inactive",
  Withdrawn: "Withdrawn",
  TransferredOut: "Transferred out",
  Graduated: "Graduated",
  Alumni: "Alumni",
};

export const studentStatusTone: Record<StudentStatus, "success" | "warning" | "neutral" | "danger" | "info"> = {
  Active: "success",
  Suspended: "warning",
  Inactive: "neutral",
  Withdrawn: "neutral",
  TransferredOut: "neutral",
  Graduated: "info",
  Alumni: "info",
};

export const enrollmentStatusLabel: Record<EnrollmentStatus, string> = {
  Active: "Current",
  Promoted: "Promoted",
  Retained: "Repeated the year",
  Transferred: "Moved section",
  Withdrawn: "Withdrawn",
  Graduated: "Graduated",
  Completed: "Completed",
};

export const relationships: GuardianRelationship[] = ["Father", "Mother", "Guardian", "Grandfather", "Grandmother", "Brother", "Sister", "Uncle", "Aunt", "Other"];
export const bloodGroups = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];

export const documentCategoryLabel: Record<string, string> = {
  photo: "Photo",
  birth_certificate: "Birth certificate / B-Form",
  national_id: "CNIC copy",
  previous_result: "Previous result card",
  transfer_certificate: "School leaving / transfer certificate",
  medical: "Medical record",
  other: "Other",
};

/** Formats a stored +92XXXXXXXXXX mobile number the way people write it: +92 300 1234567. */
export function formatPhone(phone: string | null | undefined) {
  if (!phone) return "";
  const match = /^\+92(\d{3})(\d{7})$/.exec(phone);
  return match ? `+92 ${match[1]} ${match[2]}` : phone;
}

/** 13-digit CNIC / B-Form numbers are written 35202-1234567-8. */
export function formatNationalId(value: string | null | undefined) {
  if (!value) return "";
  const digits = value.replace(/\D/g, "");
  return digits.length === 13 ? `${digits.slice(0, 5)}-${digits.slice(5, 12)}-${digits.slice(12)}` : value;
}

/** Whole years between a date of birth and today. */
export function ageFrom(dateOfBirth: string, on = new Date()) {
  const [y, m, d] = dateOfBirth.split("-").map(Number) as [number, number, number];
  let age = on.getFullYear() - y;
  if (on.getMonth() + 1 < m || (on.getMonth() + 1 === m && on.getDate() < d)) age--;
  return age;
}

export const fileUrl = (id: string, generation = 0) => `/api/files/${id}/content${generation ? `?g=${generation}` : ""}`;

// ───────── Keys ─────────

export const studentKeys = {
  all: ["students"] as const,
  list: (query: object) => ["students", "list", query] as const,
  detail: (id: string) => ["students", "detail", id] as const,
  medical: (id: string) => ["students", "medical", id] as const,
  documents: (id: string) => ["students", "documents", id] as const,
};

export const guardianKeys = {
  all: ["guardians"] as const,
  list: (query: object) => ["guardians", "list", query] as const,
  detail: (id: string) => ["guardians", "detail", id] as const,
  lookup: (phone: string) => ["guardians", "lookup", phone] as const,
};

type Query = Record<string, string | number | string[] | undefined>;

function useInvalidate() {
  const queryClient = useQueryClient();
  return (...keys: QueryKey[]) => keys.forEach((key) => void queryClient.invalidateQueries({ queryKey: key }));
}

// Anything that changes a student can change list rows, guardian pages and section occupancy.
const studentTouches: QueryKey[] = [studentKeys.all, guardianKeys.all, ["org", "sections"], ["org", "overview"]];

// ───────── Students ─────────

export const useStudents = (query: Query) =>
  useQuery({
    queryKey: studentKeys.list(query),
    queryFn: ({ signal }) => api.get<Paged<StudentListItem>>("/students", query, signal),
    placeholderData: keepPreviousData,
  });

export const useStudent = (id: string) =>
  useQuery({ queryKey: studentKeys.detail(id), queryFn: ({ signal }) => api.get<StudentDetail>(`/students/${id}`, undefined, signal) });

export function useCreateStudent() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: CreateStudentRequest) => api.post<StudentDetail>("/students", body),
    onSuccess: () => invalidate(...studentTouches),
  });
}

export function useUpdateStudent(id: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: { student: StudentDetailsInput; version: number }) => api.put<StudentDetail>(`/students/${id}`, body),
    onSuccess: () => invalidate(...studentTouches),
  });
}

export function useDeleteStudent() {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (id: string) => api.delete(`/students/${id}`), onSuccess: () => invalidate(...studentTouches) });
}

export function useChangeStudentStatus() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: { status: StudentStatus; effectiveDate: string; reason: string | null; destinationSchool: string | null } }) =>
      api.post<StudentDetail>(`/students/${id}/status`, body),
    onSuccess: () => invalidate(...studentTouches),
  });
}

export function useTransferStudent() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: { sectionId: string; effectiveDate: string; reason: string | null; overrideCapacity: boolean } }) =>
      api.post<StudentDetail>(`/students/${id}/transfer`, body),
    onSuccess: () => invalidate(...studentTouches),
  });
}

export function useBulkTransfer() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: { studentIds: string[]; sectionId: string; effectiveDate: string; reason: string | null; overrideCapacity: boolean }) =>
      api.post<{ affected: number }>("/students/bulk/transfer", body),
    onSuccess: () => invalidate(...studentTouches),
  });
}

export function useAddGuardianLink(studentId: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: GuardianLinkInput) => api.post<StudentDetail>(`/students/${studentId}/guardians`, body),
    onSuccess: () => invalidate(...studentTouches),
  });
}

export function useUpdateGuardianLink(studentId: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ linkId, body }: { linkId: string; body: GuardianPermissions & { relationship: GuardianRelationship } }) =>
      api.put<StudentDetail>(`/students/${studentId}/guardians/${linkId}`, body),
    onSuccess: () => invalidate(...studentTouches),
  });
}

export function useRemoveGuardianLink(studentId: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (linkId: string) => api.delete<StudentDetail>(`/students/${studentId}/guardians/${linkId}`),
    onSuccess: () => invalidate(...studentTouches),
  });
}

export function useSetEmergencyContacts(studentId: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (contacts: EmergencyContactInput[]) => api.put<StudentDetail>(`/students/${studentId}/emergency-contacts`, contacts),
    onSuccess: () => invalidate(studentKeys.detail(studentId)),
  });
}

export const useMedical = (id: string, enabled: boolean) =>
  useQuery({ queryKey: studentKeys.medical(id), queryFn: ({ signal }) => api.get<MedicalProfile>(`/students/${id}/medical`, undefined, signal), enabled });

export function useUpdateMedical(id: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: MedicalProfile) => api.put<MedicalProfile>(`/students/${id}/medical`, body),
    onSuccess: () => invalidate(studentKeys.medical(id), studentKeys.detail(id), studentKeys.all),
  });
}

export const useStudentDocuments = (id: string, enabled: boolean) =>
  useQuery({ queryKey: studentKeys.documents(id), queryFn: ({ signal }) => api.get<StoredFile[]>(`/students/${id}/documents`, undefined, signal), enabled });

export function useUploadStudentFile(id: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ file, category, notes }: { file: File; category: string; notes?: string | null }) => {
      const form = new FormData();
      form.append("file", file);
      form.append("category", category);
      if (notes) form.append("notes", notes);
      return upload<StoredFile>(`/students/${id}/documents`, form);
    },
    onSuccess: () => invalidate(studentKeys.documents(id), studentKeys.detail(id), studentKeys.all),
  });
}

export function useDeleteFile(invalidateKeys: QueryKey[]) {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (fileId: string) => api.delete(`/files/${fileId}`), onSuccess: () => invalidate(...invalidateKeys) });
}

export function useImportStudents() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ file, commit }: { file: File; commit: boolean }) => {
      const form = new FormData();
      form.append("file", file);
      return upload<ImportReport>("/students/import", form, { commit });
    },
    onSuccess: (report) => {
      if (report.committed) invalidate(...studentTouches);
    },
  });
}

export const exportStudents = (query: Query, format: "csv" | "xlsx") => download("/students/export", { ...query, format, page: undefined, pageSize: undefined });
export const downloadImportTemplate = () => download("/students/import/template");

// ───────── Guardians ─────────

export const useGuardians = (query: Query) =>
  useQuery({
    queryKey: guardianKeys.list(query),
    queryFn: ({ signal }) => api.get<Paged<GuardianListItem>>("/guardians", query, signal),
    placeholderData: keepPreviousData,
  });

export const useGuardian = (id: string) =>
  useQuery({ queryKey: guardianKeys.detail(id), queryFn: ({ signal }) => api.get<GuardianDetail>(`/guardians/${id}`, undefined, signal) });

/** Existing guardians with this mobile number (checked before entering a new parent, so siblings share one record). */
export function useGuardianLookup(phone: string) {
  const digits = phone.replace(/\D/g, "");
  return useQuery({
    queryKey: guardianKeys.lookup(digits),
    queryFn: ({ signal }) => api.get<GuardianMatch[]>("/guardians/lookup", { phone }, signal),
    enabled: digits.length >= 10,
    staleTime: 30_000,
  });
}

export function useSaveGuardian() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveGuardianRequest }) =>
      id ? api.put<GuardianDetail>(`/guardians/${id}`, body) : api.post<GuardianDetail>("/guardians", body),
    onSuccess: () => invalidate(guardianKeys.all, studentKeys.all),
  });
}

export function useDeleteGuardian() {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (id: string) => api.delete(`/guardians/${id}`), onSuccess: () => invalidate(guardianKeys.all) });
}

export function useEnablePortal() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (id: string) => api.post<PortalAccessResult>(`/guardians/${id}/portal`),
    onSuccess: () => invalidate(guardianKeys.all, studentKeys.all),
  });
}

// ───────── Search ─────────

export const useSearch = (term: string) =>
  useQuery({
    queryKey: ["search", term],
    queryFn: ({ signal }) => api.get<SearchResult[]>("/search", { q: term }, signal),
    enabled: term.trim().length >= 2,
    staleTime: 15_000,
    placeholderData: keepPreviousData,
  });
