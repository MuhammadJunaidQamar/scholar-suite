import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryKey } from "@tanstack/react-query";
import { api, download, type Paged } from "./client";
import type { Gender } from "./students";
import type { StaffType } from "./staff";

type Tone = "neutral" | "accent" | "success" | "warning" | "danger" | "info";

// ───────── Types (mirror Scholar.Application.Attendance DTOs) ─────────

export type AttendanceStatus = "Present" | "Late" | "Absent" | "Leave";
export type StaffAttendanceStatus = "Present" | "Late" | "HalfDay" | "Absent" | "OnLeave" | "OnDuty";
export type CorrectionStatus = "Pending" | "Approved" | "Rejected" | "Withdrawn";
export type LeaveStatus = "Pending" | "Approved" | "Rejected" | "Cancelled";
export type StudentLeaveKind = "Sick" | "Family" | "Travel" | "Religious" | "Other";
export type AttendanceMode = "Daily" | "PeriodWise";
export type MessageChannel = "Sms" | "WhatsApp" | "Email";
export type MessageStatus = "Pending" | "NotSent" | "Queued" | "Sent" | "Delivered" | "Failed" | "Cancelled";

export type DayStatus = { date: string; isSchoolDay: boolean; closedReason: string | null; isFuture: boolean; isToday: boolean };

export type RegisterHint = { kind: "leave" | "pendingLeave" | "suspended" | "joined" | "left"; text: string; leaveRequestId: string | null };

export type RegisterRow = {
  studentId: string;
  fullName: string;
  admissionNumber: string;
  rollNumber: string | null;
  gender: Gender;
  photoFileId: string | null;
  status: AttendanceStatus;
  remark: string | null;
  arrivedAt: string | null;
  saved: boolean;
  hint: RegisterHint | null;
  medicalAlert: string | null;
  recentAbsences: number;
};

export type RegisterCounts = { total: number; present: number; late: number; absent: number; leave: number };

export type MessageCounts = {
  total: number;
  pending: number;
  notSent: number;
  queued: number;
  sent: number;
  delivered: number;
  failed: number;
  cancelled: number;
  notSentReason: string | null;
  /** Channels with messages that couldn't be sent (no provider connected for them). */
  notSentChannels: MessageChannel[];
};

export type RegisterInfo = {
  id: string;
  takenAt: string;
  takenByName: string | null;
  updatedAt: string | null;
  updatedByName: string | null;
  note: string | null;
  version: number;
};

export type CorrectionItem = {
  studentId: string;
  studentName: string;
  admissionNumber: string;
  fromStatus: AttendanceStatus | null;
  toStatus: AttendanceStatus;
  remark: string | null;
  currentStatus: AttendanceStatus | null;
};

export type Correction = {
  id: string;
  registerId: string;
  sectionId: string;
  className: string;
  date: string;
  period: number | null;
  reason: string;
  status: CorrectionStatus;
  requestedByName: string | null;
  requestedAt: string;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
  items: CorrectionItem[];
  canDecide: boolean;
  canWithdraw: boolean;
};

export type RegisterSheet = {
  sectionId: string;
  className: string;
  campusId: string;
  campusName: string;
  date: string;
  period: number | null;
  periodLabel: string | null;
  subjectId: string | null;
  subjectName: string | null;
  classTeacherName: string | null;
  register: RegisterInfo | null;
  day: DayStatus;
  canTake: boolean;
  isLocked: boolean;
  locksAt: string | null;
  canCorrect: boolean;
  canApprove: boolean;
  canOverrideClosedDay: boolean;
  students: RegisterRow[];
  counts: RegisterCounts;
  pendingCorrection: Correction | null;
  alerts: MessageCounts | null;
  /** Whether absences/lateness alert guardians, and whether any provider can actually send (otherwise alerts are only recorded). */
  alertPolicy: { notifyAbsent: boolean; notifyLate: boolean; canSend: boolean };
  mode: AttendanceMode;
};

export type RegisterMark = { studentId: string; status: AttendanceStatus; remark?: string | null; arrivedAt?: string | null };

export type StudentLeave = {
  id: string;
  studentId: string;
  studentName: string;
  admissionNumber: string;
  className: string | null;
  photoFileId: string | null;
  fromDate: string;
  toDate: string;
  schoolDays: number;
  kind: StudentLeaveKind;
  reason: string;
  requestedBy: string | null;
  status: LeaveStatus;
  recordedAt: string;
  recordedByName: string | null;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
  canDecide: boolean;
};

export type LeaveDecision<T> = { leave: T; marksChanged: number };

export type AttendanceTotals = { days: number; present: number; late: number; absent: number; leave: number; percentage: number | null };

export type SectionDay = {
  sectionId: string;
  className: string;
  gradeOrder: number;
  campusId: string;
  campusName: string;
  classTeacherName: string | null;
  onRoll: number;
  registerId: string | null;
  takenAt: string | null;
  takenByName: string | null;
  present: number;
  late: number;
  absent: number;
  leave: number;
};

export type StaffDayTotals = { staff: number; marked: number; present: number; late: number; halfDay: number; absent: number; onLeave: number; onDuty: number };

export type AttendanceOverview = {
  date: string;
  day: DayStatus;
  mode: AttendanceMode;
  totals: { sections: number; taken: number; onRoll: number; marked: number; present: number; late: number; absent: number; leave: number; attendanceRate: number | null };
  sections: SectionDay[];
  pendingCorrections: number;
  pendingLeave: number;
  alerts: MessageCounts;
  staff: StaffDayTotals | null;
};

export type TrendPoint = { date: string; marked: number; present: number; late: number; absent: number; leave: number; rate: number | null };

export type MessageStatusItem = { id: string; channel: MessageChannel; recipient: string; status: MessageStatus; detail: string | null };

export type Absentee = {
  studentId: string;
  fullName: string;
  admissionNumber: string;
  photoFileId: string | null;
  sectionId: string;
  className: string;
  status: AttendanceStatus;
  arrivedAt: string | null;
  remark: string | null;
  guardianName: string | null;
  guardianPhone: string | null;
  consecutiveAbsences: number;
  alerts: MessageStatusItem[];
};

export type MonthDay = { date: string; isSchoolDay: boolean; closedReason: string | null; taken: boolean };

export type MonthRegister = {
  sectionId: string;
  className: string;
  year: number;
  month: number;
  days: MonthDay[];
  students: { studentId: string; fullName: string; admissionNumber: string; rollNumber: string | null; marks: (AttendanceStatus | null)[]; totals: AttendanceTotals }[];
  totals: AttendanceTotals;
};

export type StudentAttendanceSummary = {
  studentId: string;
  fullName: string;
  academicYearId: string;
  academicYearName: string;
  from: string;
  to: string;
  totals: AttendanceTotals;
  months: { year: number; month: number; totals: AttendanceTotals }[];
  days: { date: string; status: AttendanceStatus; remark: string | null; arrivedAt: string | null; className: string }[];
  subjects: { subjectId: string; subjectName: string; periods: number; attended: number; percentage: number | null }[];
  leave: StudentLeave[];
  minimumPercentage: number;
  leaveCountsAsPresent: boolean;
};

export type LowAttendanceRow = {
  studentId: string;
  fullName: string;
  admissionNumber: string;
  photoFileId: string | null;
  className: string | null;
  totals: AttendanceTotals;
  lastAbsent: string | null;
  guardianName: string | null;
  guardianPhone: string | null;
};

export type LowAttendance = { from: string; to: string; below: number; students: Paged<LowAttendanceRow> };

export type BellPeriod = { sequence: number; number: number | null; name: string; startTime: string; endTime: string; isBreak: boolean };
export type BellSchedule = { id: string | null; campusId: string; campusName: string; name: string; periods: BellPeriod[] };

export type StaffRegisterRow = {
  staffId: string;
  fullName: string;
  employeeNumber: string;
  designationName: string | null;
  staffType: StaffType;
  photoFileId: string | null;
  status: StaffAttendanceStatus;
  checkIn: string | null;
  checkOut: string | null;
  remark: string | null;
  saved: boolean;
  hint: RegisterHint | null;
};

export type StaffRegister = {
  date: string;
  campusId: string;
  campusName: string;
  day: DayStatus;
  canRecord: boolean;
  isLocked: boolean;
  locksAt: string | null;
  canApprove: boolean;
  canOverrideClosedDay: boolean;
  taken: boolean;
  lastSavedAt: string | null;
  lastSavedByName: string | null;
  staff: StaffRegisterRow[];
  counts: StaffDayTotals;
};

export type StaffMark = { staffId: string; status: StaffAttendanceStatus; checkIn?: string | null; checkOut?: string | null; remark?: string | null };

export type StaffCorrection = {
  id: string;
  staffId: string;
  staffName: string;
  employeeNumber: string;
  date: string;
  fromStatus: StaffAttendanceStatus | null;
  toStatus: StaffAttendanceStatus;
  checkIn: string | null;
  checkOut: string | null;
  reason: string;
  requestedBySelf: boolean;
  status: CorrectionStatus;
  requestedByName: string | null;
  requestedAt: string;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
  canDecide: boolean;
  canWithdraw: boolean;
};

export type StaffTotals = { days: number; present: number; late: number; halfDay: number; absent: number; onLeave: number; onDuty: number; percentage: number | null };

export type StaffMonth = {
  campusId: string;
  campusName: string;
  year: number;
  month: number;
  days: MonthDay[];
  staff: { staffId: string; fullName: string; employeeNumber: string; designationName: string | null; marks: (StaffAttendanceStatus | null)[]; totals: StaffTotals }[];
};

export type LeaveType = { id: string; code: string; name: string; annualDays: number | null; isPaid: boolean; requiresDocument: boolean; isActive: boolean; sortOrder: number };
export type LeaveBalance = { leaveTypeId: string; code: string; name: string; allowance: number | null; taken: number; pending: number; remaining: number | null };

export type StaffLeave = {
  id: string;
  staffId: string;
  staffName: string;
  employeeNumber: string;
  designationName: string | null;
  leaveTypeId: string;
  leaveTypeCode: string;
  leaveTypeName: string;
  fromDate: string;
  toDate: string;
  halfDay: boolean;
  days: number;
  reason: string;
  status: LeaveStatus;
  requestedBySelf: boolean;
  requestedAt: string;
  requestedByName: string | null;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
  canDecide: boolean;
  canCancel: boolean;
};

export type TeacherRegisterTask = {
  sectionId: string;
  className: string;
  kind: "daily" | "period";
  subjectId: string | null;
  subjectName: string | null;
  period: number | null;
  taken: boolean;
  onRoll: number | null;
  absent: number | null;
  takenAt: string | null;
};

export type MyStaff = { staffId: string; fullName: string; employeeNumber: string; designationName: string | null; campusId: string; campusName: string };

export type TeacherToday = {
  date: string;
  day: DayStatus;
  mode: AttendanceMode;
  staff: MyStaff | null;
  registers: TeacherRegisterTask[];
  periods: BellPeriod[];
  myAttendance: { status: StaffAttendanceStatus | null; checkIn: string | null; checkOut: string | null } | null;
  pendingLeave: number;
  pendingCorrections: number;
};

export type MyAttendance = {
  staff: MyStaff;
  year: number;
  month: number;
  days: MonthDay[];
  marks: (StaffAttendanceStatus | null)[];
  totals: StaffTotals;
  balances: LeaveBalance[];
  leave: StaffLeave[];
  corrections: StaffCorrection[];
};

// ───────── Labels, codes and tones ─────────

export const attendanceStatuses: AttendanceStatus[] = ["Present", "Late", "Absent", "Leave"];

export const attendanceLabel: Record<AttendanceStatus, string> = { Present: "Present", Late: "Late", Absent: "Absent", Leave: "Leave" };

/** Register codes, as printed on paper registers and exports. */
export const attendanceCode: Record<AttendanceStatus, string> = { Present: "P", Late: "LT", Absent: "A", Leave: "L" };

/** Keyboard shortcuts in the register. */
export const attendanceKey: Record<AttendanceStatus, string> = { Present: "p", Late: "t", Absent: "a", Leave: "l" };

export const attendanceTone: Record<AttendanceStatus, Tone> = { Present: "success", Late: "warning", Absent: "danger", Leave: "info" };

export const staffStatuses: StaffAttendanceStatus[] = ["Present", "Late", "HalfDay", "Absent", "OnLeave", "OnDuty"];

export const staffAttendanceLabel: Record<StaffAttendanceStatus, string> = {
  Present: "Present",
  Late: "Late",
  HalfDay: "Half day",
  Absent: "Absent",
  OnLeave: "On leave",
  OnDuty: "On duty",
};

export const staffAttendanceCode: Record<StaffAttendanceStatus, string> = { Present: "P", Late: "LT", HalfDay: "HD", Absent: "A", OnLeave: "L", OnDuty: "OD" };

export const staffAttendanceTone: Record<StaffAttendanceStatus, Tone> = {
  Present: "success",
  Late: "warning",
  HalfDay: "warning",
  Absent: "danger",
  OnLeave: "info",
  OnDuty: "accent",
};

export const correctionStatusLabel: Record<CorrectionStatus, string> = { Pending: "Awaiting approval", Approved: "Approved", Rejected: "Rejected", Withdrawn: "Withdrawn" };
export const correctionStatusTone: Record<CorrectionStatus, Tone> = { Pending: "warning", Approved: "success", Rejected: "danger", Withdrawn: "neutral" };

export const leaveStatusLabel: Record<LeaveStatus, string> = { Pending: "Awaiting decision", Approved: "Approved", Rejected: "Rejected", Cancelled: "Cancelled" };
export const leaveStatusTone: Record<LeaveStatus, Tone> = { Pending: "warning", Approved: "success", Rejected: "danger", Cancelled: "neutral" };

export const leaveKindLabel: Record<StudentLeaveKind, string> = { Sick: "Sick", Family: "Family", Travel: "Travel", Religious: "Religious", Other: "Other" };

export const channelLabel: Record<MessageChannel, string> = { Sms: "SMS", WhatsApp: "WhatsApp", Email: "Email" };

export const messageStatusLabel: Record<MessageStatus, string> = {
  Pending: "Waiting to send",
  NotSent: "Not sent",
  Queued: "Accepted by provider",
  Sent: "Sent",
  Delivered: "Delivered",
  Failed: "Failed",
  Cancelled: "Cancelled",
};

export const messageStatusTone: Record<MessageStatus, Tone> = {
  Pending: "neutral",
  NotSent: "neutral",
  Queued: "info",
  Sent: "info",
  Delivered: "success",
  Failed: "danger",
  Cancelled: "neutral",
};

/** ["Sms", "WhatsApp"] → "no SMS or WhatsApp provider is connected" (mid-sentence). */
export function noProviderPhrase(channels: MessageChannel[]) {
  const order: MessageChannel[] = ["Sms", "WhatsApp", "Email"];
  const names = order.filter((c) => channels.includes(c)).map((c) => (c === "Email" ? "email" : channelLabel[c]));
  if (names.length === 0) return "no messaging provider is connected";
  const list = names.length === 1 ? names[0] : `${names.slice(0, -1).join(", ")} or ${names.at(-1)}`;
  return `no ${list} provider is connected`;
}

/** 1 → "1 day", 2.5 → "2.5 days" (leave balances can hold half days). */
export const formatDays = (days: number) => `${Number.isInteger(days) ? days : days.toFixed(1)} day${days === 1 ? "" : "s"}`;

/** "HH:mm:ss" → "8:05 am" (times are local school times). */
export function formatClock(value: string | null | undefined) {
  if (!value) return "";
  const [h, m] = value.split(":").map(Number) as [number, number];
  const suffix = h >= 12 ? "pm" : "am";
  const hour = h % 12 === 0 ? 12 : h % 12;
  return `${hour}:${String(m).padStart(2, "0")} ${suffix}`;
}

/** Adds days to an ISO date ("2026-11-03" + 1 → "2026-11-04"). */
export function addDays(iso: string, days: number) {
  const [y, m, d] = iso.split("-").map(Number) as [number, number, number];
  const date = new Date(Date.UTC(y, m - 1, d + days));
  return date.toISOString().slice(0, 10);
}

// ───────── Keys ─────────

export const attendanceKeys = {
  all: ["attendance"] as const,
  sheet: (q: object) => ["attendance", "sheet", q] as const,
  overview: (q: object) => ["attendance", "overview", q] as const,
  trend: (q: object) => ["attendance", "trend", q] as const,
  absentees: (q: object) => ["attendance", "absentees", q] as const,
  month: (q: object) => ["attendance", "month", q] as const,
  student: (id: string, yearId?: string) => ["attendance", "student", id, yearId ?? "current"] as const,
  low: (q: object) => ["attendance", "low", q] as const,
  corrections: (q: object) => ["attendance", "corrections", q] as const,
  leave: (q: object) => ["attendance", "leave", q] as const,
  bells: ["attendance", "bells"] as const,
  staffRegister: (q: object) => ["attendance", "staff-register", q] as const,
  staffMonth: (q: object) => ["attendance", "staff-month", q] as const,
  staffCorrections: (q: object) => ["attendance", "staff-corrections", q] as const,
  staffLeave: (q: object) => ["attendance", "staff-leave", q] as const,
  leaveTypes: (all: boolean) => ["attendance", "leave-types", all] as const,
  balances: (staffId: string) => ["attendance", "balances", staffId] as const,
  today: (date?: string) => ["attendance", "teach-today", date ?? "today"] as const,
  mine: (q: object) => ["attendance", "mine", q] as const,
};

type Query = Record<string, string | number | string[] | undefined>;

function useInvalidate() {
  const queryClient = useQueryClient();
  return (...keys: QueryKey[]) => keys.forEach((key) => void queryClient.invalidateQueries({ queryKey: key }));
}

const clean = (query: Record<string, unknown>) =>
  Object.fromEntries(Object.entries(query).filter(([, v]) => v !== undefined && v !== null && v !== "")) as Query;

// ───────── Registers & corrections ─────────

export const useRegisterSheet = (query: { sectionId: string; date: string; period?: number | null; subjectId?: string | null }, enabled = true) =>
  useQuery({
    queryKey: attendanceKeys.sheet(query),
    queryFn: ({ signal }) => api.get<RegisterSheet>("/attendance/registers/sheet", clean(query), signal),
    enabled,
  });

export function useSaveRegister() {
  const invalidate = useInvalidate();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { sectionId: string; date: string; period: number | null; subjectId: string | null; marks: RegisterMark[]; note: string | null; version: number | null; overrideClosedDay: boolean }) =>
      api.put<RegisterSheet>("/attendance/registers", body),
    onSuccess: (sheet) => {
      queryClient.setQueryData(attendanceKeys.sheet({ sectionId: sheet.sectionId, date: sheet.date, period: sheet.period, subjectId: sheet.subjectId }), sheet);
      invalidate(attendanceKeys.all);
    },
  });
}

export function useRequestCorrection() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ registerId, body }: { registerId: string; body: { changes: RegisterMark[]; reason: string } }) =>
      api.post<Correction>(`/attendance/registers/${registerId}/corrections`, body),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

export const useCorrections = (query: { status?: CorrectionStatus; mine?: boolean; page?: number; pageSize?: number }) =>
  useQuery({
    queryKey: attendanceKeys.corrections(query),
    queryFn: ({ signal }) => api.get<Paged<Correction>>("/attendance/corrections", clean(query), signal),
    placeholderData: keepPreviousData,
  });

export function useDecideCorrection() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, approve, note }: { id: string; approve: boolean; note?: string | null }) =>
      api.post<Correction>(`/attendance/corrections/${id}/decision`, { approve, note: note ?? null }),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

export function useWithdrawCorrection() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (id: string) => api.post<Correction>(`/attendance/corrections/${id}/withdraw`),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

// ───────── Reports ─────────

export const useAttendanceOverview = (query: { date?: string; campusId?: string }, enabled = true) =>
  useQuery({
    queryKey: attendanceKeys.overview(query),
    queryFn: ({ signal }) => api.get<AttendanceOverview>("/attendance/overview", clean(query), signal),
    placeholderData: keepPreviousData,
    enabled,
  });

export const useAttendanceTrend = (query: { from?: string; to?: string; campusId?: string }) =>
  useQuery({
    queryKey: attendanceKeys.trend(query),
    queryFn: ({ signal }) => api.get<TrendPoint[]>("/attendance/trend", clean(query), signal),
    placeholderData: keepPreviousData,
  });

export const useAbsentees = (query: { date?: string; campusId?: string; sectionId?: string }) =>
  useQuery({
    queryKey: attendanceKeys.absentees(query),
    queryFn: ({ signal }) => api.get<Absentee[]>("/attendance/absentees", clean(query), signal),
    placeholderData: keepPreviousData,
  });

export const useMonthRegister = (query: { sectionId: string; year: number; month: number }, enabled = true) =>
  useQuery({
    queryKey: attendanceKeys.month(query),
    queryFn: ({ signal }) => api.get<MonthRegister>(`/attendance/sections/${query.sectionId}/month`, { year: query.year, month: query.month }, signal),
    enabled,
    placeholderData: keepPreviousData,
  });

export const exportMonthRegister = (sectionId: string, year: number, month: number, format: "csv" | "xlsx") =>
  download(`/attendance/sections/${sectionId}/month/export`, { year, month, format });

export const useStudentAttendance = (studentId: string, academicYearId?: string) =>
  useQuery({
    queryKey: attendanceKeys.student(studentId, academicYearId),
    queryFn: ({ signal }) => api.get<StudentAttendanceSummary>(`/attendance/students/${studentId}`, clean({ academicYearId }), signal),
  });

export const useLowAttendance = (query: Record<string, string | number | undefined>) =>
  useQuery({
    queryKey: attendanceKeys.low(query),
    queryFn: ({ signal }) => api.get<LowAttendance>("/attendance/low", clean(query), signal),
    placeholderData: keepPreviousData,
  });

export const exportLowAttendance = (query: Record<string, string | number | undefined>, format: "csv" | "xlsx") =>
  download("/attendance/low/export", { ...clean(query), format });

// ───────── Student leave ─────────

export const useStudentLeave = (query: Query) =>
  useQuery({
    queryKey: attendanceKeys.leave(query),
    queryFn: ({ signal }) => api.get<Paged<StudentLeave>>("/attendance/leave", clean(query), signal),
    placeholderData: keepPreviousData,
  });

export function useCreateStudentLeave() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: { studentId: string; fromDate: string; toDate: string; kind: StudentLeaveKind; reason: string; requestedBy: string | null; approve: boolean }) =>
      api.post<LeaveDecision<StudentLeave>>("/attendance/leave", body),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

export function useDecideStudentLeave() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, approve, note }: { id: string; approve: boolean; note?: string | null }) =>
      api.post<LeaveDecision<StudentLeave>>(`/attendance/leave/${id}/decision`, { approve, note: note ?? null }),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

export function useCancelStudentLeave() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, note }: { id: string; note?: string | null }) => api.post<LeaveDecision<StudentLeave>>(`/attendance/leave/${id}/cancel`, { note: note ?? null }),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

// ───────── Bell schedules ─────────

export const useBellSchedules = (enabled = true) =>
  useQuery({ queryKey: attendanceKeys.bells, queryFn: ({ signal }) => api.get<BellSchedule[]>("/attendance/bell-schedules", undefined, signal), enabled, staleTime: 5 * 60_000 });

export function useSaveBellSchedule() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ campusId, body }: { campusId: string; body: { name: string; slots: { name: string; startTime: string; endTime: string; isBreak: boolean }[] } }) =>
      api.put<BellSchedule>(`/attendance/bell-schedules/${campusId}`, body),
    onSuccess: () => invalidate(attendanceKeys.bells, attendanceKeys.all),
  });
}

// ───────── Staff attendance ─────────

export const useStaffRegister = (query: { campusId?: string; date?: string }, enabled = true) =>
  useQuery({
    queryKey: attendanceKeys.staffRegister(query),
    queryFn: ({ signal }) => api.get<StaffRegister>("/staff-attendance/register", clean(query), signal),
    enabled: enabled && !!query.campusId,
  });

export function useSaveStaffRegister() {
  const invalidate = useInvalidate();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { campusId: string; date: string; marks: StaffMark[]; overrideClosedDay: boolean }) => api.put<StaffRegister>("/staff-attendance/register", body),
    onSuccess: (register) => {
      queryClient.setQueryData(attendanceKeys.staffRegister({ campusId: register.campusId, date: register.date }), register);
      invalidate(attendanceKeys.all);
    },
  });
}

export const useStaffMonth = (query: { campusId?: string; year: number; month: number }) =>
  useQuery({
    queryKey: attendanceKeys.staffMonth(query),
    queryFn: ({ signal }) => api.get<StaffMonth>("/staff-attendance/month", clean(query), signal),
    enabled: !!query.campusId,
    placeholderData: keepPreviousData,
  });

export const exportStaffMonth = (campusId: string, year: number, month: number, format: "csv" | "xlsx") =>
  download("/staff-attendance/month/export", { campusId, year, month, format });

export const useStaffCorrections = (query: { status?: CorrectionStatus; page?: number; pageSize?: number }) =>
  useQuery({
    queryKey: attendanceKeys.staffCorrections(query),
    queryFn: ({ signal }) => api.get<Paged<StaffCorrection>>("/staff-attendance/corrections", clean(query), signal),
    placeholderData: keepPreviousData,
  });

export function useRequestStaffCorrection() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: { staffId: string | null; date: string; status: StaffAttendanceStatus; checkIn: string | null; checkOut: string | null; reason: string }) =>
      api.post<StaffCorrection>("/staff-attendance/corrections", body),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

export function useDecideStaffCorrection() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, approve, note }: { id: string; approve: boolean; note?: string | null }) =>
      api.post<StaffCorrection>(`/staff-attendance/corrections/${id}/decision`, { approve, note: note ?? null }),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

export function useWithdrawStaffCorrection() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (id: string) => api.post<StaffCorrection>(`/staff-attendance/corrections/${id}/withdraw`),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

// ───────── Staff leave ─────────

export const useLeaveTypes = (includeInactive = false) =>
  useQuery({
    queryKey: attendanceKeys.leaveTypes(includeInactive),
    queryFn: ({ signal }) => api.get<LeaveType[]>("/staff-leave/types", includeInactive ? { includeInactive: "true" } : undefined, signal),
    staleTime: 5 * 60_000,
  });

export function useSaveLeaveType() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, body }: { id?: string; body: Omit<LeaveType, "id"> }) =>
      id ? api.put<LeaveType>(`/staff-leave/types/${id}`, body) : api.post<LeaveType>("/staff-leave/types", body),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

export function useDeleteLeaveType() {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (id: string) => api.delete(`/staff-leave/types/${id}`), onSuccess: () => invalidate(attendanceKeys.all) });
}

export const useStaffLeave = (query: Query, enabled = true) =>
  useQuery({
    queryKey: attendanceKeys.staffLeave(query),
    queryFn: ({ signal }) => api.get<Paged<StaffLeave>>("/staff-leave", clean(query), signal),
    placeholderData: keepPreviousData,
    enabled,
  });

export const useLeaveBalances = (staffId: string | null | undefined) =>
  useQuery({
    queryKey: attendanceKeys.balances(staffId ?? ""),
    queryFn: ({ signal }) => api.get<LeaveBalance[]>("/staff-leave/balances", { staffId: staffId! }, signal),
    enabled: !!staffId,
  });

export function useCreateStaffLeave() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: { staffId: string | null; leaveTypeId: string; fromDate: string; toDate: string; halfDay: boolean; reason: string; approve: boolean }) =>
      api.post<StaffLeave>("/staff-leave", body),
    onSuccess: () => invalidate(attendanceKeys.all, ["staff"]),
  });
}

export function useDecideStaffLeave() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, approve, note }: { id: string; approve: boolean; note?: string | null }) =>
      api.post<StaffLeave>(`/staff-leave/${id}/decision`, { approve, note: note ?? null }),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

export function useCancelStaffLeave() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, note }: { id: string; note?: string | null }) => api.post<StaffLeave>(`/staff-leave/${id}/cancel`, { note: note ?? null }),
    onSuccess: () => invalidate(attendanceKeys.all),
  });
}

// ───────── The teacher's day ─────────

export const useTeacherToday = (date?: string) =>
  useQuery({
    queryKey: attendanceKeys.today(date),
    queryFn: ({ signal }) => api.get<TeacherToday>("/teach/today", date ? { date } : undefined, signal),
  });

export const useMyAttendance = (query: { year?: number; month?: number }, enabled = true) =>
  useQuery({
    queryKey: attendanceKeys.mine(query),
    queryFn: ({ signal }) => api.get<MyAttendance>("/teach/my-attendance", clean(query), signal),
    enabled,
    retry: false,
    placeholderData: keepPreviousData,
  });
