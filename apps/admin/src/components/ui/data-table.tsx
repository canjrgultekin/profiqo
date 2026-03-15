// Path: apps/admin/src/components/ui/data-table.tsx
"use client";

import React, { useMemo, useState, useCallback } from "react";
import { cn } from "@/lib/utils";

// ── Types ─────────────────────────────────────────────────────────────────────

export type SortDir = "asc" | "desc" | null;

export type ColumnDef<T> = {
  /** Unique key for this column */
  key: string;
  /** Header label */
  header: string;
  /** Accessor: extract raw value from row for sorting/search/export */
  accessor?: (row: T) => string | number | null | undefined;
  /** Custom cell renderer */
  render?: (row: T, index: number) => React.ReactNode;
  /** Enable sorting on this column (default false) */
  sortable?: boolean;
  /** Enable search on this column (default false) */
  searchable?: boolean;
  /** Text alignment */
  align?: "left" | "center" | "right";
  /** Min width */
  minWidth?: string;
  /** Whether to include in CSV export (default true) */
  exportable?: boolean;
};

export type DataTableProps<T> = {
  /** Column definitions */
  columns: ColumnDef<T>[];
  /** Data rows */
  data: T[];
  /** Unique key extractor for each row */
  rowKey: (row: T) => string;
  /** Enable global search (default true) */
  searchable?: boolean;
  /** Search placeholder */
  searchPlaceholder?: string;
  /** Enable CSV export button (default true) */
  exportable?: boolean;
  /** Export filename without extension */
  exportFilename?: string;
  /** Page size (default 25) */
  pageSize?: number;
  /** Show page size selector */
  pageSizeOptions?: number[];
  /** Loading state */
  loading?: boolean;
  /** Empty state message */
  emptyMessage?: string;
  /** Row click handler */
  onRowClick?: (row: T) => void;
  /** Optional header right area (buttons etc.) */
  headerRight?: React.ReactNode;
  /** Title */
  title?: string;
  /** Subtitle / description */
  subtitle?: string;
  /** Total count badge */
  totalLabel?: string;
};

// ── Helpers ───────────────────────────────────────────────────────────────────

function normalize(val: unknown): string {
  if (val === null || val === undefined) return "";
  return String(val).toLowerCase().replace(/\s+/g, " ").trim();
}

function compareValues(a: unknown, b: unknown, dir: "asc" | "desc"): number {
  const aNull = a === null || a === undefined || a === "";
  const bNull = b === null || b === undefined || b === "";
  if (aNull && bNull) return 0;
  if (aNull) return 1;
  if (bNull) return -1;

  if (typeof a === "number" && typeof b === "number") {
    return dir === "asc" ? a - b : b - a;
  }

  const aStr = String(a).toLowerCase();
  const bStr = String(b).toLowerCase();

  // Try date comparison
  const aDate = Date.parse(aStr);
  const bDate = Date.parse(bStr);
  if (!isNaN(aDate) && !isNaN(bDate)) {
    return dir === "asc" ? aDate - bDate : bDate - aDate;
  }

  return dir === "asc"
    ? aStr.localeCompare(bStr, "tr")
    : bStr.localeCompare(aStr, "tr");
}

function exportToCsv<T>(columns: ColumnDef<T>[], data: T[], filename: string) {
  const exportCols = columns.filter((c) => c.exportable !== false && c.accessor);

  const header = exportCols.map((c) => `"${c.header.replace(/"/g, '""')}"`).join(";");

  const rows = data.map((row) =>
    exportCols
      .map((col) => {
        const val = col.accessor?.(row);
        if (val === null || val === undefined) return '""';
        const str = String(val).replace(/"/g, '""');
        return `"${str}"`;
      })
      .join(";")
  );

  const bom = "\uFEFF";
  const csv = bom + [header, ...rows].join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `${filename}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}

// ── Sort Icon ─────────────────────────────────────────────────────────────────

function SortIcon({ dir }: { dir: SortDir }) {
  return (
    <span className="ml-1 inline-flex flex-col text-[8px] leading-none">
      <span className={cn("transition-colors", dir === "asc" ? "text-primary" : "text-dark-6 dark:text-dark-5")}>▲</span>
      <span className={cn("transition-colors", dir === "desc" ? "text-primary" : "text-dark-6 dark:text-dark-5")}>▼</span>
    </span>
  );
}

// ── Skeleton Rows ─────────────────────────────────────────────────────────────

function SkeletonRows({ cols, rows = 8 }: { cols: number; rows?: number }) {
  return (
    <>
      {Array.from({ length: rows }).map((_, i) => (
        <tr key={i} className="border-b border-stroke dark:border-dark-3">
          {Array.from({ length: cols }).map((_, j) => (
            <td key={j} className="px-4 py-3.5">
              <div className="h-4 w-full animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
            </td>
          ))}
        </tr>
      ))}
    </>
  );
}

// ── Main Component ────────────────────────────────────────────────────────────

export function DataTable<T>({
  columns,
  data,
  rowKey,
  searchable = true,
  searchPlaceholder = "Tabloda ara...",
  exportable = true,
  exportFilename = "export",
  pageSize: defaultPageSize = 25,
  pageSizeOptions,
  loading = false,
  emptyMessage = "Kayıt bulunamadı.",
  onRowClick,
  headerRight,
  title,
  subtitle,
  totalLabel,
}: DataTableProps<T>) {
  const [search, setSearch] = useState("");
  const [sortKey, setSortKey] = useState<string | null>(null);
  const [sortDir, setSortDir] = useState<SortDir>(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(defaultPageSize);

  // Reset page on search change
  const handleSearch = useCallback((val: string) => {
    setSearch(val);
    setPage(1);
  }, []);

  // Toggle sort
  const toggleSort = useCallback(
    (key: string) => {
      if (sortKey === key) {
        if (sortDir === "asc") setSortDir("desc");
        else if (sortDir === "desc") {
          setSortKey(null);
          setSortDir(null);
        }
      } else {
        setSortKey(key);
        setSortDir("asc");
      }
      setPage(1);
    },
    [sortKey, sortDir]
  );

  // Searchable columns
  const searchCols = useMemo(
    () => columns.filter((c) => c.searchable && c.accessor),
    [columns]
  );

  // Filtered data
  const filtered = useMemo(() => {
    if (!search.trim() || searchCols.length === 0) return data;
    const q = normalize(search);
    return data.filter((row) =>
      searchCols.some((col) => {
        const val = normalize(col.accessor?.(row));
        return val.includes(q);
      })
    );
  }, [data, search, searchCols]);

  // Sorted data
  const sorted = useMemo(() => {
    if (!sortKey || !sortDir) return filtered;
    const col = columns.find((c) => c.key === sortKey);
    if (!col?.accessor) return filtered;
    return [...filtered].sort((a, b) =>
      compareValues(col.accessor!(a), col.accessor!(b), sortDir)
    );
  }, [filtered, sortKey, sortDir, columns]);

  // Paginated data
  const totalPages = Math.max(1, Math.ceil(sorted.length / pageSize));
  const safePage = Math.min(page, totalPages);
  const pageData = useMemo(
    () => sorted.slice((safePage - 1) * pageSize, safePage * pageSize),
    [sorted, safePage, pageSize]
  );

  // Page range for pagination buttons
  const pageRange = useMemo(() => {
    const range: number[] = [];
    const maxButtons = 5;
    let start = Math.max(1, safePage - Math.floor(maxButtons / 2));
    const end = Math.min(totalPages, start + maxButtons - 1);
    start = Math.max(1, end - maxButtons + 1);
    for (let i = start; i <= end; i++) range.push(i);
    return range;
  }, [safePage, totalPages]);

  return (
    <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
      {/* Header */}
      <div className="flex flex-col gap-4 border-b border-stroke px-5 py-4 dark:border-dark-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          {title && (
            <div>
              <h2 className="text-base font-semibold text-dark dark:text-white">{title}</h2>
              {subtitle && <p className="mt-0.5 text-xs text-dark-5 dark:text-dark-6">{subtitle}</p>}
            </div>
          )}
          {totalLabel && (
            <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">
              {totalLabel}
            </span>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {searchable && searchCols.length > 0 && (
            <div className="relative">
              <input
                type="text"
                value={search}
                onChange={(e) => handleSearch(e.target.value)}
                placeholder={searchPlaceholder}
                className="h-9 w-full rounded-lg border border-stroke bg-transparent pl-9 pr-3 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white sm:w-56"
              />
              <svg
                className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-dark-5 dark:text-dark-6"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth={2}
              >
                <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
              </svg>
            </div>
          )}

          {exportable && sorted.length > 0 && (
            <button
              onClick={() => exportToCsv(columns, sorted, exportFilename)}
              className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-stroke px-3 text-xs font-medium text-dark-5 transition-colors hover:border-primary hover:text-primary dark:border-dark-3 dark:text-dark-6 dark:hover:border-primary dark:hover:text-primary"
            >
              <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3" />
              </svg>
              CSV
            </button>
          )}

          {headerRight}
        </div>
      </div>

      {/* Table */}
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr className="border-b border-stroke text-left text-xs font-medium uppercase tracking-wider text-dark-5 dark:border-dark-3 dark:text-dark-6">
              {columns.map((col) => (
                <th
                  key={col.key}
                  className={cn(
                    "whitespace-nowrap px-4 py-3",
                    col.align === "right" && "text-right",
                    col.align === "center" && "text-center",
                    col.sortable && "cursor-pointer select-none hover:text-primary"
                  )}
                  style={col.minWidth ? { minWidth: col.minWidth } : undefined}
                  onClick={col.sortable ? () => toggleSort(col.key) : undefined}
                >
                  <span className="inline-flex items-center gap-0.5">
                    {col.header}
                    {col.sortable && (
                      <SortIcon dir={sortKey === col.key ? sortDir : null} />
                    )}
                  </span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-stroke dark:divide-dark-3">
            {loading ? (
              <SkeletonRows cols={columns.length} />
            ) : pageData.length === 0 ? (
              <tr>
                <td
                  className="px-5 py-12 text-center text-sm text-dark-5 dark:text-dark-6"
                  colSpan={columns.length}
                >
                  {search ? `"${search}" için sonuç bulunamadı.` : emptyMessage}
                </td>
              </tr>
            ) : (
              pageData.map((row, idx) => (
                <tr
                  key={rowKey(row)}
                  className={cn(
                    "text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2",
                    onRowClick && "cursor-pointer"
                  )}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                >
                  {columns.map((col) => (
                    <td
                      key={col.key}
                      className={cn(
                        "whitespace-nowrap px-4 py-3.5",
                        col.align === "right" && "text-right",
                        col.align === "center" && "text-center"
                      )}
                    >
                      {col.render
                        ? col.render(row, (safePage - 1) * pageSize + idx)
                        : col.accessor
                          ? (col.accessor(row) ?? "-")
                          : "-"}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Footer / Pagination */}
      {!loading && sorted.length > 0 && (
        <div className="flex flex-col items-center justify-between gap-3 border-t border-stroke px-5 py-3.5 dark:border-dark-3 sm:flex-row">
          <div className="flex items-center gap-2 text-sm text-dark-5 dark:text-dark-6">
            <span>
              {sorted.length} kayıttan {(safePage - 1) * pageSize + 1}-
              {Math.min(safePage * pageSize, sorted.length)} arası
            </span>
            {pageSizeOptions && pageSizeOptions.length > 0 && (
              <>
                <span className="text-dark-6">|</span>
                <select
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value));
                    setPage(1);
                  }}
                  className="rounded border border-stroke bg-transparent px-2 py-1 text-xs text-dark outline-none dark:border-dark-3 dark:bg-gray-dark dark:text-white"
                >
                  {pageSizeOptions.map((opt) => (
                    <option key={opt} value={opt}>
                      {opt} satır
                    </option>
                  ))}
                </select>
              </>
            )}
          </div>

          {totalPages > 1 && (
            <div className="flex items-center gap-1">
              <button
                onClick={() => setPage(1)}
                disabled={safePage === 1}
                className="flex h-8 w-8 items-center justify-center rounded text-xs text-dark-5 transition-colors hover:bg-primary hover:text-white disabled:opacity-40 dark:text-dark-6"
              >
                «
              </button>
              <button
                onClick={() => setPage(safePage - 1)}
                disabled={safePage === 1}
                className="flex h-8 w-8 items-center justify-center rounded text-xs text-dark-5 transition-colors hover:bg-primary hover:text-white disabled:opacity-40 dark:text-dark-6"
              >
                ‹
              </button>

              {pageRange.map((p) => (
                <button
                  key={p}
                  onClick={() => setPage(p)}
                  className={cn(
                    "flex h-8 w-8 items-center justify-center rounded text-xs font-medium transition-colors",
                    p === safePage
                      ? "bg-primary text-white"
                      : "text-dark-5 hover:bg-gray-2 dark:text-dark-6 dark:hover:bg-dark-3"
                  )}
                >
                  {p}
                </button>
              ))}

              <button
                onClick={() => setPage(safePage + 1)}
                disabled={safePage >= totalPages}
                className="flex h-8 w-8 items-center justify-center rounded text-xs text-dark-5 transition-colors hover:bg-primary hover:text-white disabled:opacity-40 dark:text-dark-6"
              >
                ›
              </button>
              <button
                onClick={() => setPage(totalPages)}
                disabled={safePage >= totalPages}
                className="flex h-8 w-8 items-center justify-center rounded text-xs text-dark-5 transition-colors hover:bg-primary hover:text-white disabled:opacity-40 dark:text-dark-6"
              >
                »
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}