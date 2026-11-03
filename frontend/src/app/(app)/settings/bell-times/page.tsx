"use client";

import * as React from "react";
import { Coffee, Plus, Trash2 } from "lucide-react";
import { useSession } from "@/lib/auth/session";
import { P } from "@/lib/permissions";
import { formatClock, useBellSchedules, useSaveBellSchedule, type BellSchedule } from "@/lib/api/attendance";
import { Page, PageHeader } from "@/components/shell/page";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/display";
import { ErrorState } from "@/components/ui/states";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/toggles";
import { SectionHeading } from "@/components/ui/record-list";
import { toast } from "@/components/ui/toaster";

type Slot = { key: number; name: string; startTime: string; endTime: string; isBreak: boolean };

let nextKey = 1;
const slot = (name: string, startTime: string, endTime: string, isBreak = false): Slot => ({ key: nextKey++, name, startTime, endTime, isBreak });

/** A typical Pakistani school day: assembly, eight 40-minute periods and a break. */
const standardDay = (): Slot[] => [
  slot("Assembly", "07:50", "08:00", true), slot("", "08:00", "08:40"), slot("", "08:40", "09:20"), slot("", "09:20", "10:00"), slot("", "10:00", "10:40"),
  slot("Break", "10:40", "11:00", true), slot("", "11:00", "11:40"), slot("", "11:40", "12:20"), slot("", "12:20", "13:00"), slot("", "13:00", "13:40"),
];

export default function BellTimesPage() {
  const schedules = useBellSchedules();
  return (
    <Page className="px-0 sm:px-0 lg:px-0">
      <PageHeader breadcrumbs={[{ label: "Settings", href: "/settings" }, { label: "Bell times" }]} title="Bell times"
        description="Each campus's school day. Teaching periods are numbered in order; they label period-wise registers (and, later, the timetable)." />
      {schedules.isPending ? <Skeleton className="h-96" /> : schedules.error ? <ErrorState error={schedules.error} /> : (
        <div className="grid gap-10 xl:grid-cols-2">{schedules.data.map((s) => <ScheduleEditor key={s.campusId} schedule={s} />)}</div>
      )}
    </Page>
  );
}

function ScheduleEditor({ schedule }: { schedule: BellSchedule }) {
  const { can } = useSession();
  const editable = can(P.Timetable.Manage);
  const save = useSaveBellSchedule();
  const [name, setName] = React.useState(schedule.name);
  const [slots, setSlots] = React.useState<Slot[]>(() =>
    schedule.periods.map((p) => slot(p.isBreak || !/^Period \d+$/.test(p.name) ? p.name : "", p.startTime.slice(0, 5), p.endTime.slice(0, 5), p.isBreak)));
  const [editing, setEditing] = React.useState(false);

  const update = (key: number, patch: Partial<Slot>) => setSlots((list) => list.map((s) => (s.key === key ? { ...s, ...patch } : s)));
  const add = () => setSlots((list) => {
    const last = list.at(-1);
    const [h, m] = (last?.endTime ?? "08:00").split(":").map(Number) as [number, number];
    const end = new Date(Date.UTC(2000, 0, 1, h, m + 40));
    return [...list, slot("", last?.endTime ?? "08:00", `${String(end.getUTCHours()).padStart(2, "0")}:${String(end.getUTCMinutes()).padStart(2, "0")}`)];
  });

  const submit = async () => {
    try {
      await save.mutateAsync({ campusId: schedule.campusId, body: { name, slots: slots.map((s) => ({ name: s.name, startTime: `${s.startTime}:00`, endTime: `${s.endTime}:00`, isBreak: s.isBreak })) } });
      toast.success(`${schedule.campusName} bell times saved`);
      setEditing(false);
    } catch (error) {
      toast.error(error, "The bell times couldn't be saved");
    }
  };

  let number = 0;
  return (
    <section className="grid content-start gap-2">
      <SectionHeading title={schedule.campusName} description={schedule.id ? schedule.name : "No bell times yet"}
        actions={editable && !editing && <Button size="sm" variant="secondary" onClick={() => { if (!schedule.id && slots.length === 0) setSlots(standardDay()); setEditing(true); }}>{schedule.id ? "Edit" : "Set up"}</Button>} />
      {editing ? (
        <div className="rounded-lg border border-line bg-surface">
          <div className="flex flex-wrap items-center gap-2 border-b border-line px-4 py-2.5">
            <Input aria-label="Schedule name" value={name} onChange={(e) => setName(e.target.value)} className="max-w-56" />
            <Button size="sm" variant="ghost" onClick={() => setSlots(standardDay())}>Use a standard day</Button>
          </div>
          <ul className="divide-y divide-line">
            {slots.map((s) => {
              if (!s.isBreak) number++;
              return (
                <li key={s.key} className="grid grid-cols-[3.5rem_minmax(0,1fr)_6.5rem_6.5rem_auto_auto] items-center gap-2 px-4 py-2">
                  <span className="text-sm text-ink-3 tabular">{s.isBreak ? "—" : `P${number}`}</span>
                  <Input inputSize="sm" aria-label="Name" placeholder={s.isBreak ? "Break" : `Period ${number}`} value={s.name} onChange={(e) => update(s.key, { name: e.target.value })} />
                  <Input inputSize="sm" type="time" aria-label="Starts" value={s.startTime} onChange={(e) => update(s.key, { startTime: e.target.value })} />
                  <Input inputSize="sm" type="time" aria-label="Ends" value={s.endTime} onChange={(e) => update(s.key, { endTime: e.target.value })} />
                  <label className="inline-flex items-center gap-1.5 text-xs text-ink-2"><Switch checked={s.isBreak} onCheckedChange={(v) => update(s.key, { isBreak: v })} /> Break</label>
                  <Button size="icon-sm" variant="ghost" aria-label="Remove" onClick={() => setSlots((list) => list.filter((x) => x.key !== s.key))}><Trash2 /></Button>
                </li>
              );
            })}
          </ul>
          <div className="flex items-center gap-2 border-t border-line px-4 py-2.5">
            <Button size="sm" variant="ghost" onClick={add}><Plus /> Add a slot</Button>
            <div className="ml-auto flex gap-2">
              <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>Cancel</Button>
              <Button size="sm" variant="primary" loading={save.isPending} disabled={slots.length === 0} onClick={() => void submit()}>Save bell times</Button>
            </div>
          </div>
        </div>
      ) : schedule.periods.length === 0 ? (
        <p className="rounded-lg border border-line bg-surface px-4 py-6 text-sm text-ink-2">No bell times for this campus yet. Period registers are then labelled P1, P2 …</p>
      ) : (
        <ol className="divide-y divide-line rounded-lg border border-line bg-surface">
          {schedule.periods.map((p) => (
            <li key={p.sequence} className="flex items-center gap-3 px-4 py-2 text-sm">
              <span className="w-10 text-ink-3 tabular">{p.number ? `P${p.number}` : ""}</span>
              <span className="flex flex-1 items-center gap-1.5 text-ink">{p.isBreak && <Coffee className="size-3.5 text-ink-3" />}{p.name}</span>
              <span className="text-ink-2 tabular">{formatClock(p.startTime)} – {formatClock(p.endTime)}</span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
