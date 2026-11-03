"use client";

import * as React from "react";
import Link from "next/link";
import { useTheme } from "next-themes";
import { KeyRound, LogOut, Menu, Monitor, Moon, Search, Sun, UserRound } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { useMounted } from "@/hooks/use-mounted";
import { Avatar, Kbd } from "@/components/ui/display";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from "@/components/ui/menu";

function ThemeItems() {
  const { theme, setTheme } = useTheme();
  const mounted = useMounted();
  const options = [
    { value: "light", label: "Light", icon: Sun },
    { value: "dark", label: "Dark", icon: Moon },
    { value: "system", label: "Match system", icon: Monitor },
  ];
  return (
    <>
      {options.map(({ value, label, icon: Icon }) => (
        <DropdownMenuItem key={value} onSelect={() => setTheme(value)}>
          <Icon /> {label}
          {mounted && theme === value && <span className="ml-auto size-1.5 rounded-full bg-accent" aria-label="selected" />}
        </DropdownMenuItem>
      ))}
    </>
  );
}

export function UserMenu() {
  const { me, signOut } = useSession();
  const { resolvedTheme } = useTheme();
  const mounted = useMounted();
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button type="button" aria-label="Account" className="rounded-full outline-offset-2">
          <Avatar name={me.user.fullName} size={28} />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="w-64">
        <div className="flex items-center gap-2.5 px-2 py-2">
          <Avatar name={me.user.fullName} size={32} />
          <div className="grid min-w-0 leading-tight">
            <span className="truncate text-sm font-semibold">{me.user.fullName}</span>
            <span className="truncate text-xs text-ink-2">{me.user.email ?? me.user.userName}</span>
          </div>
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link href="/account"><UserRound /> Your account</Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <Link href="/account/security"><KeyRound /> Password & sessions</Link>
        </DropdownMenuItem>
        <DropdownMenuSub>
          <DropdownMenuSubTrigger>
            {mounted && resolvedTheme === "dark" ? <Moon /> : <Sun />} Theme
          </DropdownMenuSubTrigger>
          <DropdownMenuSubContent>
            <DropdownMenuLabel>Appearance</DropdownMenuLabel>
            <ThemeItems />
          </DropdownMenuSubContent>
        </DropdownMenuSub>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => void signOut()}>
          <LogOut /> Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export function Topbar({ onOpenSearch, onOpenMenu }: { onOpenSearch: () => void; onOpenMenu: () => void }) {
  const mounted = useMounted();
  const isMac = mounted && /Mac|iPhone|iPad/.test(navigator.platform);
  return (
    <header className="sticky top-0 z-30 flex h-[52px] items-center gap-3 border-b border-line bg-canvas px-4 lg:px-6">
      <Button variant="ghost" size="icon" className="md:hidden" aria-label="Open navigation" onClick={onOpenMenu}>
        <Menu />
      </Button>
      <button
        type="button"
        onClick={onOpenSearch}
        className="flex h-8 w-full max-w-md items-center gap-2 rounded-md border border-line bg-surface px-2.5 text-sm text-ink-3 shadow-[0_1px_1px_oklch(0.2_0.01_70/0.03)] transition-colors hover:border-line-strong hover:text-ink-2"
      >
        <Search className="size-4" />
        <span className="flex-1 text-left">Search or jump to…</span>
        <span className="hidden items-center gap-0.5 sm:flex">
          <Kbd>{isMac ? "⌘" : "Ctrl"}</Kbd>
          <Kbd>K</Kbd>
        </span>
      </button>
      <div className="ml-auto flex items-center gap-1.5">
        <UserMenu />
      </div>
    </header>
  );
}
