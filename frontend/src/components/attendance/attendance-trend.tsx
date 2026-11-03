"use client";

import * as React from "react";
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis, type TooltipContentProps } from "recharts";
import type { NameType, ValueType } from "recharts/types/component/DefaultTooltipContent";
import type { TrendPoint } from "@/lib/api/attendance";
import { formatDate, formatPercent } from "@/lib/format";
import { cn } from "@/lib/utils";

const short = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", timeZone: "UTC" });
const dayLabel = (iso: string) => short.format(new Date(`${iso}T12:00:00Z`));

/**
 * Daily attendance rate as a single-series area (one hue, one axis). The crosshair snaps to a day; the tooltip leads
 * with the value. A table view carries the same numbers for anyone who can't use the chart.
 */
export function AttendanceTrend({ points, fetching }: { points: TrendPoint[]; fetching?: boolean }) {
  const [table, setTable] = React.useState(false);
  const data = points.filter((p) => p.rate !== null);
  if (data.length === 0) {
    return <p className="px-4 py-12 text-center text-sm text-ink-2">No registers were taken in this period.</p>;
  }

  const lowest = Math.min(...data.map((p) => p.rate!));
  // Clean ticks: start at a round number below the lowest day so the line uses the height; percentages top out at 100.
  const floor = Math.max(0, Math.floor((lowest - 2) / 5) * 5);
  const ticks = Array.from({ length: Math.floor((100 - floor) / 5) + 1 }, (_, i) => floor + i * 5).filter((t, i, all) => all.length <= 6 || i % 2 === 0 || t === 100);
  // Each day's rate already follows the school's leave policy; weight the days by how many were marked.
  const average = data.reduce((sum, p) => sum + p.rate! * p.marked, 0) / Math.max(1, data.reduce((sum, p) => sum + p.marked, 0));

  return (
    <div className="grid gap-3">
      <div className="flex flex-wrap items-baseline justify-between gap-2 px-4">
        <p className="text-sm text-ink-2">
          Daily attendance · <span className="font-medium text-ink tabular">{formatPercent(average)}</span> over {data.length} school day{data.length === 1 ? "" : "s"}
        </p>
        <button type="button" onClick={() => setTable((t) => !t)} className="text-sm font-medium text-accent hover:underline">
          {table ? "Show chart" : "Show as table"}
        </button>
      </div>
      {table ? (
        <div className="max-h-96 overflow-y-auto border-t border-line">
          <table className="w-full text-sm">
            <caption className="sr-only">Daily attendance</caption>
            <thead className="sticky top-0 bg-surface">
              <tr className="border-b border-line text-left text-xs text-ink-3">
                <th className="px-4 py-2 font-medium">Day</th>
                <th className="px-3 py-2 text-right font-medium">Attendance</th>
                <th className="px-3 py-2 text-right font-medium">Present</th>
                <th className="px-3 py-2 text-right font-medium">Late</th>
                <th className="px-3 py-2 text-right font-medium">Absent</th>
                <th className="px-4 py-2 text-right font-medium">Leave</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line tabular">
              {[...data].reverse().map((p) => (
                <tr key={p.date}>
                  <td className="px-4 py-1.5 text-ink">{formatDate(p.date, { weekday: true })}</td>
                  <td className="px-3 py-1.5 text-right font-medium text-ink">{formatPercent(p.rate)}</td>
                  <td className="px-3 py-1.5 text-right text-ink-2">{p.present}</td>
                  <td className="px-3 py-1.5 text-right text-ink-2">{p.late}</td>
                  <td className="px-3 py-1.5 text-right text-ink-2">{p.absent}</td>
                  <td className="px-4 py-1.5 text-right text-ink-2">{p.leave}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className={cn("h-64 px-2 transition-opacity", fetching && "opacity-60")} role="img"
          aria-label={`Daily attendance from ${formatDate(data[0]!.date)} to ${formatDate(data.at(-1)!.date)}, lowest ${formatPercent(lowest)}.`}>
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
              <defs>
                <linearGradient id="trend-fill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--chart-line)" stopOpacity={0.16} />
                  <stop offset="100%" stopColor="var(--chart-line)" stopOpacity={0.02} />
                </linearGradient>
              </defs>
              <CartesianGrid vertical={false} stroke="var(--chart-grid)" strokeWidth={1} />
              <XAxis dataKey="date" tickFormatter={dayLabel} tickLine={false} axisLine={{ stroke: "var(--line)" }} minTickGap={28}
                tick={{ fill: "var(--ink-3)", fontSize: 11 }} tickMargin={8} />
              <YAxis domain={[floor, 100]} ticks={ticks} tickFormatter={(v: number) => `${v}%`} width={44} tickLine={false} axisLine={false}
                tick={{ fill: "var(--ink-3)", fontSize: 11 }} />
              <Tooltip cursor={{ stroke: "var(--line-strong)", strokeWidth: 1 }} content={TrendTooltip} isAnimationActive={false} />
              <Area type="monotone" dataKey="rate" stroke="var(--chart-line)" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round"
                fill="url(#trend-fill)" isAnimationActive={false}
                activeDot={{ r: 4, fill: "var(--chart-line)", stroke: "var(--surface)", strokeWidth: 2 }} dot={false} />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}

function TrendTooltip({ active, payload }: TooltipContentProps<ValueType, NameType>) {
  const point = payload?.[0]?.payload as TrendPoint | undefined;
  if (!active || !point) return null;
  return (
    <div className="grid min-w-44 gap-1 rounded-md border border-line bg-raised px-3 py-2 text-xs shadow-popover">
      <span className="text-ink-2">{formatDate(point.date, { weekday: true })}</span>
      <span className="flex items-center gap-2">
        <span className="h-0.5 w-3 rounded-full bg-[var(--chart-line)]" aria-hidden />
        <span className="text-sm font-semibold text-ink tabular">{formatPercent(point.rate)}</span>
        <span className="text-ink-2">attended</span>
      </span>
      <span className="text-ink-2 tabular">{point.absent} absent · {point.late} late · {point.leave} on leave</span>
    </div>
  );
}
