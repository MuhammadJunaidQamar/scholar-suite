"use client";

import * as React from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { usePathname, useRouter } from "next/navigation";
import { api, ApiError, onSessionExpired } from "@/lib/api/client";
import { setFormatContext } from "@/lib/format";
import type { Me, Portal } from "./types";

export const meQueryKey = ["auth", "me"] as const;

export function useMeQuery() {
  return useQuery({
    queryKey: meQueryKey,
    queryFn: ({ signal }) => api.get<Me>("/auth/me", undefined, signal),
    staleTime: 60_000,
    retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
  });
}

type SessionValue = {
  me: Me;
  /** True when the user holds any of the given permissions in the active school. */
  can: (...permissions: string[]) => boolean;
  /** True when the optional module is switched on for the school. */
  hasModule: (module: string) => boolean;
  portals: Portal[];
  signOut: () => Promise<void>;
};

const SessionContext = React.createContext<SessionValue | null>(null);

export function useSession(): SessionValue {
  const value = React.useContext(SessionContext);
  if (!value) throw new Error("useSession must be used inside <SessionProvider>");
  return value;
}

export function useCan(...permissions: string[]) {
  return useSession().can(...permissions);
}

/** Applies the school's accent colour to the whole interface (clamped for legibility in globals.css). */
function useBrandColor(color: string | null | undefined) {
  React.useEffect(() => {
    const root = document.documentElement;
    if (color && /^#[0-9a-f]{6}$/i.test(color)) root.style.setProperty("--brand", color);
    else root.style.removeProperty("--brand");
  }, [color]);
}

export function useSignOut() {
  const queryClient = useQueryClient();
  const router = useRouter();
  return React.useCallback(async () => {
    try {
      await api.post("/auth/logout", {}, { noRefresh: true });
    } finally {
      queryClient.clear();
      router.replace("/login");
    }
  }, [queryClient, router]);
}

export function SessionProvider({ me, children }: { me: Me; children: React.ReactNode }) {
  const signOut = useSignOut();
  useBrandColor(me.school?.brandColor);

  React.useMemo(() => {
    if (me.school) setFormatContext({ locale: me.school.locale, currency: me.school.currencyCode, timeZone: me.school.timezone });
  }, [me.school]);

  const value = React.useMemo<SessionValue>(() => {
    const permissions = new Set(me.access?.permissions ?? []);
    const modules = new Set(me.school?.enabledModules ?? []);
    return {
      me,
      can: (...required) => required.length === 0 || required.some((p) => permissions.has(p)),
      hasModule: (module) => ["academics", "attendance", "communication"].includes(module) || modules.has(module),
      portals: me.access?.portals ?? [],
      signOut,
    };
  }, [me, signOut]);

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

/**
 * Gate for every signed-in route: loads the session, sends signed-out users to sign-in (remembering where they
 * were going) and users with a temporary password to the change-password screen.
 */
export function useSessionGate() {
  const query = useMeQuery();
  const router = useRouter();
  const pathname = usePathname();
  const queryClient = useQueryClient();

  React.useEffect(
    () =>
      onSessionExpired(() => {
        queryClient.clear();
        router.replace(`/login?next=${encodeURIComponent(window.location.pathname + window.location.search)}&expired=1`);
      }),
    [queryClient, router],
  );

  const unauthenticated = query.error instanceof ApiError && query.error.status === 401;
  React.useEffect(() => {
    if (unauthenticated) router.replace(`/login?next=${encodeURIComponent(pathname)}`);
  }, [unauthenticated, pathname, router]);

  React.useEffect(() => {
    if (query.data?.user.mustChangePassword && pathname !== "/change-password") router.replace("/change-password");
  }, [query.data, pathname, router]);

  return query;
}
