"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";

const tabs = [
  { href: "/admissions", label: "Applications" },
  { href: "/admissions/inquiries", label: "Inquiries" },
];

/** Applications · Inquiries: the two halves of admissions, as page-level tabs. */
export function AdmissionsNav() {
  const pathname = usePathname();
  return (
    <nav aria-label="Admissions" className="flex gap-5 border-b border-line">
      {tabs.map((t) => {
        const active = t.href === "/admissions" ? pathname === "/admissions" || pathname.startsWith("/admissions/applications") : pathname.startsWith(t.href);
        return (
          <Link key={t.href} href={t.href} aria-current={active ? "page" : undefined}
            className={cn("relative -mb-px inline-flex h-9 items-center border-b-2 border-transparent text-sm font-medium text-ink-2 hover:text-ink",
              active && "border-ink text-ink")}>
            {t.label}
          </Link>
        );
      })}
    </nav>
  );
}
