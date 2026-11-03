"use client";

import * as React from "react";
import { useRouter } from "next/navigation";
import { useTheme } from "next-themes";
import { Dialog as DialogPrimitive } from "radix-ui";
import { CornerDownLeft, FileText, LogOut, Moon, Sun } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { formatPhone, useSearch, type SearchResult } from "@/lib/api/students";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { Kbd } from "@/components/ui/display";
import { useDebouncedValue } from "@/components/data-table/use-list-params";
import { RecordAvatar } from "@/components/records/record-avatar";
import { adminNavigation, settingsItem, visibleNavigation } from "./nav";
import { settingsNavigation } from "@/app/(app)/settings/settings-nav";

const kindHeading: Record<string, string> = { student: "Students", guardian: "Guardians", application: "Applications" };

/**
 * ⌘K: find a student, parent or application by name, admission number or mobile, or jump to any page the user may
 * open. Records come from the server (already limited to what the user may see); pages are filtered locally.
 */
export function CommandPalette({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const router = useRouter();
  const { can, hasModule, signOut } = useSession();
  const { resolvedTheme, setTheme } = useTheme();
  const [term, setTerm] = React.useState("");
  const query = useDebouncedValue(term.trim(), 180);
  const search = useSearch(query);
  const groups = visibleNavigation(adminNavigation, can, hasModule);
  const settings = settingsNavigation.flatMap((g) => g.items).filter((i) => i.ready && can(...(i.permissions ?? [])));
  const records = query.length >= 2 && term.trim().length >= 2 ? (search.data ?? []) : [];
  const recordGroups = Object.entries(
    records.reduce<Record<string, SearchResult[]>>((acc, r) => {
      (acc[r.kind] ??= []).push(r);
      return acc;
    }, {}),
  );

  const handleOpenChange = (next: boolean) => {
    if (!next) setTerm("");
    onOpenChange(next);
  };

  const run = (action: () => void) => {
    handleOpenChange(false);
    action();
  };

  return (
    <DialogPrimitive.Root open={open} onOpenChange={handleOpenChange}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-overlay data-[state=open]:animate-fade-in" />
        <DialogPrimitive.Content
          aria-describedby={undefined}
          className="fixed top-[14vh] left-1/2 z-50 w-[calc(100vw-2rem)] max-w-xl -translate-x-1/2 overflow-hidden rounded-xl border border-line bg-raised shadow-dialog data-[state=open]:animate-pop-in"
        >
          <DialogPrimitive.Title className="sr-only">Search and jump</DialogPrimitive.Title>
          <Command loop>
            <CommandInput placeholder="Find a student, parent or application, or a page…" autoFocus value={term} onValueChange={setTerm} />
            <CommandList>
              {records.length === 0 && (
                <CommandEmpty>{search.isFetching ? "Searching…" : "Nothing matches that."}</CommandEmpty>
              )}
              {recordGroups.map(([kind, items]) => (
                // Server results are matched on more than their text (mobile numbers, B-Form), so they skip local filtering.
                <CommandGroup key={kind} heading={kindHeading[kind] ?? kind} forceMount>
                  {items.map((r) => (
                    <CommandItem key={`${r.kind}:${r.id}`} value={`record ${r.kind} ${r.id}`} forceMount onSelect={() => run(() => router.push(r.href))}>
                      {r.kind === "application" ? <FileText /> : <RecordAvatar name={r.title} fileId={r.photoFileId} size={22} />}
                      <span className="grid min-w-0 leading-tight">
                        <span className="truncate">{r.title}</span>
                        {r.subtitle && <span className="truncate text-xs text-ink-2">{r.kind === "guardian" ? formatPhone(r.subtitle) : r.subtitle}</span>}
                      </span>
                    </CommandItem>
                  ))}
                </CommandGroup>
              ))}
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
