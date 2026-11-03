"use client";

import * as React from "react";
import { useTheme } from "next-themes";
import { Download, Filter, Inbox, Moon, Plus, Search, Sun, UserPlus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input, Textarea } from "@/components/ui/input";
import { Field, FieldRow, FormSection } from "@/components/ui/field";
import { Select } from "@/components/ui/select";
import { Checkbox, Segmented, Switch } from "@/components/ui/toggles";
import { Avatar, Badge, DetailList, Kbd, StatusBadge } from "@/components/ui/display";
import { EmptyState } from "@/components/ui/states";
import { formatMoney } from "@/lib/format";
import { useMounted } from "@/hooks/use-mounted";

const swatches = ["canvas", "surface", "sunken", "line", "line-strong", "ink", "ink-2", "ink-3", "accent", "accent-soft",
  "success", "warning", "danger", "info"];

export function DesignSystemPreview() {
  const { resolvedTheme: theme, setTheme } = useTheme();
  const mounted = useMounted();
  const resolvedTheme = mounted ? theme : undefined;
  const [view, setView] = React.useState<"day" | "week" | "month">("week");
  const [checked, setChecked] = React.useState(true);

  return (
    <main className="mx-auto max-w-6xl px-8 py-10">
      <header className="flex items-end justify-between border-b border-line pb-6">
        <div className="grid gap-1">
          <p className="text-sm text-ink-2">Greenfield Grammar School</p>
          <h1 className="font-display text-3xl text-ink">Design system</h1>
          <p className="text-base text-ink-2">The Register — paper, ink and hairlines. Colour only where it means something.</p>
        </div>
        <Button variant="secondary" onClick={() => setTheme(resolvedTheme === "dark" ? "light" : "dark")}>
          {resolvedTheme === "dark" ? <Sun /> : <Moon />} {resolvedTheme === "dark" ? "Light" : "Dark"} theme
        </Button>
      </header>

      <section className="grid gap-4 border-b border-line py-8">
        <h2 className="text-md font-semibold">Colour</h2>
        <div className="grid grid-cols-7 gap-3">
          {swatches.map((s) => (
            <div key={s} className="grid gap-1.5">
              <div className="h-12 rounded-md border border-line" style={{ background: `var(--${s})` }} />
              <span className="text-xs text-ink-2">{s}</span>
            </div>
          ))}
        </div>
      </section>

      <section className="grid gap-5 border-b border-line py-8">
        <h2 className="text-md font-semibold">Type</h2>
        <div className="grid gap-3">
          <p className="font-display text-4xl">Rs 4.8 lac collected</p>
          <p className="font-display text-3xl">Students</p>
          <p className="font-display text-2xl">Class 5 – A, First Term</p>
          <p className="text-xl font-semibold">Section heading 20</p>
          <p className="text-lg font-medium">Panel title 17</p>
          <p className="max-w-2xl text-base">
            Body 14 — Ayesha Khan was marked late on 3 November. Her attendance this term is 94.2%, above the school minimum of 75%.
          </p>
          <p className="text-sm text-ink-2">Secondary 13 — Admission ADM-2026-0412 · Gulberg Campus</p>
          <p className="tabular text-sm">Tabular 0123456789 · 12,450 · 98.75%</p>
          <p className="font-mono text-xs text-ink-2">CH-2611-00428 · RCT-2611-00117</p>
        </div>
      </section>

      <section className="grid gap-5 border-b border-line py-8">
        <h2 className="text-md font-semibold">Actions</h2>
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="primary"><Plus /> New student</Button>
          <Button variant="secondary"><Download /> Export</Button>
          <Button variant="ghost"><Filter /> Filter</Button>
          <Button variant="subtle">Subtle</Button>
          <Button variant="danger">Reverse payment</Button>
          <Button variant="danger-ghost">Remove</Button>
          <Button variant="link">View statement</Button>
          <Button variant="primary" loading>Saving</Button>
          <Button variant="secondary" disabled>Disabled</Button>
          <Button variant="secondary" size="sm">Small</Button>
          <Button variant="secondary" size="icon" aria-label="Add member"><UserPlus /></Button>
          <Segmented value={view} onChange={setView} aria-label="Range"
            options={[{ value: "day", label: "Day" }, { value: "week", label: "Week" }, { value: "month", label: "Month" }]} />
        </div>
      </section>

      <section className="grid gap-5 border-b border-line py-8">
        <h2 className="text-md font-semibold">Status</h2>
        <div className="flex flex-wrap items-center gap-5">
          <StatusBadge tone="success">Paid</StatusBadge>
          <StatusBadge tone="warning">Partially paid</StatusBadge>
          <StatusBadge tone="danger">Overdue</StatusBadge>
          <StatusBadge tone="info">Under review</StatusBadge>
          <StatusBadge tone="neutral">Withdrawn</StatusBadge>
          <StatusBadge tone="accent">Enrolled</StatusBadge>
          <StatusBadge tone="success" variant="soft">Present</StatusBadge>
          <StatusBadge tone="danger" variant="soft">Absent</StatusBadge>
          <StatusBadge tone="warning" variant="soft">Late</StatusBadge>
          <Badge>Class 5 – A</Badge>
          <Badge tone="accent">Sibling</Badge>
          <span className="flex items-center gap-1.5 text-sm text-ink-2">Search <Kbd>⌘</Kbd><Kbd>K</Kbd></span>
        </div>
        <div className="flex items-center gap-3">
          {["Ayesha Khan", "Muhammad Ali", "Fatima Zahra", "Hamza Siddiqui", "Zainab Riaz", "Omar Farooq"].map((n) => (
            <Avatar key={n} name={n} size={32} />
          ))}
        </div>
      </section>

      <section className="py-8">
        <FormSection title="Student" description="Name as it appears on the B-Form. This is printed on certificates and report cards.">
          <FieldRow>
            <Field label="First name" htmlFor="first"><Input defaultValue="Ayesha" /></Field>
            <Field label="Last name" htmlFor="last" error="Last name is required."><Input /></Field>
          </FieldRow>
          <FieldRow cols={3}>
            <Field label="Date of birth" htmlFor="dob"><Input type="date" defaultValue="2015-04-12" /></Field>
            <Field label="Gender" htmlFor="gender">
              <Select options={[{ value: "f", label: "Female" }, { value: "m", label: "Male" }]} defaultValue="f" />
            </Field>
            <Field label="B-Form number" htmlFor="bform" optional hint="13 digits, without dashes.">
              <Input placeholder="35202xxxxxxxx" />
            </Field>
          </FieldRow>
          <Field label="Search" htmlFor="search"><Input leading={<Search />} placeholder="Search students, staff, challans…" /></Field>
          <Field label="Monthly tuition" htmlFor="fee"><Input leading={<span className="text-sm">Rs</span>} defaultValue="12,500" className="max-w-48" /></Field>
          <Field label="Medical notes" htmlFor="notes" optional><Textarea placeholder="Allergies, conditions…" /></Field>
          <Field inline label="Sends absence alerts" hint="Guardians receive an SMS when the student is absent." htmlFor="alerts">
            <Switch checked={checked} onCheckedChange={setChecked} />
          </Field>
          <Field inline label="Fee payer" htmlFor="payer"><Checkbox defaultChecked /></Field>
        </FormSection>

        <FormSection title="Record" description="Details as a ruled list.">
          <DetailList columns={2} items={[
            { label: "Admission no.", value: <span className="tabular">ADM-2026-0412</span> },
            { label: "Class", value: "Class 5 – A" },
            { label: "Campus", value: "Gulberg Campus" },
            { label: "Outstanding", value: <span className="tabular text-danger">{formatMoney(18500)}</span> },
            { label: "Status", value: <StatusBadge tone="success">Active</StatusBadge> },
            { label: "Guardian", value: "Tariq Khan (Father)" },
          ]} />
        </FormSection>

        <FormSection title="Empty state" description="Calm, specific, with a next step.">
          <div className="rounded-lg border border-dashed border-line-strong">
            <EmptyState icon={<Inbox />} title="No inquiries yet"
              description="Walk-in and phone inquiries you record will appear here, ready to follow up."
              action={<Button variant="primary" size="sm"><Plus /> Record inquiry</Button>} />
          </div>
        </FormSection>
      </section>
    </main>
  );
}
