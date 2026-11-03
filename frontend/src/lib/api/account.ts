import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type { SessionInfo } from "@/lib/auth/types";

export function useSessions() {
  return useQuery({ queryKey: ["auth", "sessions"], queryFn: () => api.get<SessionInfo[]>("/auth/sessions") });
}

export function useRevokeSession() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.delete(`/auth/sessions/${id}`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["auth", "sessions"] }),
  });
}

/** "Chrome on macOS" from a user agent string — enough to recognise a device. */
export function describeDevice(userAgent: string | null): string {
  if (!userAgent) return "Unknown device";
  const browser = /Edg\//.test(userAgent) ? "Edge" : /Chrome\//.test(userAgent) ? "Chrome" : /Firefox\//.test(userAgent) ? "Firefox"
    : /Safari\//.test(userAgent) ? "Safari" : "Browser";
  const os = /Windows/.test(userAgent) ? "Windows" : /Android/.test(userAgent) ? "Android" : /iPhone|iPad/.test(userAgent) ? "iOS"
    : /Mac OS X/.test(userAgent) ? "macOS" : /Linux/.test(userAgent) ? "Linux" : "an unknown system";
  return `${browser} on ${os}`;
}
