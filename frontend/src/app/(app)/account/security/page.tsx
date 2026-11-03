"use client";

import { Laptop, Smartphone } from "lucide-react";
import { formatRelative } from "@/lib/format";
import { describeDevice, useRevokeSession, useSessions } from "@/lib/api/account";
import { Page, PageHeader } from "@/components/shell/page";
import { FormSection } from "@/components/ui/field";
import { Button } from "@/components/ui/button";
import { Badge, Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { toast } from "@/components/ui/toaster";
import { ChangePasswordForm } from "@/app/(auth)/password-forms";

export default function SecurityPage() {
  const sessions = useSessions();
  const revoke = useRevokeSession();

  return (
    <Page width="narrow">
      <PageHeader breadcrumbs={[{ label: "Your account", href: "/account" }, { label: "Password & sessions" }]}
        title="Password & sessions" description="Keep your account secure. Changing your password signs out every other device." />
      <FormSection title="Password" description="At least 8 characters, mixing letters and numbers. Avoid passwords you use elsewhere.">
        <div className="max-w-sm">
          <ChangePasswordForm onDone={() => {
            toast.success("Password changed", "Other devices have been signed out.");
            void sessions.refetch();
          }} />
        </div>
      </FormSection>
      <FormSection title="Signed-in devices" description="Sign out of any device you don't recognise.">
        {sessions.isPending ? (
          <div className="grid gap-2">{[0, 1].map((i) => <Skeleton key={i} className="h-14" />)}</div>
        ) : sessions.error ? (
          <ErrorState error={sessions.error} onRetry={() => void sessions.refetch()} />
        ) : (
          <ul className="divide-y divide-line rounded-lg border border-line bg-surface">
            {sessions.data.map((session) => {
              const Icon = session.clientType === "Mobile" ? Smartphone : Laptop;
              return (
                <li key={session.id} className="flex items-center gap-3 px-4 py-3">
                  <Icon className="size-5 shrink-0 text-ink-3" strokeWidth={1.5} />
                  <div className="grid min-w-0 flex-1 gap-0.5">
                    <span className="flex items-center gap-2 text-base font-medium">
                      {describeDevice(session.userAgent)}
                      {session.isCurrent && <Badge tone="accent">This device</Badge>}
                    </span>
                    <span className="text-sm text-ink-2">
                      {session.ipAddress ?? "Unknown location"} · active {formatRelative(session.lastSeenAt)} · signed in {formatRelative(session.createdAt)}
                    </span>
                  </div>
                  {!session.isCurrent && (
                    <Button size="sm" variant="secondary" loading={revoke.isPending && revoke.variables === session.id}
                      onClick={async () => {
                        try {
                          await revoke.mutateAsync(session.id);
                          toast.success("Device signed out");
                        } catch (error) {
                          toast.error(error);
                        }
                      }}>
                      Sign out
                    </Button>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </FormSection>
    </Page>
  );
}
