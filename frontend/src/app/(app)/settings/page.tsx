"use client";

import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { Page, PageHeader } from "@/components/shell/page";
import { settingsNavigation } from "./settings-nav";

export default function SettingsIndexPage() {
  const { can, me } = useSession();
  const groups = settingsNavigation
    .map((g) => ({ ...g, items: g.items.filter((i) => i.ready && can(...(i.permissions ?? []))) }))
    .filter((g) => g.items.length > 0);

  return (
    <Page width="narrow">
      <PageHeader title="Settings" description={`How ${me.school?.name ?? "the school"} is set up: structure, policies and who has access.`} />
      <div className="grid gap-8">
        {groups.map((group) => (
          <section key={group.id} className="grid gap-2">
            <h2 className="text-sm font-medium text-ink-2">{group.label}</h2>
            <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
              {group.items.map((item) => (
                <li key={item.href}>
                  <Link href={item.href} className="group flex items-center gap-3.5 px-4 py-3 transition-colors hover:bg-sunken/60">
                    <span className="grid size-8 shrink-0 place-items-center rounded-md border border-line bg-canvas text-ink-2">
                      <item.icon className="size-4" strokeWidth={1.75} />
                    </span>
                    <span className="grid min-w-0 flex-1 gap-0.5">
                      <span className="text-base font-medium text-ink">{item.label}</span>
                      <span className="text-sm text-ink-2">{item.description}</span>
                    </span>
                    <ChevronRight className="size-4 text-ink-3 transition-transform group-hover:translate-x-0.5" />
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>
    </Page>
  );
}
