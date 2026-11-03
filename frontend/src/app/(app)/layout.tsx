import { Suspense } from "react";
import { AppShell, ShellSkeleton } from "@/components/shell/app-shell";

/**
 * Signed-in routes are rendered in the browser behind the session gate (data comes from the API with the user's
 * cookies), so there is no meaningful static shell beyond the skeleton. Navigations into this area are allowed to
 * wait for the session; pages beneath it render their own loading states.
 */
export const instant = false;

/**
 * Every signed-in administration route renders inside the application shell. The shell reads the URL (pathname,
 * route params, search params), which on dynamic routes is only known per request, so it streams in under Suspense
 * and the prerendered static shell is the same skeleton users see while their session loads.
 */
export default function AppLayout({ children }: LayoutProps<"/">) {
  return (
    <Suspense fallback={<ShellSkeleton />}>
      <AppShell>{children}</AppShell>
    </Suspense>
  );
}
