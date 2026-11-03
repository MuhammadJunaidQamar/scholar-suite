"use client";

import * as React from "react";
import { Eye, EyeOff, CircleAlert, CircleCheck } from "lucide-react";
import { cn } from "@/lib/utils";
import { Input, type InputProps } from "@/components/ui/input";

export function AuthHeading({ title, description }: { title: string; description?: React.ReactNode }) {
  return (
    <div className="mb-7 grid gap-1.5">
      <h1 className="font-display text-[28px] leading-9 text-ink">{title}</h1>
      {description && <p className="text-base text-ink-2">{description}</p>}
    </div>
  );
}

export function PasswordInput(props: Omit<InputProps, "type" | "trailing">) {
  const [visible, setVisible] = React.useState(false);
  return (
    <Input
      {...props}
      type={visible ? "text" : "password"}
      inputSize="lg"
      trailing={
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          aria-label={visible ? "Hide password" : "Show password"}
          aria-pressed={visible}
          className="grid size-7 place-items-center rounded-md text-ink-3 hover:bg-sunken hover:text-ink"
        >
          {visible ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
        </button>
      }
    />
  );
}

export function Notice({ tone, children }: { tone: "danger" | "info" | "success"; children: React.ReactNode }) {
  const Icon = tone === "success" ? CircleCheck : CircleAlert;
  return (
    <div
      role={tone === "danger" ? "alert" : "status"}
      className={cn(
        "flex items-start gap-2 rounded-md px-3 py-2.5 text-sm",
        tone === "danger" && "bg-danger-soft text-danger",
        tone === "info" && "bg-info-soft text-info",
        tone === "success" && "bg-success-soft text-[oklch(from_var(--success)_calc(l-0.06)_c_h)] dark:text-success",
      )}
    >
      <Icon className="mt-px size-4 shrink-0" />
      <div>{children}</div>
    </div>
  );
}

/** Safe post-sign-in destination: same-origin paths only. */
export function safeNext(next: string | null | undefined): string | null {
  if (!next || !next.startsWith("/") || next.startsWith("//") || next.startsWith("/login")) return null;
  return next;
}
