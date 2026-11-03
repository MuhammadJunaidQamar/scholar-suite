"use client";

import * as React from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { Check, ChevronsUpDown, PanelLeftClose, PanelLeftOpen, Repeat2 } from "lucide-react";
import { cn, initials } from "@/lib/utils";
import { api } from "@/lib/api/client";
import { productName } from "@/lib/brand";
import { useSession } from "@/lib/auth/session";
import { portalHome, portalLabel, type Portal } from "@/lib/auth/types";
import { Tooltip, DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { activeHref, adminNavigation, settingsItem, visibleNavigation, type NavItem } from "./nav";

/** The school's public crest URL; the version makes a new upload show at once despite caching. */
export const crestUrl = (code: string, logoFileId: string) => `/api/public/schools/${encodeURIComponent(code)}/logo?v=${logoFileId}`;

/** The school's uploaded crest, or its initials on the accent colour. */
export function SchoolCrest({ name, size = 30, className, logo }: { name: string; size?: number; className?: string; logo?: string | null }) {
  const [failed, setFailed] = React.useState<string | null>(null);
  if (logo && failed !== logo) {
    return (
      // eslint-disable-next-line @next/next/no-img-element -- served by the API, outside Next's image optimiser
      <img src={logo} alt="" aria-hidden width={size} height={size} onError={() => setFailed(logo)}
        className={cn("shrink-0 rounded-md object-contain", className)} style={{ width: size, height: size }} />
    );
  }
  return (
    <span
      aria-hidden
      className={cn("grid shrink-0 place-items-center rounded-md bg-accent font-display text-accent-ink shadow-[inset_0_0_0_1px_oklch(0_0_0/0.08)]", className)}
      style={{ width: size, height: size, fontSize: Math.round(size * 0.42) }}
    >
      {initials(name)}
    </span>
  );
}

function SchoolSwitcher({ collapsed }: { collapsed: boolean }) {
  const { me } = useSession();
  const queryClient = useQueryClient();
  const router = useRouter();
  const school = me.school;
  if (!school) return null;

  const switchTo = async (schoolId: string) => {
    try {
      await api.post("/auth/switch-school", { schoolId });
      queryClient.clear();
      router.replace("/");
    } catch (error) {
      toast.error(error, "Could not switch school");
    }
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className={cn(
            "flex w-full items-center gap-2.5 rounded-lg p-1.5 text-left transition-colors hover:bg-line/50",
            collapsed && "justify-center",
          )}
        >
          <SchoolCrest name={school.shortName ?? school.name} logo={school.logoFileId ? crestUrl(school.code, school.logoFileId) : null} />
          {!collapsed && (
            <>
              <span className="grid min-w-0 flex-1 leading-tight">
                <span className="truncate text-sm font-semibold text-ink">{school.shortName ?? school.name}</span>
                <span className="truncate text-xs text-ink-3">{productName}</span>
              </span>
              <ChevronsUpDown className="size-3.5 shrink-0 text-ink-3" />
            </>
          )}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-64">
        <DropdownMenuLabel>Schools</DropdownMenuLabel>
        {me.schools.map((s) => (
          <DropdownMenuItem key={s.id} onSelect={() => s.id !== school.id && switchTo(s.id)}>
            <SchoolCrest name={s.shortName ?? s.name} size={20} logo={s.logoFileId ? crestUrl(s.code, s.logoFileId) : null} />
            <span className="flex-1 truncate">{s.name}</span>
            {s.id === school.id && <Check className="!text-accent" />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function NavLink({ item, active, collapsed, onNavigate }: { item: NavItem; active: boolean; collapsed: boolean; onNavigate?: () => void }) {
  const Icon = item.icon;
  const link = (
    <Link
      href={item.href}
      onNavigate={onNavigate}
      aria-current={active ? "page" : undefined}
      className={cn(
        "group flex h-8 items-center gap-2.5 rounded-md px-2 text-[13.5px] text-ink-2 transition-colors",
        "hover:bg-line/45 hover:text-ink",
        active && "bg-surface font-medium text-ink shadow-[0_1px_2px_oklch(0.2_0.01_70/0.06),0_0_0_1px_var(--line)] hover:bg-surface",
        collapsed && "justify-center px-0",
      )}
    >
      <Icon className={cn("size-4 shrink-0 text-ink-3 transition-colors group-hover:text-ink-2", active && "text-accent group-hover:text-accent")}
        strokeWidth={1.75} />
      {!collapsed && <span className="truncate">{item.label}</span>}
    </Link>
  );
  return collapsed ? <Tooltip content={item.label} side="right">{link}</Tooltip> : link;
}

function PortalSwitcher({ collapsed }: { collapsed: boolean }) {
  const { portals } = useSession();
  const router = useRouter();
  const others = portals.filter((p) => p !== "Admin");
  if (others.length === 0) return null;
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button type="button" className={cn("flex h-8 w-full items-center gap-2.5 rounded-md px-2 text-[13.5px] text-ink-2 hover:bg-line/45 hover:text-ink", collapsed && "justify-center px-0")}>
          <Repeat2 className="size-4 text-ink-3" strokeWidth={1.75} />
          {!collapsed && <span>Switch portal</span>}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" side="top" className="w-56">
        <DropdownMenuLabel>Open another portal</DropdownMenuLabel>
        {portals.map((portal: Portal) => (
          <DropdownMenuItem key={portal} onSelect={() => router.push(portalHome[portal])}>
            {portalLabel[portal]}
          </DropdownMenuItem>
        ))}
        <DropdownMenuSeparator />
        <p className="px-2 py-1.5 text-xs text-ink-3">Each portal shows only what that role needs.</p>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export function Sidebar({ collapsed, onToggle, onNavigate, mobile }: {
  collapsed: boolean;
  onToggle?: () => void;
  onNavigate?: () => void;
  mobile?: boolean;
}) {
  const pathname = usePathname();
  const { can, hasModule } = useSession();
  const groups = visibleNavigation(adminNavigation, can, hasModule);
  const current = activeHref(adminNavigation, pathname);
  const showSettings = can(...(settingsItem.permissions ?? []));

  return (
    <nav aria-label="Main" className={cn("flex h-full flex-col gap-1 bg-sunken/80", mobile ? "w-full" : collapsed ? "w-[60px]" : "w-[248px]")}>
      <div className={cn("px-2.5 pt-2.5 pb-2", collapsed && "px-2")}>
        <SchoolSwitcher collapsed={collapsed} />
      </div>
      <div className="flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto px-2.5 pb-3 scrollbar-thin">
        {groups.map((group) => (
          <div key={group.id} className="grid gap-px">
            {group.label && !collapsed && <p className="px-2 pt-1 pb-1.5 text-xs font-medium text-ink-3">{group.label}</p>}
            {group.label && collapsed && <div className="mx-auto my-1.5 h-px w-5 bg-line-strong" aria-hidden />}
            {group.items.map((item) => (
              <NavLink key={item.href} item={item} active={current === item.href} collapsed={collapsed} onNavigate={onNavigate} />
            ))}
          </div>
        ))}
      </div>
      <div className="grid gap-px border-t border-line px-2.5 py-2.5">
        {showSettings && <NavLink item={settingsItem} active={current === settingsItem.href} collapsed={collapsed} onNavigate={onNavigate} />}
        <PortalSwitcher collapsed={collapsed} />
        {onToggle && (
          <Tooltip content={collapsed ? "Expand sidebar  [" : "Collapse sidebar  ["} side="right">
            <button type="button" onClick={onToggle} aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
              className={cn("flex h-8 items-center gap-2.5 rounded-md px-2 text-[13.5px] text-ink-2 hover:bg-line/45 hover:text-ink", collapsed && "justify-center px-0")}>
              {collapsed ? <PanelLeftOpen className="size-4 text-ink-3" strokeWidth={1.75} /> : <PanelLeftClose className="size-4 text-ink-3" strokeWidth={1.75} />}
              {!collapsed && <span>Collapse</span>}
            </button>
          </Tooltip>
        )}
      </div>
    </nav>
  );
}
