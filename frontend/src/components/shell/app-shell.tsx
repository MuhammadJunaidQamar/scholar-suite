"use client";

import * as React from "react";
import { Dialog as DialogPrimitive } from "radix-ui";
import { SessionProvider, useSessionGate } from "@/lib/auth/session";
import { ApiError } from "@/lib/api/client";
import { ErrorState } from "@/components/ui/states";
import { Skeleton } from "@/components/ui/display";
import { Sidebar } from "./sidebar";
import { Topbar } from "./topbar";
import { CommandPalette } from "./command-palette";

const COLLAPSE_KEY = "ss.sidebar.collapsed";

/** What a signed-in page looks like before the session (and the URL, on dynamic routes) is known. */
export function ShellSkeleton() {
  return (
    <div className="flex min-h-dvh" aria-busy>
      <div className="hidden w-[248px] border-r border-line bg-sunken/80 p-3 md:block">
        <Skeleton className="mb-6 h-9" />
        {Array.from({ length: 9 }).map((_, i) => (
          <Skeleton key={i} className="mb-2 h-6" style={{ width: `${60 + ((i * 17) % 35)}%` }} />
        ))}
      </div>
      <div className="flex-1">
        <div className="h-[52px] border-b border-line" />
        <div className="mx-auto max-w-[1400px] px-6 py-8">
          <Skeleton className="mb-3 h-4 w-40" />
          <Skeleton className="mb-8 h-8 w-72" />
          <Skeleton className="h-64" />
        </div>
      </div>
    </div>
  );
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const { data: me, error, refetch } = useSessionGate();
  const [collapsed, setCollapsed] = React.useState(false);
  const [mobileOpen, setMobileOpen] = React.useState(false);
  const [searchOpen, setSearchOpen] = React.useState(false);

  React.useEffect(() => {
    try {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setCollapsed(window.localStorage.getItem(COLLAPSE_KEY) === "1");
    } catch {
      /* storage unavailable */
    }
  }, []);

  const toggle = React.useCallback(() => {
    setCollapsed((value) => {
      try {
        window.localStorage.setItem(COLLAPSE_KEY, value ? "0" : "1");
      } catch {
        /* ignore */
      }
      return !value;
    });
  }, []);

  React.useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement;
      const typing = target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName);
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setSearchOpen((v) => !v);
      } else if (!typing && event.key === "[" && !event.metaKey && !event.ctrlKey) {
        toggle();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [toggle]);

  if (!me) {
    if (error && !(error instanceof ApiError && error.status === 401)) {
      return <ErrorState className="min-h-dvh" error={error} onRetry={() => void refetch()} title="We couldn't open your workspace" />;
    }
    return <ShellSkeleton />;
  }

  return (
    <SessionProvider me={me}>
      <a href="#main" className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-[70] focus:rounded-md focus:bg-raised focus:px-3 focus:py-2 focus:shadow-popover">
        Skip to content
      </a>
      <div className="flex min-h-dvh">
        <aside className="sticky top-0 hidden h-dvh shrink-0 border-r border-line md:block">
          <Sidebar collapsed={collapsed} onToggle={toggle} />
        </aside>
        <DialogPrimitive.Root open={mobileOpen} onOpenChange={setMobileOpen}>
          <DialogPrimitive.Portal>
            <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-overlay md:hidden" />
            <DialogPrimitive.Content aria-describedby={undefined}
              className="fixed inset-y-0 left-0 z-50 w-[280px] border-r border-line bg-canvas shadow-dialog md:hidden data-[state=open]:animate-fade-in">
              <DialogPrimitive.Title className="sr-only">Navigation</DialogPrimitive.Title>
              <Sidebar collapsed={false} mobile onNavigate={() => setMobileOpen(false)} />
            </DialogPrimitive.Content>
          </DialogPrimitive.Portal>
        </DialogPrimitive.Root>
        <div className="flex min-w-0 flex-1 flex-col">
          <Topbar onOpenSearch={() => setSearchOpen(true)} onOpenMenu={() => setMobileOpen(true)} />
          <main id="main" className="min-w-0 flex-1">
            {children}
          </main>
        </div>
      </div>
      <CommandPalette open={searchOpen} onOpenChange={setSearchOpen} />
    </SessionProvider>
  );
}
