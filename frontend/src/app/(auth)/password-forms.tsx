"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "@/lib/api/client";
import { meQueryKey } from "@/lib/auth/session";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/ui/field";
import { AuthHeading, Notice, PasswordInput } from "./auth-ui";

const passwordRule = z
  .string()
  .min(8, "Use at least 8 characters.")
  .max(128)
  .refine((p) => /[a-z]/i.test(p) && /\d/.test(p), "Use a mix of letters and numbers.");

export function ForgotPasswordForm() {
  const [sent, setSent] = React.useState(false);
  // Whether reset links can be e-mailed at all is system-wide, so asking reveals nothing about any account.
  const options = useQuery({
    queryKey: ["auth", "options"],
    queryFn: () => api.get<{ passwordResetByEmail: boolean }>("/auth/options"),
    staleTime: 5 * 60_000,
  });
  const form = useForm<{ identifier: string }>({
    resolver: zodResolver(z.object({ identifier: z.string().trim().min(1, "Enter your email or username.") })),
    defaultValues: { identifier: "" },
  });
  const submit = form.handleSubmit(async (values) => {
    try {
      await api.post("/auth/password/forgot", values, { noRefresh: true });
    } finally {
      // The response is the same whether or not the account exists.
      setSent(true);
    }
  });

  if (options.data && !options.data.passwordResetByEmail) {
    return (
      <>
        <AuthHeading title="Ask your school office"
          description="Email isn't set up yet, so a reset link can't be sent. The office can give you a one-time password; you'll choose a new one when you sign in." />
        <Button asChild variant="secondary" size="lg" className="w-full"><Link href="/login">Back to sign in</Link></Button>
      </>
    );
  }

  if (sent) {
    return (
      <>
        <AuthHeading title="Check your email" description="If an account matches, a link to choose a new password is being emailed to it. The link works for one hour." />
        <Button asChild variant="secondary" size="lg" className="w-full"><Link href="/login">Back to sign in</Link></Button>
      </>
    );
  }
  return (
    <>
      <AuthHeading title="Reset your password" description="Enter your email or username and we'll send you a link." />
      <form onSubmit={submit} noValidate className="grid gap-4">
        <Field label="Email or username" htmlFor="identifier" error={form.formState.errors.identifier?.message}>
          <Input inputSize="lg" autoFocus autoComplete="username" autoCapitalize="none" {...form.register("identifier")} />
        </Field>
        <Button type="submit" variant="primary" size="lg" className="mt-2 w-full" loading={form.formState.isSubmitting}>
          Send reset link
        </Button>
        <Link href="/login" className="justify-self-center text-sm text-ink-2 hover:text-ink">Back to sign in</Link>
      </form>
    </>
  );
}

const newPasswordSchema = z
  .object({ password: passwordRule, confirm: z.string() })
  .refine((v) => v.password === v.confirm, { message: "The passwords don't match.", path: ["confirm"] });

/** Used by both the reset link and the invitation link (same token flow, different welcome). */
export function SetPasswordForm({ mode }: { mode: "reset" | "invite" }) {
  const search = useSearchParams();
  const router = useRouter();
  const [error, setError] = React.useState<string | null>(null);
  const uid = search.get("uid");
  const token = search.get("token");
  const form = useForm<z.infer<typeof newPasswordSchema>>({ resolver: zodResolver(newPasswordSchema), defaultValues: { password: "", confirm: "" } });

  const submit = form.handleSubmit(async (values) => {
    setError(null);
    try {
      await api.post("/auth/password/reset", { userId: uid, token, newPassword: values.password }, { noRefresh: true });
      router.replace("/login?reset=1");
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Something went wrong. Please try again.");
    }
  });

  if (!uid || !token) {
    return (
      <>
        <AuthHeading title="This link is incomplete" description="Open the link from your email again, or request a new one." />
        <Button asChild variant="secondary" size="lg" className="w-full"><Link href="/forgot-password">Request a new link</Link></Button>
      </>
    );
  }

  return (
    <>
      <AuthHeading
        title={mode === "invite" ? "Welcome" : "Choose a new password"}
        description={mode === "invite" ? "You've been invited to your school's workspace. Choose a password to finish setting up your account." : "This will sign you out of all other devices."}
      />
      <form onSubmit={submit} noValidate className="grid gap-4">
        {error && <Notice tone="danger">{error}</Notice>}
        <Field label="New password" htmlFor="password" error={form.formState.errors.password?.message} hint="At least 8 characters, with letters and numbers.">
          <PasswordInput autoFocus autoComplete="new-password" {...form.register("password")} />
        </Field>
        <Field label="Confirm password" htmlFor="confirm" error={form.formState.errors.confirm?.message}>
          <PasswordInput autoComplete="new-password" {...form.register("confirm")} />
        </Field>
        <Button type="submit" variant="primary" size="lg" className="mt-2 w-full" loading={form.formState.isSubmitting}>
          {mode === "invite" ? "Set password" : "Save password"}
        </Button>
      </form>
    </>
  );
}

const changeSchema = z
  .object({ current: z.string().min(1, "Enter your current password."), password: passwordRule, confirm: z.string() })
  .refine((v) => v.password === v.confirm, { message: "The passwords don't match.", path: ["confirm"] })
  .refine((v) => v.password !== v.current, { message: "Choose a password different from the current one.", path: ["password"] });

export function ChangePasswordForm({ forced, onDone }: { forced?: boolean; onDone?: () => void }) {
  const queryClient = useQueryClient();
  const router = useRouter();
  const [error, setError] = React.useState<string | null>(null);
  const form = useForm<z.infer<typeof changeSchema>>({ resolver: zodResolver(changeSchema), defaultValues: { current: "", password: "", confirm: "" } });

  const submit = form.handleSubmit(async (values) => {
    setError(null);
    try {
      await api.post("/auth/password/change", { currentPassword: values.current, newPassword: values.password });
      await queryClient.invalidateQueries({ queryKey: meQueryKey });
      form.reset();
      if (onDone) onDone();
      else router.replace("/");
    } catch (e) {
      if (e instanceof ApiError && e.fieldErrors.currentPassword) form.setError("current", { message: e.fieldErrors.currentPassword[0] });
      else if (e instanceof ApiError && e.fieldErrors.newPassword) form.setError("password", { message: e.fieldErrors.newPassword[0] });
      else setError(e instanceof ApiError ? e.message : "Something went wrong. Please try again.");
    }
  });

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      {forced && <Notice tone="info">Your account was set up with a temporary password. Choose your own to continue.</Notice>}
      {error && <Notice tone="danger">{error}</Notice>}
      <Field label="Current password" htmlFor="current" error={form.formState.errors.current?.message}>
        <PasswordInput autoComplete="current-password" {...form.register("current")} />
      </Field>
      <Field label="New password" htmlFor="password" error={form.formState.errors.password?.message} hint="At least 8 characters, with letters and numbers.">
        <PasswordInput autoComplete="new-password" {...form.register("password")} />
      </Field>
      <Field label="Confirm new password" htmlFor="confirm" error={form.formState.errors.confirm?.message}>
        <PasswordInput autoComplete="new-password" {...form.register("confirm")} />
      </Field>
      <Button type="submit" variant="primary" size={forced ? "lg" : "md"} className={forced ? "mt-2 w-full" : "justify-self-start"} loading={form.formState.isSubmitting}>
        Change password
      </Button>
    </form>
  );
}
