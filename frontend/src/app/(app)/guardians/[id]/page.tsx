"use client";

import * as React from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { KeyRound, Mail, MessageCircle, MoreHorizontal, Pencil, Phone, Smartphone, Trash2, UserCheck } from "lucide-react";
import { ApiError } from "@/lib/api/client";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import type { AccountHandover } from "@/lib/api/access";
import {
  formatNationalId,
  formatPhone,
  studentStatusLabel,
  useDeleteGuardian,
  useEnablePortal,
  useGuardian,
  useResetPortalPassword,
} from "@/lib/api/students";
import { Breadcrumbs, Page, Panel } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Badge, DetailList, Skeleton, StatusBadge } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { ConfirmDialog, Dialog, DialogBody, DialogContent, DialogFooter, DialogHeader } from "@/components/ui/dialog";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/menu";
import { toast } from "@/components/ui/toaster";
import { RecordAvatar } from "@/components/records/record-avatar";
import { AccountHandoverNotice } from "@/components/records/account-handover";
import { GuardianFormDialog } from "@/components/students/guardian-form-dialog";

export default function GuardianPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { can } = useSession();
  const guardian = useGuardian(id);
  const enable = useEnablePortal();
  const reset = useResetPortalPassword();
  const remove = useDeleteGuardian();
  const [editing, setEditing] = React.useState(false);
  const [confirm, setConfirm] = React.useState<"enable" | "reset" | "delete" | null>(null);
  const [handover, setHandover] = React.useState<{ handover: AccountHandover; loginName: string; reset: boolean } | null>(null);

  if (guardian.isPending) {
    return <Page><Skeleton className="mt-7 h-4 w-40" /><Skeleton className="mt-6 h-16 w-96" /><Skeleton className="mt-8 h-80" /></Page>;
  }
  if (guardian.error) {
    const missing = guardian.error instanceof ApiError && guardian.error.status === 404;
    return (
      <Page>
        {missing ? (
          <EmptyState title="Guardian not found" description="The record may have been removed, or the children are at a campus you can't see."
            action={<Button asChild><Link href="/guardians">Back to guardians</Link></Button>} />
        ) : (
          <ErrorState error={guardian.error} onRetry={() => void guardian.refetch()} />
        )}
      </Page>
    );
  }

  const g = guardian.data;
  const portal = can(P.Guardians.ManagePortal);
  const first = g.firstName;
  const address = [g.address.line1, g.address.line2, g.address.city, g.address.province].filter(Boolean).join(", ");
  const channels = [
    { on: g.notifyBySms, label: "SMS", icon: Smartphone },
    { on: g.notifyByWhatsApp, label: "WhatsApp", icon: MessageCircle },
    { on: g.notifyByEmail, label: "Email", icon: Mail },
  ];

  return (
    <Page>
      <Breadcrumbs className="pt-6" items={[{ label: "Guardians", href: "/guardians" }, { label: g.fullName }]} />
      <header className="flex flex-wrap items-start gap-x-5 gap-y-4 pt-4 pb-6">
        <RecordAvatar name={g.fullName} fileId={null} size={64} />
        <div className="grid min-w-0 flex-1 gap-1.5">
          <h1 className="font-display text-2xl text-ink">{g.fullName}</h1>
          <p className="flex flex-wrap items-center gap-x-2 gap-y-0.5 text-sm text-ink-2">
            <a href={`tel:${g.phone}`} className="text-ink tabular hover:underline">{formatPhone(g.phone)}</a>
            {g.email && <><span aria-hidden className="text-ink-3">·</span><a href={`mailto:${g.email}`} className="hover:text-ink">{g.email}</a></>}
            {g.occupation && <><span aria-hidden className="text-ink-3">·</span><span>{g.occupation}</span></>}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {can(P.Guardians.Update) && <Button variant="secondary" onClick={() => setEditing(true)}><Pencil /> Edit</Button>}
          {can(P.Guardians.Delete) && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="secondary" size="icon" aria-label="More actions"><MoreHorizontal /></Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent>
                <DropdownMenuItem tone="danger" disabled={g.children.length > 0} onSelect={() => setConfirm("delete")}>
                  <Trash2 /> {g.children.length > 0 ? "Delete (unlink children first)" : "Delete record…"}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>
      </header>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <div className="grid content-start gap-6">
          <Panel title="Children" description="Relationship and roles are set on each child's record." flush>
            {g.children.length === 0 ? (
              <p className="px-4 py-4 text-sm text-ink-2">Not linked to any student yet. Add them from a student&apos;s Family tab.</p>
            ) : (
              <ul className="divide-y divide-line">
                {g.children.map((c) => (
                  <li key={c.linkId}>
                    <Link href={`/students/${c.studentId}?tab=family`} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3 hover:bg-sunken/60">
                      <RecordAvatar name={c.fullName} fileId={c.photoFileId} size={34} />
                      <span className="grid min-w-44 flex-1 leading-tight">
                        <span className="font-medium text-ink">{c.fullName}</span>
                        <span className="text-xs text-ink-2">
                          <span className="font-mono">{c.admissionNumber}</span> · {c.status === "Active" ? (c.className ?? "Not placed") : studentStatusLabel[c.status]}
                        </span>
                      </span>
                      <span className="flex flex-wrap gap-1.5">
                        <Badge>{c.relationship}</Badge>
                        {c.isPrimary && <Badge tone="accent">Primary contact</Badge>}
                        {c.isFeePayer && <Badge>Pays fees</Badge>}
                        {!c.portalAccess && <Badge tone="warning">Hidden from portal</Badge>}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </Panel>
          <Panel title="Contact">
            <DetailList columns={2} items={[
              { label: "Mobile", value: <a href={`tel:${g.phone}`} className="tabular hover:underline">{formatPhone(g.phone)}</a> },
              { label: "Other number", value: g.alternatePhone ? <span className="tabular">{formatPhone(g.alternatePhone)}</span> : null },
              { label: "Email", value: g.email },
              { label: "CNIC", value: g.nationalId ? <span className="tabular">{formatNationalId(g.nationalId)}</span> : null },
              { label: "Occupation", value: g.occupation },
              { label: "Employer", value: g.employer },
              { label: "Address", value: address || null },
              { label: "Language", value: g.preferredLanguage === "ur" ? "Urdu" : g.preferredLanguage === "en" ? "English" : null },
            ]} />
          </Panel>
        </div>

        <div className="grid content-start gap-6">
          <Panel title="Family portal">
            {g.hasLogin ? (
              <div className="grid gap-3">
                <StatusBadge tone="success">Has a login</StatusBadge>
                <p className="text-sm text-ink-2">Signs in as <span className="font-mono text-ink">{g.loginName}</span>, and sees the children marked for the portal.</p>
                {portal && <Button variant="secondary" className="w-fit" onClick={() => setConfirm("reset")}><KeyRound /> Reset password…</Button>}
              </div>
            ) : (
              <div className="grid gap-3">
                <p className="text-sm text-ink-2">No login yet. With one, {first} can follow attendance, results and fees for the children marked for the portal.</p>
                {portal && <Button variant="primary" className="w-fit" onClick={() => setConfirm("enable")}><UserCheck /> Give portal access</Button>}
              </div>
            )}
          </Panel>
          <Panel title="Messages from school" flush>
            <ul className="divide-y divide-line">
              {channels.map((c) => (
                <li key={c.label} className="flex items-center justify-between gap-3 px-4 py-2.5 text-sm">
                  <span className="flex items-center gap-2 text-ink"><c.icon className="size-4 text-ink-3" aria-hidden /> {c.label}</span>
                  <span className={c.on ? "text-ink" : "text-ink-3"}>{c.on ? "Yes" : "No"}</span>
                </li>
              ))}
            </ul>
          </Panel>
          <a href={`tel:${g.phone}`} className="flex items-center justify-center gap-2 rounded-md border border-line bg-surface py-2.5 text-sm font-medium text-ink hover:bg-sunken lg:hidden">
            <Phone className="size-4" aria-hidden /> Call {first}
          </a>
        </div>
      </div>

      <GuardianFormDialog guardian={g} open={editing} onOpenChange={setEditing} />
      <ConfirmDialog open={confirm === "enable"} onOpenChange={(o) => !o && setConfirm(null)}
        title={`Give ${first} family-portal access?`}
        description={g.email ? `${first} will sign in with ${g.email}.` : `${first} has no email address, so they'll sign in with a username made from their mobile number.`}
        confirmLabel="Give access" loading={enable.isPending}
        onConfirm={async () => {
          try {
            const result = await enable.mutateAsync(g.id);
            setConfirm(null);
            if (result.handover) setHandover({ handover: result.handover, loginName: result.loginName, reset: false });
            else toast.success("Portal access given", `${first} already has an account and signs in with it.`);
          } catch (error) {
            toast.error(error, "Portal access couldn't be given");
          }
        }} />
      <ConfirmDialog open={confirm === "reset"} onOpenChange={(o) => !o && setConfirm(null)}
        title={`Reset ${first}'s portal password?`}
        description={`${first} will be signed out everywhere, and you'll get a one-time password to give them.`}
        confirmLabel="Reset password" loading={reset.isPending}
        onConfirm={async () => {
          try {
            const result = await reset.mutateAsync(g.id);
            setConfirm(null);
            setHandover({ handover: result, loginName: g.loginName ?? "", reset: true });
          } catch (error) {
            toast.error(error, "The password couldn't be reset");
          }
        }} />
      <ConfirmDialog open={confirm === "delete"} onOpenChange={(o) => !o && setConfirm(null)} tone="danger"
        title={`Delete ${g.fullName}?`} description="This guardian isn't linked to any student. The record is removed permanently."
        confirmLabel="Delete" loading={remove.isPending}
        onConfirm={async () => {
          try {
            await remove.mutateAsync(g.id);
            toast.success("Guardian deleted");
            router.replace("/guardians");
          } catch (error) {
            toast.error(error, "The guardian couldn't be deleted");
          }
        }} />
      <Dialog open={handover !== null} onOpenChange={(o) => !o && setHandover(null)}>
        <DialogContent size="md">
          <DialogHeader title={handover?.reset ? "One-time password issued" : `${first} has portal access`}
            description={handover?.reset ? `${first} has been signed out everywhere.` : "Their family-portal account is ready."} />
          <DialogBody>
            {handover && <AccountHandoverNotice handover={handover.handover} name={first} loginName={handover.loginName} reset={handover.reset} />}
          </DialogBody>
          <DialogFooter><Button variant="primary" onClick={() => setHandover(null)}>Done</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </Page>
  );
}
