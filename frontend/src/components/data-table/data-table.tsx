"use client";

import * as React from "react";
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
  type RowData,
  type RowSelectionState,
  type VisibilityState,
} from "@tanstack/react-table";
import { useRouter } from "next/navigation";
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, ChevronsUpDown, Ellipsis } from "lucide-react";
import { cn } from "@/lib/utils";
import { formatNumber } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/toggles";
import { NativeSelect } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/display";
import { EmptyState, ErrorState } from "@/components/ui/states";
import { DropdownMenu, DropdownMenuContent, DropdownMenuTrigger } from "@/components/ui/menu";
import type { ListParams } from "./use-list-params";

declare module "@tanstack/react-table" {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  interface ColumnMeta<TData extends RowData, TValue> {
    /** Column title used in the column-visibility menu (defaults to the header string). */
    label?: string;
    align?: "left" | "right" | "center";
    /** Server sort key; omit for unsortable columns. */
    sortKey?: string;
    /** Hide on narrow screens. */
    hideBelow?: "sm" | "md" | "lg";
    /** Cannot be hidden by the user. */
    pinned?: boolean;
    width?: string;
    className?: string;
  }
}

export type Density = "comfortable" | "compact";

type DataTableProps<T> = {
  columns: ColumnDef<T, unknown>[];
  rows: T[] | undefined;
  total: number;
  params: Pick<ListParams, "page" | "pageSize" | "sort" | "dir">;
  onParamsChange: (patch: Partial<ListParams>) => void;
  getRowId: (row: T) => string;
  loading?: boolean;
  fetching?: boolean;
  error?: unknown;
  onRetry?: () => void;
  onRowClick?: (row: T) => void;
  rowHref?: (row: T) => string;
  selectable?: boolean;
  selection?: RowSelectionState;
  onSelectionChange?: (selection: RowSelectionState) => void;
  bulkActions?: (selectedIds: string[], clear: () => void) => React.ReactNode;
  empty?: React.ReactNode;
  emptyFiltered?: React.ReactNode;
  filtered?: boolean;
  density?: Density;
  columnVisibility?: VisibilityState;
  onColumnVisibilityChange?: (state: VisibilityState) => void;
  rowActions?: (row: T) => React.ReactNode;
  className?: string;
  caption?: string;
};

const hideBelowClass = { sm: "max-sm:hidden", md: "max-md:hidden", lg: "max-lg:hidden" } as const;

/**
 * Server-driven table: the API pages, sorts and filters; the table renders, selects and navigates.
 * Rows are ruled, never boxed; numbers align right in tabular figures.
 */
export function DataTable<T>({
  columns,
  rows,
  total,
  params,
  onParamsChange,
  getRowId,
  loading,
  fetching,
  error,
  onRetry,
  onRowClick,
  rowHref,
  selectable,
  selection = {},
  onSelectionChange,
  bulkActions,
  empty,
  emptyFiltered,
  filtered,
  density = "comfortable",
  columnVisibility,
  onColumnVisibilityChange,
  rowActions,
  className,
  caption,
}: DataTableProps<T>) {
  const router = useRouter();
  const allColumns = React.useMemo<ColumnDef<T, unknown>[]>(() => {
    const list = [...columns];
    if (selectable) {
      list.unshift({
        id: "__select",
        meta: { pinned: true, width: "2.5rem" },
        header: ({ table }) => (
          <Checkbox
            aria-label="Select all on this page"
            checked={table.getIsAllRowsSelected() ? true : table.getIsSomeRowsSelected() ? "indeterminate" : false}
            onCheckedChange={(v) => table.toggleAllRowsSelected(v === true)}
          />
        ),
        cell: ({ row }) => (
          <Checkbox
            aria-label="Select row"
            checked={row.getIsSelected()}
            onCheckedChange={(v) => row.toggleSelected(v === true)}
            onClick={(e) => e.stopPropagation()}
          />
        ),
      });
    }
    if (rowActions) {
      list.push({
        id: "__actions",
        meta: { pinned: true, align: "right", width: "3rem" },
        header: () => <span className="sr-only">Actions</span>,
        cell: ({ row }) => (
          <div onClick={(e) => e.stopPropagation()} className="flex justify-end">
            <DropdownMenu modal={false}>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon-sm" aria-label="Row actions"
                  className="opacity-0 group-hover/row:opacity-100 focus-visible:opacity-100 data-[state=open]:opacity-100 [@media(hover:none)]:opacity-100">
                  <Ellipsis />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent>{rowActions(row.original)}</DropdownMenuContent>
            </DropdownMenu>
          </div>
        ),
      });
    }
    return list;
  }, [columns, selectable, rowActions]);

  // Rows are not memoised by TanStack's API (it returns fresh functions each render); the table is cheap enough.
  // eslint-disable-next-line react-hooks/incompatible-library
  const table = useReactTable({
    data: rows ?? [],
    columns: allColumns,
    getRowId,
    getCoreRowModel: getCoreRowModel(),
    manualPagination: true,
    manualSorting: true,
    rowCount: total,
    enableRowSelection: selectable,
    state: { rowSelection: selection, columnVisibility: columnVisibility ?? {} },
    onRowSelectionChange: (updater) => {
      const next = typeof updater === "function" ? updater(selection) : updater;
      onSelectionChange?.(next);
    },
    onColumnVisibilityChange: (updater) => {
      const next = typeof updater === "function" ? updater(columnVisibility ?? {}) : updater;
      onColumnVisibilityChange?.(next);
    },
  });

  const selectedIds = Object.keys(selection).filter((k) => selection[k]);
  const rowHeight = density === "compact" ? "h-[34px]" : "h-[42px]";
  const pageCount = Math.max(1, Math.ceil(total / params.pageSize));
  const from = total === 0 ? 0 : (params.page - 1) * params.pageSize + 1;
  const to = Math.min(total, params.page * params.pageSize);

  const toggleSort = (key: string) => {
    if (params.sort !== key) onParamsChange({ sort: key, dir: "asc" });
    else if (params.dir === "asc") onParamsChange({ sort: key, dir: "desc" });
    else onParamsChange({ sort: undefined, dir: undefined });
  };

  const visibleColumnCount = table.getVisibleLeafColumns().length;

  return (
    <div className={cn("relative", className)}>
      <div className="overflow-x-auto scrollbar-thin">
        <table className="w-full border-collapse text-left">
          {caption && <caption className="sr-only">{caption}</caption>}
          <thead>
            {table.getHeaderGroups().map((group) => (
              <tr key={group.id} className="border-y border-line bg-sunken/50">
                {group.headers.map((header) => {
                  const meta = header.column.columnDef.meta;
                  const sortKey = meta?.sortKey;
                  const sorted = sortKey && params.sort === sortKey ? params.dir : undefined;
                  return (
                    <th
                      key={header.id}
                      scope="col"
                      style={{ width: meta?.width }}
                      aria-sort={sorted === "asc" ? "ascending" : sorted === "desc" ? "descending" : undefined}
                      className={cn(
                        "h-9 px-3 text-xs font-medium whitespace-nowrap text-ink-2 first:pl-4 last:pr-4",
                        meta?.align === "right" && "text-right",
                        meta?.align === "center" && "text-center",
                        meta?.hideBelow && hideBelowClass[meta.hideBelow],
                      )}
                    >
                      {header.isPlaceholder ? null : sortKey ? (
                        <button
                          type="button"
                          onClick={() => toggleSort(sortKey)}
                          className={cn(
                            "group/sort -mx-1 inline-flex items-center gap-1 rounded-sm px-1 py-0.5 hover:text-ink",
                            meta?.align === "right" && "flex-row-reverse",
                            sorted && "text-ink",
                          )}
                        >
                          {flexRender(header.column.columnDef.header, header.getContext())}
                          {sorted === "asc" ? (
                            <ArrowUp className="size-3" />
                          ) : sorted === "desc" ? (
                            <ArrowDown className="size-3" />
                          ) : (
                            <ChevronsUpDown className="size-3 opacity-0 transition-opacity group-hover/sort:opacity-60" />
                          )}
                        </button>
                      ) : (
                        flexRender(header.column.columnDef.header, header.getContext())
                      )}
                    </th>
                  );
                })}
              </tr>
            ))}
          </thead>
          <tbody className={cn(fetching && !loading && "opacity-70 transition-opacity")}>
            {loading ? (
              Array.from({ length: Math.min(params.pageSize, 8) }).map((_, i) => (
                <tr key={i} className={cn("border-b border-line", rowHeight)}>
                  {table.getVisibleLeafColumns().map((column, j) => (
                    <td key={column.id} className={cn("px-3 first:pl-4 last:pr-4", column.columnDef.meta?.hideBelow && hideBelowClass[column.columnDef.meta.hideBelow])}>
                      <Skeleton className={cn("h-3.5", j === 0 ? "w-4" : j === 1 ? "w-40" : "w-20")} />
                    </td>
                  ))}
                </tr>
              ))
            ) : error ? (
              <tr>
                <td colSpan={visibleColumnCount}>
                  <ErrorState error={error} onRetry={onRetry} />
                </td>
              </tr>
            ) : table.getRowModel().rows.length === 0 ? (
              <tr>
                <td colSpan={visibleColumnCount}>
                  {filtered ? (emptyFiltered ?? <EmptyState compact title="No matches" description="Try a different search or clear the filters." />) : empty}
                </td>
              </tr>
            ) : (
              table.getRowModel().rows.map((row) => {
                const interactive = Boolean(onRowClick || rowHref);
                return (
                  <tr
                    key={row.id}
                    data-state={row.getIsSelected() ? "selected" : undefined}
                    onClick={
                      interactive
                        ? (event) => {
                            // Controls inside the row (checkboxes, menus, links) keep their own behaviour.
                            if ((event.target as HTMLElement).closest("a,button,input,label,[role=checkbox],[role=menuitem]")) return;
                            if (rowHref) {
                              const href = rowHref(row.original);
                              if (event.metaKey || event.ctrlKey) window.open(href, "_blank", "noopener");
                              else router.push(href);
                            } else onRowClick?.(row.original);
                          }
                        : undefined
                    }
                    className={cn(
                      "group/row border-b border-line text-base transition-colors",
                      rowHeight,
                      interactive && "cursor-pointer hover:bg-sunken/70",
                      "data-[state=selected]:bg-accent-soft",
                    )}
                  >
                    {row.getVisibleCells().map((cell) => {
                      const meta = cell.column.columnDef.meta;
                      return (
                        <td
                          key={cell.id}
                          className={cn(
                            "px-3 align-middle first:pl-4 last:pr-4",
                            meta?.align === "right" && "text-right tabular",
                            meta?.align === "center" && "text-center",
                            meta?.hideBelow && hideBelowClass[meta.hideBelow],
                            meta?.className,
                          )}
                        >
                          {flexRender(cell.column.columnDef.cell, cell.getContext())}
                        </td>
                      );
                    })}
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {!error && total > 0 && (
        <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm text-ink-2">
          <span className="tabular">
            {formatNumber(from)}–{formatNumber(to)} of {formatNumber(total)}
          </span>
          <div className="flex items-center gap-3">
            <label className="flex items-center gap-2">
              <span className="max-sm:hidden">Rows</span>
              <NativeSelect
                aria-label="Rows per page"
                className="h-7 w-[4.5rem] text-sm"
                value={params.pageSize}
                onChange={(e) => onParamsChange({ pageSize: Number(e.target.value), page: 1 })}
              >
                {[25, 50, 100].map((n) => (
                  <option key={n} value={n}>
                    {n}
                  </option>
                ))}
              </NativeSelect>
            </label>
            <span className="tabular max-sm:hidden">
              Page {params.page} of {pageCount}
            </span>
            <div className="flex gap-1">
              <Button size="icon-sm" variant="secondary" aria-label="Previous page" disabled={params.page <= 1}
                onClick={() => onParamsChange({ page: params.page - 1 })}>
                <ChevronLeft />
              </Button>
              <Button size="icon-sm" variant="secondary" aria-label="Next page" disabled={params.page >= pageCount}
                onClick={() => onParamsChange({ page: params.page + 1 })}>
                <ChevronRight />
              </Button>
            </div>
          </div>
        </div>
      )}

      {selectable && selectedIds.length > 0 && bulkActions && (
        <div className="pointer-events-none fixed inset-x-0 bottom-6 z-40 flex justify-center px-4">
          <div
            role="toolbar"
            aria-label="Bulk actions"
            className="pointer-events-auto flex animate-slide-in-up items-center gap-1 rounded-lg bg-line-ink py-1.5 pr-1.5 pl-4 text-ink-inverse shadow-dialog"
          >
            <span className="mr-2 text-sm font-medium tabular">{formatNumber(selectedIds.length)} selected</span>
            <div className="flex items-center gap-1 [&_button]:text-ink-inverse [&_button:hover]:bg-white/10 dark:[&_button:hover]:bg-black/10">
              {bulkActions(selectedIds, () => onSelectionChange?.({}))}
              <Button size="sm" variant="ghost" onClick={() => onSelectionChange?.({})}>
                Clear
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
