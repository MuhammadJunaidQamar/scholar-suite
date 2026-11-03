"use client";

import * as React from "react";
import { Check, Copy, KeyRound, MailCheck, MailX } from "lucide-react";
import type { AccountHandover } from "@/lib/api/access";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/toaster";

function CopyValue({ label, value, secret }: { label: string; value: string; secret?: boolean }) {
  const [copied, setCopied] = React.useState(false);
  return (
    <div className="grid gap-1">
      <span className="text-xs text-ink-2">{label}</span>
      <div className="flex items-center justify-between gap-3 rounded-md border border-line bg-surface px-3 py-2">
        <code className={secret ? "font-mono text-md tracking-wide text-ink" : "font-mono text-sm text-ink"}>{value}</code>
        <Button type="button" size="sm" variant="ghost"
          onClick={async () => {
            try {
              await navigator.clipboard.writeText(value);
              setCopied(true);
              setTimeout(() => setCopied(false), 2000);
            } catch {
              toast.error(new Error("Copy it by hand instead."), "The clipboard isn't available");
            }
          }}>
          {copied ? <Check /> : <Copy />} {copied ? "Copied" : "Copy"}
        </Button>
      </div>
    </div>
  );
}

/**
 * The outcome of giving someone a new account, stated exactly: an invitation the provider accepted (or queued), or —
 * when nothing could be sent — one-time sign-in details for the office to pass on. Never "sent" unless it was.
 */
export function AccountHandoverNotice({ handover, name, loginName, reset }: {
  handover: AccountHandover;
  name: string;
  loginName: string;
  /** A password reset by an administrator rather than a new account. */
  reset?: boolean;
}) {
  if (handover.temporaryPassword) {
    return (
      <div className="grid gap-4">
        {reset ? (
          <p className="text-sm text-ink-2">Give {name} these details in person or by phone.</p>
        ) : (
          <div className="flex gap-3 rounded-md border border-warning/40 bg-warning-soft px-3.5 py-3 text-sm">
            <MailX className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
            <div className="grid gap-0.5">
              <p className="font-medium text-ink">No invitation was sent</p>
              <p className="text-ink-2">{handover.detail ?? "The invitation could not be sent."} Give {name} these details in person or by phone.</p>
            </div>
          </div>
        )}
        <CopyValue label="Sign in with" value={loginName} />
        <CopyValue label="Temporary password" value={handover.temporaryPassword} secret />
        <p className="flex gap-2 text-sm text-ink-2">
          <KeyRound className="mt-0.5 size-4 shrink-0 text-ink-3" aria-hidden />
          {name} will choose their own password the first time they sign in. This password is shown only now.
        </p>
      </div>
    );
  }
  return (
    <div className="flex gap-3 rounded-md border border-line bg-sunken/60 px-3.5 py-3 text-sm">
      <MailCheck className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
      <p className="text-ink">
        {handover.invitation === "Sent"
          ? <>Invitation handed to the email provider for <strong className="font-medium">{handover.sentTo}</strong>. We can&apos;t see when it&apos;s opened.</>
          : <>Invitation queued for <strong className="font-medium">{handover.sentTo}</strong>. It has not been delivered yet.</>}
      </p>
    </div>
  );
}
