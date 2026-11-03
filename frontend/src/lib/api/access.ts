import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Paged } from "./client";
import type { Portal } from "@/lib/auth/types";

export type PermissionDefinition = { key: string; label: string; description: string; sensitive: boolean };
export type PermissionGroup = { key: string; label: string; module: string | null; permissions: PermissionDefinition[] };

export type RoleListItem = {
  id: string;
  key: string | null;
  name: string;
  description: string | null;
  portal: Portal;
  isSystem: boolean;
  isLocked: boolean;
  permissionCount: number;
  memberCount: number;
};

export type RoleDetail = Omit<RoleListItem, "permissionCount" | "memberCount"> & { permissions: string[] };

export type RoleAssignment = { roleId: string; roleName: string; roleKey: string | null; campusId: string | null; campusName: string | null };

export type Member = {
  userId: string;
  fullName: string;
  email: string | null;
  userName: string;
  status: "Active" | "Invited" | "Disabled";
  membershipStatus: "Active" | "Suspended";
  lastLoginAt: string | null;
  roles: RoleAssignment[];
};

export type Campus = { id: string; code: string; name: string; isMain: boolean; isActive: boolean; city: string | null };

export type SaveRole = { name: string; description?: string | null; portal: Portal; permissions: string[] };
export type InviteMember = { firstName: string; lastName: string; email: string; roles: { roleId: string; campusId: string | null }[] };

export const accessKeys = {
  roles: ["access", "roles"] as const,
  role: (id: string) => ["access", "roles", id] as const,
  permissions: ["access", "permissions"] as const,
  members: (query: object) => ["access", "members", query] as const,
  campuses: ["org", "campuses"] as const,
};

export function usePermissionCatalog() {
  return useQuery({ queryKey: accessKeys.permissions, queryFn: () => api.get<PermissionGroup[]>("/access/permissions"), staleTime: Infinity });
}

export function useRoles() {
  return useQuery({ queryKey: accessKeys.roles, queryFn: () => api.get<RoleListItem[]>("/access/roles") });
}

export function useRole(id: string) {
  return useQuery({ queryKey: accessKeys.role(id), queryFn: () => api.get<RoleDetail>(`/access/roles/${id}`) });
}

export function useCampuses() {
  return useQuery({ queryKey: accessKeys.campuses, queryFn: () => api.get<Campus[]>("/org/campuses"), staleTime: 5 * 60_000 });
}

export function useMembers(query: Record<string, unknown>) {
  return useQuery({
    queryKey: accessKeys.members(query),
    queryFn: ({ signal }) => api.get<Paged<Member>>("/access/members", query as Record<string, string>, signal),
    placeholderData: (previous) => previous,
  });
}

export function useSaveRole(id?: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveRole) => (id ? api.put<RoleDetail>(`/access/roles/${id}`, body) : api.post<RoleDetail>("/access/roles", body)),
    onSuccess: (role) => {
      queryClient.setQueryData(accessKeys.role(role.id), role);
      void queryClient.invalidateQueries({ queryKey: accessKeys.roles });
      void queryClient.invalidateQueries({ queryKey: ["auth", "me"] });
    },
  });
}

export function useDeleteRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/access/roles/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: accessKeys.roles }),
  });
}

/** What happened to an invitation: never "Sent" unless the e-mail provider accepted it. */
export type DeliveryState = "NotSent" | "Queued" | "Sent";

/** How a new account's first sign-in was handed over (an invitation, or a one-time password shown once). */
export type AccountHandover = { invitation: DeliveryState; sentTo: string | null; temporaryPassword: string | null; detail: string | null };

export type InviteResult = { member: Member; handover: AccountHandover | null };

/** Issues a one-time password (shown once) and signs the person out everywhere. */
export function useResetMemberPassword() {
  return useMutation({
    mutationFn: (userId: string) => api.post<AccountHandover>(`/access/members/${userId}/reset-password`),
  });
}

export function useInviteMember() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: InviteMember) => api.post<InviteResult>("/access/members", body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["access", "members"] });
      void queryClient.invalidateQueries({ queryKey: accessKeys.roles });
    },
  });
}

export function useUpdateMemberRoles() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, roles }: { userId: string; roles: { roleId: string; campusId: string | null }[] }) =>
      api.put<Member>(`/access/members/${userId}/roles`, { roles }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["access", "members"] });
      void queryClient.invalidateQueries({ queryKey: accessKeys.roles });
    },
  });
}

export function useSetMemberSuspended() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, suspended }: { userId: string; suspended: boolean }) =>
      api.post(`/access/members/${userId}/${suspended ? "suspend" : "reactivate"}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["access", "members"] }),
  });
}
