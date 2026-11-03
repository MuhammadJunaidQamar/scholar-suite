"use client";

import * as React from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";

export type ListParams = {
  page: number;
  pageSize: number;
  sort?: string;
  dir?: "asc" | "desc";
  search?: string;
  filters: Record<string, string[]>;
};

const RESERVED = new Set(["page", "pageSize", "sort", "dir", "search"]);

/**
 * List state (page, sort, search, filters) lives in the URL so views are shareable, survive reloads and work with
 * the back button. Filters are repeated query keys: `?status=Active&status=Suspended&grade=…`.
 */
export function useListParams(defaults?: { pageSize?: number; sort?: string; dir?: "asc" | "desc" }) {
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  const params = React.useMemo<ListParams>(() => {
    const filters: Record<string, string[]> = {};
    searchParams.forEach((value, key) => {
      if (RESERVED.has(key)) return;
      (filters[key] ??= []).push(value);
    });
    const dir = searchParams.get("dir");
    return {
      page: Math.max(1, Number(searchParams.get("page")) || 1),
      pageSize: Number(searchParams.get("pageSize")) || defaults?.pageSize || 25,
      sort: searchParams.get("sort") ?? defaults?.sort,
      dir: dir === "asc" || dir === "desc" ? dir : defaults?.dir,
      search: searchParams.get("search") ?? undefined,
      filters,
    };
  }, [searchParams, defaults?.pageSize, defaults?.sort, defaults?.dir]);

  const update = React.useCallback(
    (patch: Partial<Omit<ListParams, "filters">> & { filters?: Record<string, string[] | undefined> }, resetPage = true) => {
      const next = new URLSearchParams(searchParams.toString());
      const set = (key: string, value: string | number | undefined) => {
        if (value === undefined || value === "" || value === null) next.delete(key);
        else next.set(key, String(value));
      };
      if ("page" in patch) set("page", patch.page === 1 ? undefined : patch.page);
      if ("pageSize" in patch) set("pageSize", patch.pageSize);
      if ("sort" in patch) set("sort", patch.sort);
      if ("dir" in patch) set("dir", patch.dir);
      if ("search" in patch) set("search", patch.search?.trim() || undefined);
      if (patch.filters) {
        for (const [key, values] of Object.entries(patch.filters)) {
          next.delete(key);
          values?.forEach((v) => next.append(key, v));
        }
      }
      if (resetPage && !("page" in patch)) next.delete("page");
      const query = next.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [pathname, router, searchParams],
  );

  const clearFilters = React.useCallback(() => {
    const next = new URLSearchParams();
    const keep = ["pageSize", "sort", "dir"];
    keep.forEach((k) => {
      const v = searchParams.get(k);
      if (v) next.set(k, v);
    });
    const query = next.toString();
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  }, [pathname, router, searchParams]);

  /** Query object for the API: filters are flattened (single values as strings, multi as arrays). */
  const apiQuery = React.useMemo(() => {
    const query: Record<string, string | number | string[] | undefined> = {
      page: params.page,
      pageSize: params.pageSize,
      sort: params.sort,
      dir: params.dir,
      search: params.search,
    };
    for (const [key, values] of Object.entries(params.filters)) {
      query[key] = values.length === 1 ? values[0] : values;
    }
    return query;
  }, [params]);

  const activeFilterCount = Object.values(params.filters).reduce((n, v) => n + (v.length > 0 ? 1 : 0), 0) + (params.search ? 1 : 0);

  return { params, update, clearFilters, apiQuery, activeFilterCount };
}

export function useDebouncedValue<T>(value: T, delay = 250): T {
  const [debounced, setDebounced] = React.useState(value);
  React.useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(handle);
  }, [value, delay]);
  return debounced;
}
