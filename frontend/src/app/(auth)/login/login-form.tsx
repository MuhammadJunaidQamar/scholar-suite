"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useQueryClient } from "@tanstack/react-query";
import { ChevronDown } from "lucide-react";
import { api, ApiError } from "@/lib/api/client";
import { meQueryKey } from "@/lib/auth/session";
import { portalHome, type BrowserSession, type Me } from "@/lib/auth/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Field } from "@/components/ui/field";
import { AuthHeading, Notice, PasswordInput, safeNext } from "../auth-ui";

const schema = z.object({
  identifier: z.string().trim().min(1, "Enter your email or username."),
  password: z.string().min(1, "Enter your password."),
  schoolCode: z.string().trim().max(32).optional(),
});

type Values = z.infer<typeof schema>;

export function LoginForm() {
  const router = useRouter();
  const search = useSearchParams();
  const queryClient = useQueryClient();
  const [error, setError] = React.useState<string | null>(null);
  const [showSchool, setShowSchool] = React.useState(Boolean(search.get("school")));
  const form = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { identifier: "", password: "", schoolCode: search.get("school") ?? "" },
  });

  const submit = form.handleSubmit(async (values) => {
    setError(null);
    try {
      await api.post<BrowserSession>(
        "/auth/login",
        { identifier: values.identifier, password: values.password, schoolCode: values.schoolCode || null },
        { noRefresh: true },
      );
      queryClient.clear();
      const me = await queryClient.fetchQuery({ queryKey: meQueryKey, queryFn: () => api.get<Me>("/auth/me") });
      if (me.user.mustChangePassword) {
        router.replace("/change-password");
        return;
      }
      const home = portalHome[me.access?.portals[0] ?? "Admin"];
      router.replace(safeNext(search.get("next")) ?? home);
    } catch (e) {
      setError(e instanceof ApiError && e.status < 500 ? e.message : "We couldn't sign you in right now. Please try again in a moment.");
      form.setValue("password", "");
      form.setFocus("password");
    }
  });

  const { errors, isSubmitting } = form.formState;
  return (
    <>
      <AuthHeading title="Sign in" description="Use the email or username your school gave you." />
      <form onSubmit={submit} noValidate className="grid gap-4">
        {search.get("expired") && !error && <Notice tone="info">Your session ended. Please sign in again.</Notice>}
        {search.get("reset") && !error && <Notice tone="success">Your password has been set. Sign in with it now.</Notice>}
        {error && <Notice tone="danger">{error}</Notice>}
        <Field label="Email or username" htmlFor="identifier" error={errors.identifier?.message}>
          <Input inputSize="lg" autoComplete="username" autoFocus autoCapitalize="none" spellCheck={false} {...form.register("identifier")} />
        </Field>
        <Field
          label="Password"
          htmlFor="password"
          error={errors.password?.message}
          action={<Link href="/forgot-password" className="text-sm text-ink-2 hover:text-ink">Forgot password?</Link>}
        >
          <PasswordInput autoComplete="current-password" {...form.register("password")} />
        </Field>
        {showSchool ? (
          <Field label="School code" htmlFor="schoolCode" optional hint="Only needed if you work at more than one school.">
            <Input inputSize="lg" autoCapitalize="none" spellCheck={false} placeholder="e.g. greenfield" {...form.register("schoolCode")} />
          </Field>
        ) : (
          <button type="button" onClick={() => setShowSchool(true)}
            className="flex items-center gap-1 justify-self-start text-sm text-ink-2 hover:text-ink">
            Signing in to a particular school? <ChevronDown className="size-3.5" />
          </button>
        )}
        <Button type="submit" variant="primary" size="lg" className="mt-2 w-full" loading={isSubmitting}>
          Sign in
        </Button>
      </form>
      <p className="mt-8 text-sm text-ink-3">
        Parents and students sign in here too. If you don&apos;t have an account, ask the school office.
      </p>
    </>
  );
}
