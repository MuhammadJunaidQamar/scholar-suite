export type Portal = "Admin" | "Teacher" | "Family" | "Student";

export type AuthUser = {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string | null;
  userName: string;
  mustChangePassword: boolean;
  isPlatformAdmin: boolean;
  schoolId: string | null;
};

export type SchoolSummary = {
  id: string;
  code: string;
  name: string;
  shortName: string | null;
  logoFileId: string | null;
  brandColor: string | null;
};

export type SchoolContext = SchoolSummary & {
  timezone: string;
  currencyCode: string;
  locale: string;
  enabledModules: string[];
};

export type Access = {
  permissions: string[];
  portals: Portal[];
  roles: string[];
  campusIds: string[] | null;
};

export type Me = {
  user: AuthUser;
  school: SchoolContext | null;
  schools: SchoolSummary[];
  access: Access | null;
};

export type BrowserSession = { user: AuthUser; accessTokenExpiresAt: string };

export type SessionInfo = {
  id: string;
  clientType: "Browser" | "Mobile";
  userAgent: string | null;
  ipAddress: string | null;
  createdAt: string;
  lastSeenAt: string;
  expiresAt: string;
  isCurrent: boolean;
};

export const portalHome: Record<Portal, string> = {
  Admin: "/",
  Teacher: "/teach",
  Family: "/family",
  Student: "/learn",
};

export const portalLabel: Record<Portal, string> = {
  Admin: "Administration",
  Teacher: "Teacher portal",
  Family: "Family portal",
  Student: "Student portal",
};
