import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryKey } from "@tanstack/react-query";
import { api, upload, type Paged } from "./client";
import type { Address } from "./org";
import type { Gender, GuardianRelationship, StoredFile } from "./students";

// ───────── Types (mirror Scholar.Application.Admissions DTOs) ─────────

export type ApplicationStatus =
  | "Draft" | "Submitted" | "UnderReview" | "AssessmentScheduled" | "Assessed" | "Offered" | "Waitlisted" | "Accepted"
  | "Rejected" | "Enrolled" | "Withdrawn" | "Expired";
export type ApplicationSource = "Office" | "Online";
export type InquiryStatus = "New" | "Contacted" | "ApplicationIssued" | "Converted" | "Closed";
export type InquirySource = "WalkIn" | "Phone" | "Website" | "Referral" | "SocialMedia" | "Event" | "Other";
export type AssessmentType = "EntranceTest" | "Interview";
export type AssessmentOutcome = "Pending" | "Passed" | "Failed" | "NoShow";

export type ApplicationListItem = {
  id: string;
  applicationNumber: string;
  applicantName: string;
  gender: Gender;
  dateOfBirth: string;
  gradeId: string;
  gradeName: string;
  campusName: string;
  guardianName: string;
  guardianPhone: string;
  status: ApplicationStatus;
  source: ApplicationSource;
  submittedAt: string | null;
  waitlistRank: number | null;
  offerExpiresOn: string | null;
  updatedAt: string;
};

export type PipelineStage = { status: ApplicationStatus; count: number };
export type Assessment = { id: string; type: AssessmentType; scheduledAt: string; location: string | null; outcome: AssessmentOutcome; score: number | null; maxScore: number | null; notes: string | null };
export type ApplicationHistory = { fromStatus: ApplicationStatus; toStatus: ApplicationStatus; note: string | null; changedByName: string | null; changedAt: string };

export type ApplicationDetail = {
  id: string;
  applicationNumber: string;
  status: ApplicationStatus;
  source: ApplicationSource;
  academicYearId: string;
  academicYearName: string;
  campusId: string;
  campusName: string;
  gradeId: string;
  gradeName: string;
  inquiryId: string | null;
  firstName: string;
  middleName: string | null;
  lastName: string;
  applicantName: string;
  gender: Gender;
  dateOfBirth: string;
  nationalId: string | null;
  previousSchoolName: string | null;
  previousSchoolClass: string | null;
  previousResult: string | null;
  address: Address;
  guardianFirstName: string;
  guardianLastName: string;
  guardianRelationship: GuardianRelationship;
  guardianPhone: string;
  guardianEmail: string | null;
  guardianNationalId: string | null;
  guardianOccupation: string | null;
  notes: string | null;
  submittedAt: string | null;
  decisionNote: string | null;
  decidedAt: string | null;
  waitlistRank: number | null;
  offerExpiresOn: string | null;
  studentId: string | null;
  allowedTransitions: ApplicationStatus[];
  assessments: Assessment[];
  history: ApplicationHistory[];
  version: number;
};

export type ApplicantInput = {
  firstName: string;
  middleName: string | null;
  lastName: string;
  gender: Gender;
  dateOfBirth: string;
  nationalId: string | null;
  previousSchoolName: string | null;
  previousSchoolClass: string | null;
  previousResult: string | null;
  address: Address | null;
  guardianFirstName: string;
  guardianLastName: string;
  guardianRelationship: GuardianRelationship;
  guardianPhone: string;
  guardianEmail: string | null;
  guardianNationalId: string | null;
  guardianOccupation: string | null;
  notes: string | null;
};

export type Inquiry = {
  id: string;
  studentName: string;
  dateOfBirth: string | null;
  guardianName: string;
  phone: string;
  email: string | null;
  source: InquirySource;
  status: InquiryStatus;
  campusId: string | null;
  campusName: string | null;
  gradeId: string | null;
  gradeName: string | null;
  followUpOn: string | null;
  notes: string | null;
  closedReason: string | null;
  createdAt: string;
  applicationId: string | null;
};

export type SaveInquiry = {
  studentName: string;
  dateOfBirth: string | null;
  guardianName: string;
  phone: string;
  email: string | null;
  source: InquirySource;
  campusId: string | null;
  gradeId: string | null;
  followUpOn: string | null;
  notes: string | null;
};

export type PublicSchool = {
  code: string;
  name: string;
  shortName: string | null;
  motto: string | null;
  brandColor: string | null;
  logoFileId: string | null;
  phone: string | null;
  email: string | null;
  acceptingApplications: boolean;
  academicYearName: string | null;
  academicYearId: string | null;
  campuses: { id: string; name: string; city: string | null }[];
  grades: { id: string; name: string }[];
  requiredDocuments: string[];
};

export type PublicReceipt = { applicationNumber: string; schoolName: string; applicantName: string };

// ───────── Labels ─────────

export const applicationStatusLabel: Record<ApplicationStatus, string> = {
  Draft: "Draft",
  Submitted: "New",
  UnderReview: "Under review",
  AssessmentScheduled: "Assessment booked",
  Assessed: "Assessed",
  Offered: "Offered a place",
  Waitlisted: "Waiting list",
  Accepted: "Accepted",
  Rejected: "Not offered",
  Enrolled: "Enrolled",
  Withdrawn: "Withdrawn",
  Expired: "Offer lapsed",
};

export const applicationStatusTone: Record<ApplicationStatus, "neutral" | "info" | "accent" | "success" | "warning" | "danger"> = {
  Draft: "neutral",
  Submitted: "info",
  UnderReview: "info",
  AssessmentScheduled: "info",
  Assessed: "info",
  Offered: "accent",
  Waitlisted: "warning",
  Accepted: "success",
  Rejected: "neutral",
  Enrolled: "success",
  Withdrawn: "neutral",
  Expired: "warning",
};

/** The working stages, in pipeline order. */
export const openStages: ApplicationStatus[] = ["Submitted", "UnderReview", "AssessmentScheduled", "Assessed", "Waitlisted", "Offered", "Accepted"];
export const closedStages: ApplicationStatus[] = ["Enrolled", "Rejected", "Withdrawn", "Expired"];

export const inquiryStatusLabel: Record<InquiryStatus, string> = {
  New: "New",
  Contacted: "Contacted",
  ApplicationIssued: "Form issued",
  Converted: "Applied",
  Closed: "Closed",
};

export const inquirySourceLabel: Record<InquirySource, string> = {
  WalkIn: "Walk-in",
  Phone: "Phone call",
  Website: "Website",
  Referral: "Referral",
  SocialMedia: "Social media",
  Event: "Open day / event",
  Other: "Other",
};

export const assessmentTypeLabel: Record<AssessmentType, string> = { EntranceTest: "Entrance test", Interview: "Interview" };
export const assessmentOutcomeLabel: Record<AssessmentOutcome, string> = { Pending: "Result pending", Passed: "Passed", Failed: "Did not pass", NoShow: "Did not attend" };

// ───────── Keys & hooks ─────────

export const admissionKeys = {
  all: ["admissions"] as const,
  applications: (query: object) => ["admissions", "applications", query] as const,
  pipeline: (yearId?: string) => ["admissions", "pipeline", yearId ?? "all"] as const,
  application: (id: string) => ["admissions", "application", id] as const,
  documents: (id: string) => ["admissions", "documents", id] as const,
  inquiries: (query: object) => ["admissions", "inquiries", query] as const,
};

type Query = Record<string, string | number | string[] | undefined>;

function useInvalidate() {
  const queryClient = useQueryClient();
  return (...keys: QueryKey[]) => keys.forEach((key) => void queryClient.invalidateQueries({ queryKey: key }));
}

export const useApplications = (query: Query) =>
  useQuery({
    queryKey: admissionKeys.applications(query),
    queryFn: ({ signal }) => api.get<Paged<ApplicationListItem>>("/admissions/applications", query, signal),
    placeholderData: keepPreviousData,
  });

export const usePipeline = (academicYearId?: string) =>
  useQuery({
    queryKey: admissionKeys.pipeline(academicYearId),
    queryFn: ({ signal }) => api.get<PipelineStage[]>("/admissions/applications/pipeline", { academicYearId }, signal),
  });

export const useApplication = (id: string) =>
  useQuery({ queryKey: admissionKeys.application(id), queryFn: ({ signal }) => api.get<ApplicationDetail>(`/admissions/applications/${id}`, undefined, signal) });

function useApplicationMutation<TBody>(build: (body: TBody) => Promise<ApplicationDetail>) {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: build, onSuccess: () => invalidate(admissionKeys.all, ["search"]) });
}

export const useCreateApplication = () =>
  useApplicationMutation((body: { academicYearId: string; campusId: string; gradeId: string; applicant: ApplicantInput; inquiryId: string | null; submit: boolean }) =>
    api.post<ApplicationDetail>("/admissions/applications", body));

export const useUpdateApplication = (id: string) =>
  useApplicationMutation((body: { campusId: string; gradeId: string; applicant: ApplicantInput; version: number }) =>
    api.put<ApplicationDetail>(`/admissions/applications/${id}`, body));

/** submit · review · accept · withdraw */
export const useApplicationAction = (id: string) =>
  useApplicationMutation(({ action, reason }: { action: "submit" | "review" | "accept" | "withdraw"; reason?: string | null }) =>
    api.post<ApplicationDetail>(`/admissions/applications/${id}/actions/${action}`, { reason: reason ?? null }));

export const useDecision = (id: string) =>
  useApplicationMutation((body: { decision: "offer" | "waitlist" | "reject"; note: string | null; offerExpiresOn: string | null; waitlistRank: number | null }) =>
    api.post<ApplicationDetail>(`/admissions/applications/${id}/decision`, body));

export const useScheduleAssessment = (id: string) =>
  useApplicationMutation((body: { type: AssessmentType; scheduledAt: string; location: string | null }) =>
    api.post<ApplicationDetail>(`/admissions/applications/${id}/assessments`, body));

export const useRecordAssessment = (id: string) =>
  useApplicationMutation(({ assessmentId, ...body }: { assessmentId: string; outcome: AssessmentOutcome; score: number | null; maxScore: number | null; notes: string | null }) =>
    api.put<ApplicationDetail>(`/admissions/applications/${id}/assessments/${assessmentId}`, body));

export function useEnrollApplicant(id: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: { sectionId: string; admissionDate: string; rollNumber: string | null; overrideCapacity: boolean }) =>
      api.post<ApplicationDetail>(`/admissions/applications/${id}/enroll`, body),
    onSuccess: () => invalidate(admissionKeys.all, ["students"], ["guardians"], ["org", "sections"], ["org", "overview"]),
  });
}

export const useApplicationDocuments = (id: string) =>
  useQuery({ queryKey: admissionKeys.documents(id), queryFn: ({ signal }) => api.get<StoredFile[]>(`/admissions/applications/${id}/documents`, undefined, signal) });

export function useUploadApplicationFile(id: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ file, category }: { file: File; category: string }) => {
      const form = new FormData();
      form.append("file", file);
      form.append("category", category);
      return upload<StoredFile>(`/admissions/applications/${id}/documents`, form);
    },
    onSuccess: () => invalidate(admissionKeys.documents(id)),
  });
}

export const useInquiries = (query: Query) =>
  useQuery({
    queryKey: admissionKeys.inquiries(query),
    queryFn: ({ signal }) => api.get<Paged<Inquiry>>("/admissions/inquiries", query, signal),
    placeholderData: keepPreviousData,
  });

export function useSaveInquiry() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, body }: { id?: string; body: SaveInquiry }) =>
      id ? api.put<Inquiry>(`/admissions/inquiries/${id}`, body) : api.post<Inquiry>("/admissions/inquiries", body),
    onSuccess: () => invalidate(admissionKeys.all),
  });
}

export function useInquiryAction() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, action, reason }: { id: string; action: "contacted" | "close" | "reopen"; reason?: string | null }) =>
      api.post<Inquiry>(`/admissions/inquiries/${id}/${action}`, { reason: reason ?? null }),
    onSuccess: () => invalidate(admissionKeys.all),
  });
}

// ───────── Public (no session) ─────────

export const usePublicSchool = (code: string) =>
  useQuery({
    queryKey: ["public", "school", code],
    queryFn: ({ signal }) => api.get<PublicSchool>(`/public/schools/${encodeURIComponent(code)}`, undefined, signal),
    retry: false,
  });

export const submitPublicApplication = (code: string, body: { campusId: string; gradeId: string; applicant: ApplicantInput }) =>
  api.post<PublicReceipt>(`/public/schools/${encodeURIComponent(code)}/applications`, body, { noRefresh: true });
