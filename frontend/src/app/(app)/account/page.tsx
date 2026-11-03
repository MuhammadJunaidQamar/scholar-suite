"use client";

import Link from "next/link";
import { useSession } from "@/lib/auth/session";
import { portalLabel } from "@/lib/auth/types";
import { useRoles } from "@/lib/api/access";
import { Page, PageHeader } from "@/components/shell/page";
import { FormSection } from "@/components/ui/field";
import { Avatar, Badge, DetailList } from "@/components/ui/display";
import { Button } from "@/components/ui/button";

export default function AccountPage() {
  const { me, can } = useSession();
  const roles = useRoles();
  const canSeeRoles = can("access.roles.view");
  const roleNames = canSeeRoles
    ? (roles.data ?? []).filter((r) => r.key && me.access?.roles.includes(r.key)).map((r) => r.name)
    : me.access?.roles ?? [];

  return (
    <Page width="narrow">
      <PageHeader title="Your account" description="How you appear to colleagues and what you can access." />
      <FormSection title="Profile" description="Your name and sign-in details. Ask an administrator to change your email.">
        <div className="flex items-center gap-4">
          <Avatar name={me.user.fullName} size={56} />
          <div className="grid gap-0.5">
            <p className="text-lg font-semibold">{me.user.fullName}</p>
            <p className="text-sm text-ink-2">{me.user.email ?? me.user.userName}</p>
          </div>
        </div>
        <DetailList items={[
          { label: "Username", value: me.user.userName },
          { label: "Email", value: me.user.email },
          { label: "School", value: me.school?.name },
        ]} />
      </FormSection>
      <FormSection title="Access" description="Roles decide what you can see and do. Administrators manage them in Settings › Users.">
        <div className="flex flex-wrap gap-1.5">
          {roleNames.length > 0 ? roleNames.map((name) => <Badge key={name}>{name.replaceAll("_", " ")}</Badge>) : <span className="text-ink-3">No roles</span>}
        </div>
        <DetailList items={[
          { label: "Portals", value: me.access?.portals.map((p) => portalLabel[p]).join(", ") },
          { label: "Campuses", value: me.access?.campusIds === null ? "All campuses" : `${me.access?.campusIds?.length ?? 0} campus(es)` },
          { label: "Permissions", value: <span className="tabular">{me.access?.permissions.length ?? 0}</span> },
        ]} />
      </FormSection>
      <FormSection title="Security" description="Password and signed-in devices.">
        <div><Button asChild variant="secondary"><Link href="/account/security">Manage password & sessions</Link></Button></div>
      </FormSection>
    </Page>
  );
}
