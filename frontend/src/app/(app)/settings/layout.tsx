"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { useSession } from "@/lib/auth/session";
import { settingsNavigation } from "./settings-nav";

export default function SettingsLayout({ children }: LayoutProps<"/settings">) {
  const pathname = usePathname();
  const { can } = useSession();
  const isIndex = pathname === "/settings";
  const groups = settingsNavigation
    .map((g) => ({ ...g, items: g.items.filter((i) => i.ready && can(...(i.permissions ?? []))) }))
    .filter((g) => g.items.length > 0);

  if (isIndex) return <>{children}</>;

  return (
    <div className="mx-auto flex w-full max-w-[1400px] gap-8 px-4 sm:px-6 lg:px-8">
      <nav aria-label="Settings" className="sticky top-[52px] hidden max-h-[calc(100dvh-52px)] w-52 shrink-0 overflow-y-auto py-7 lg:block">
        <Link href="/settings" className="mb-4 block px-2 text-sm font-medium text-ink-2 hover:text-ink">
          ← All settings
        </Link>
        {groups.map((group) => (
          <div key={group.id} className="mb-5 grid gap-px">
            <p className="px-2 pb-1.5 text-xs font-medium text-ink-3">{group.label}</p>
            {group.items.map((item) => {
              const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  aria-current={active ? "page" : undefined}
                  className={cn(
                    "rounded-md px-2 py-1.5 text-[13.5px] text-ink-2 hover:bg-sunken hover:text-ink",
                    active && "bg-sunken font-medium text-ink",
                  )}
                >
                  {item.label}
                </Link>
              );
            })}
          </div>
        ))}
      </nav>
      <div className="min-w-0 flex-1 pb-16">{children}</div>
    </div>
  );
}
