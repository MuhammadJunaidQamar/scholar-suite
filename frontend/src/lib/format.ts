/**
 * Locale-aware formatting. Defaults are Pakistan-first (en-PK, PKR) but always overridable with the
 * school context so the same components serve international schools.
 */
export type FormatContext = {
  locale: string;
  currency: string;
  timeZone: string;
};

export const defaultFormatContext: FormatContext = {
  locale: "en-PK",
  currency: "PKR",
  timeZone: "Asia/Karachi",
};

let active: FormatContext = defaultFormatContext;

export function setFormatContext(context: Partial<FormatContext>) {
  active = { ...active, ...context };
}

export function getFormatContext() {
  return active;
}

const cache = new Map<string, Intl.NumberFormat | Intl.DateTimeFormat>();

function numberFormat(key: string, options: Intl.NumberFormatOptions): Intl.NumberFormat {
  const id = `${active.locale}|${key}`;
  let f = cache.get(id) as Intl.NumberFormat | undefined;
  if (!f) {
    f = new Intl.NumberFormat(active.locale, options);
    cache.set(id, f);
  }
  return f;
}

function dateFormat(key: string, options: Intl.DateTimeFormatOptions): Intl.DateTimeFormat {
  const id = `${active.locale}|${active.timeZone}|${key}`;
  let f = cache.get(id) as Intl.DateTimeFormat | undefined;
  if (!f) {
    f = new Intl.DateTimeFormat(active.locale, { timeZone: active.timeZone, ...options });
    cache.set(id, f);
  }
  return f;
}

/** "Rs 12,500" — whole rupees unless the amount has paisa. */
export function formatMoney(amount: number | null | undefined, options?: { currency?: string; compact?: boolean }) {
  if (amount == null || Number.isNaN(amount)) return "—";
  const currency = options?.currency ?? active.currency;
  if (options?.compact && Math.abs(amount) >= 100_000) {
    return `${currencySymbol(currency)} ${formatCompact(amount)}`;
  }
  const hasFraction = Math.round(amount * 100) % 100 !== 0;
  const formatted = numberFormat(`money-${hasFraction}`, {
    minimumFractionDigits: hasFraction ? 2 : 0,
    maximumFractionDigits: 2,
  }).format(amount);
  return `${currencySymbol(currency)} ${formatted}`;
}

export function currencySymbol(currency: string) {
  switch (currency) {
    case "PKR":
      return "Rs";
    case "USD":
      return "$";
    case "GBP":
      return "£";
    case "EUR":
      return "€";
    case "AED":
      return "AED";
    default:
      return currency;
  }
}

/** South Asian grouping for large figures: 1.2 lakh, 3.4 crore in en-PK; K/M elsewhere. */
export function formatCompact(value: number) {
  if (active.locale.endsWith("-PK") || active.locale.endsWith("-IN")) {
    const abs = Math.abs(value);
    if (abs >= 10_000_000) return `${trim(value / 10_000_000)} cr`;
    if (abs >= 100_000) return `${trim(value / 100_000)} lac`;
  }
  return numberFormat("compact", { notation: "compact", maximumFractionDigits: 1 }).format(value);
}

function trim(n: number) {
  return n.toFixed(n >= 100 ? 0 : 1).replace(/\.0$/, "");
}

export function formatNumber(value: number | null | undefined, digits = 0) {
  if (value == null || Number.isNaN(value)) return "—";
  return numberFormat(`n${digits}`, { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
}

export function formatPercent(value: number | null | undefined, digits = 1) {
  if (value == null || Number.isNaN(value)) return "—";
  return `${formatNumber(value, digits)}%`;
}

/** Parses an ISO date ("2026-11-03") as a calendar date, independent of the browser's timezone. */
export function parseDateOnly(value: string): Date {
  const [y, m, d] = value.slice(0, 10).split("-").map(Number);
  return new Date(Date.UTC(y!, (m ?? 1) - 1, d ?? 1, 12));
}

/** "3 Nov 2026" */
export function formatDate(value: string | Date | null | undefined, options?: { weekday?: boolean }) {
  if (!value) return "—";
  const date = typeof value === "string" ? (value.length <= 10 ? parseDateOnly(value) : new Date(value)) : value;
  const zoneSafe = typeof value === "string" && value.length <= 10;
  const f = zoneSafe
    ? new Intl.DateTimeFormat(active.locale, {
        timeZone: "UTC",
        day: "numeric",
        month: "short",
        year: "numeric",
        weekday: options?.weekday ? "short" : undefined,
      })
    : dateFormat(`date-${options?.weekday ?? false}`, {
        day: "numeric",
        month: "short",
        year: "numeric",
        weekday: options?.weekday ? "short" : undefined,
      });
  return f.format(date);
}

/** "3 Nov 2026, 9:42 am" in the school's timezone. */
export function formatDateTime(value: string | Date | null | undefined) {
  if (!value) return "—";
  const date = typeof value === "string" ? new Date(value) : value;
  return dateFormat("datetime", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(date);
}

export function formatTime(value: string | Date) {
  const date = typeof value === "string" ? new Date(value) : value;
  return dateFormat("time", { hour: "numeric", minute: "2-digit" }).format(date);
}

/** "just now", "5 min ago", "yesterday", or the date. */
export function formatRelative(value: string | Date | null | undefined) {
  if (!value) return "—";
  const date = typeof value === "string" ? new Date(value) : value;
  const seconds = Math.round((Date.now() - date.getTime()) / 1000);
  if (seconds < 45) return "just now";
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  const days = Math.round(hours / 24);
  if (days === 1) return "yesterday";
  if (days < 7) return `${days} days ago`;
  return formatDate(date);
}

/** Today's date in the school's timezone as yyyy-mm-dd. */
export function todayIso() {
  const parts = new Intl.DateTimeFormat("en-CA", { timeZone: active.timeZone, year: "numeric", month: "2-digit", day: "2-digit" })
    .formatToParts(new Date());
  const get = (t: string) => parts.find((p) => p.type === t)?.value;
  return `${get("year")}-${get("month")}-${get("day")}`;
}
