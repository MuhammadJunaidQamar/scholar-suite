"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useTheme } from "next-themes";
import { Dialog as DialogPrimitive } from "radix-ui";
import { CornerDownLeft, LogOut, Moon, Sun } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { Kbd } from "@/components/ui/display";
import { adminNavigation, settingsItem, visibleNavigation } from "./nav";
import { settingsNavigation } from "@/app/(app)/settings/settings-nav";

/** ⌘K: jump anywhere the user is allowed to go. Record search joins here as modules provide search endpoints. */
export function CommandPalette({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const router = useRouter();
  const { can, hasModule, signOut } = useSession();
  const { resolvedTheme, setTheme } = useTheme();
  const groups = visibleNavigation(adminNavigation, can, hasModule);
  const settings = settingsNavigation.flatMap((g) => g.items).filter((i) => i.ready && can(...(i.permissions ?? [])));

  const run = (action: () => void) => {
    onOpenChange(false);
    action();
  };

  return (
    <DialogPrimitive.Root open={open} onOpenChange={onOpenChange}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-overlay data-[state=open]:animate-fade-in" />
        <DialogPrimitive.Content
          aria-describedby={undefined}
          className="fixed top-[14vh] left-1/2 z-50 w-[calc(100vw-2rem)] max-w-xl -translate-x-1/2 overflow-hidden rounded-xl border border-line bg-raised shadow-dialog data-[state=open]:animate-pop-in"
        >
          <DialogPrimitive.Title className="sr-only">Search and jump</DialogPrimitive.Title>
          <Command loop>
            <CommandInput placeholder="Where to? Search pages and actions…" autoFocus />
            <CommandList>
              <CommandEmpty>Nothing matches that.</CommandEmpty>
              {groups.map((group) => (
                <CommandGroup key={group.id} heading={group.label ?? "Go to"}>
                  {group.items.map((item) => (
                    <CommandItem key={item.href} value={`${item.label} ${item.keywords ?? ""}`} onSelect={() => run(() => router.push(item.href))}>
                      <item.icon /> {item.label}
                    </CommandItem>
                  ))}
                </CommandGroup>
              ))}
              {settings.length > 0 && (
                <CommandGroup heading={settingsItem.label}>
                  {settings.map((item) => (
                    <CommandItem key={item.href} value={`settings ${item.label} ${item.keywords ?? ""}`} onSelect={() => run(() => router.push(item.href))}>
                      <item.icon /> {item.label}
                    </CommandItem>
                  ))}
                </CommandGroup>
              )}
              <CommandGroup heading="Preferences">
                <CommandItem value="toggle theme dark light" onSelect={() => run(() => setTheme(resolvedTheme === "dark" ? "light" : "dark"))}>
                  {resolvedTheme === "dark" ? <Sun /> : <Moon />} Switch to {resolvedTheme === "dark" ? "light" : "dark"} theme
                </CommandItem>
                <CommandItem value="sign out log out" onSelect={() => run(() => void signOut())}>
                  <LogOut /> Sign out
                </CommandItem>
              </CommandGroup>
            </CommandList>
            <div className="flex items-center gap-3 border-t border-line px-4 py-2 text-xs text-ink-3">
              <span className="flex items-center gap-1"><Kbd>↑</Kbd><Kbd>↓</Kbd> move</span>
              <span className="flex items-center gap-1"><Kbd><CornerDownLeft className="size-3" /></Kbd> open</span>
              <span className="flex items-center gap-1"><Kbd>esc</Kbd> close</span>
            </div>
          </Command>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}
