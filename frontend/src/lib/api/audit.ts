import { useQuery } from "@tanstack/react-query";
import { api, type Paged } from "./client";

export type AuditEntry = {
  id: string;
  occurredAt: string;
  userId: string | null;
  userName: string | null;
  action: string;
  entityType: string;
  entityLabel: string;
  entityId: string | null;
  changes: string | null;
  ipAddress: string | null;
};

export function useAudit(query: Record<string, unknown>, enabled = true) {
  return useQuery({
    queryKey: ["access", "audit", query],
    queryFn: ({ signal }) => api.get<Paged<AuditEntry>>("/access/audit", query as Record<string, string>, signal),
    enabled,
    placeholderData: (previous) => previous,
  });
}

const actionVerb: Record<string, string> = { Created: "added", Updated: "updated", Deleted: "removed" };

/** "Saad Malik updated a campus" */
export function describeAudit(entry: AuditEntry): string {
  const who = entry.userName ?? "The system";
  const verb = actionVerb[entry.action] ?? entry.action.replace(/^[a-z]+\./, "").replaceAll("_", " ");
  const article = /^[aeiou]/.test(entry.entityLabel) ? "an" : "a";
  return actionVerb[entry.action] ? `${who} ${verb} ${article} ${entry.entityLabel}` : `${who} — ${verb}`;
}

/** Names of the fields an update touched: "name, phone". */
export function changedFields(entry: AuditEntry): string | null {
  if (entry.action !== "Updated" || !entry.changes) return null;
  try {
    const keys = Object.keys(JSON.parse(entry.changes) as Record<string, unknown>);
    return keys.map((k) => k.replace(/([A-Z])/g, " $1").replace(/\./g, " ").toLowerCase()).slice(0, 4).join(", ");
  } catch {
    return null;
  }
}

/** The record's own name from the change payload ("Iqbal Day", "Gulberg Campus"), when one was captured. */
export function entityName(entry: AuditEntry): string | null {
  if (!entry.changes) return null;
  try {
    const changes = JSON.parse(entry.changes) as Record<string, { to?: unknown }>;
    for (const key of ["name", "title", "fullName", "firstName", "code"]) {
      const value = changes[key]?.to;
      if (typeof value === "string" && value.trim()) return value;
    }
  } catch {
    /* ignore malformed payloads */
  }
  return null;
}

export type AuditGroup = { key: string; first: AuditEntry; count: number; names: string[] };

/**
 * Collapses runs of the same person doing the same thing to the same kind of record within ten minutes
 * ("added 13 calendar events"), which is how people describe their own work.
 */
export function groupAudit(entries: AuditEntry[], windowMs = 10 * 60_000): AuditGroup[] {
  const groups: AuditGroup[] = [];
  for (const entry of entries) {
    const last = groups[groups.length - 1];
    const sameRun =
      last &&
      last.first.userId === entry.userId &&
      last.first.action === entry.action &&
      last.first.entityType === entry.entityType &&
      Math.abs(new Date(last.first.occurredAt).getTime() - new Date(entry.occurredAt).getTime()) <= windowMs;
    const name = entityName(entry);
    if (sameRun) {
      last.count += 1;
      if (name && last.names.length < 4 && !last.names.includes(name)) last.names.push(name);
    } else {
      groups.push({ key: entry.id, first: entry, count: 1, names: name ? [name] : [] });
    }
  }
  return groups;
}

export function describeGroup(group: AuditGroup): string {
  if (group.count === 1) return describeAudit(group.first);
  const who = group.first.userName ?? "The system";
  const verb = actionVerb[group.first.action] ?? group.first.action;
  const label = group.first.entityLabel.endsWith("s") ? group.first.entityLabel : `${group.first.entityLabel}s`;
  return `${who} ${verb} ${group.count} ${label.replace(/ys$/, "ies").replace(/(ch|sh|x)s$/, "$1es")}`;
}
