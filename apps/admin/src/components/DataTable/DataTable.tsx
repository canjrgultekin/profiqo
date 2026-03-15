// Path: apps/admin/src/components/DataTable/DataTable.tsx
"use client";

import React, { useState } from "react";
import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  flexRender,
  type ColumnDef,
  type SortingState,
  type Row,
} from "@tanstack/react-table";

// ── Types ─────────────────────────────────────────────────────────────────────

export type ServerPagination = {
  page: number;
  pageSize: number;
  total: number;
  onPageChange: (page: number) => void;
};

export type DataTableProps<T> = {
  columns: ColumnDef<T, any>[];
  data: T[];
  loading?: boolean;
  emptyIcon?: React.ReactNode;
  emptyTitle?: string;
  emptyDescription?: string;
  pagination?: ServerPagination;
  onRowClick?: (row: Row<T>) => void;
  rowClassName?: (row: Row<T>) => string;
  getRowId?: (row: T) => string;
  skeletonRows?: number;
  initialSorting?: SortingState;
};

// ── Sort Icon ─────────────────────────────────────────────────────────────────

function SortIcon({ direction }: { direction: "asc" | "desc" | false }) {
  if (!direction) {
    return (
      <svg className="ml-1 inline h-3 w-3 text-dark-6/40" viewBox="0 0 16 16" fill="currentColor">
        <path d="M8 3.5l3.5 4h-7L8 3.5zm0 9l-3.5-4h7L8 12.5z" />
      </svg>
    );
  }
  return (
    <svg className="ml-1 inline h-3 w-3 text-primary" viewBox="0 0 16 16" fill="currentColor">
      {direction === "asc" ? (
        <path d="M8 3.5l3.5 4h-7L8 3.5z" />
      ) : (
        <path d="M8 12.5l-3.5-4h7L8 12.5z" />
      )}
    </svg>
  );
}

// ── Pagination ────────────────────────────────────────────────────────────────

function PaginationBar({ page, pageSize, total, onPageChange }: ServerPagination) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  if (totalPages <= 1 && total <= pageSize) return null;

  const start = (page - 1) * pageSize + 1;
  const end = Math.min(page * pageSize, total);

  // Page range (max 5 visible)
  const maxVisible = 5;
  let rangeStart = Math.max(1, page - Math.floor(maxVisible / 2));
  const rangeEnd = Math.min(totalPages, rangeStart + maxVisible - 1);
  rangeStart = Math.max(1, rangeEnd - maxVisible + 1);
  const range: number[] = [];
  for (let i = rangeStart; i <= rangeEnd; i++) range.push(i);

  const btnCls = "flex h-8 w-8 items-center justify-center rounded text-xs text-dark-5 transition-colors hover:bg-primary hover:text-white disabled:opacity-40 dark:text-dark-6";

  return (
    <div className="flex flex-col items-center justify-between gap-3 border-t border-stroke px-5 py-3.5 dark:border-dark-3 sm:flex-row">
      <span className="text-sm text-dark-5 dark:text-dark-6">
        {total} kayıttan {start}-{end} arası
      </span>
      {totalPages > 1 && (
        <div className="flex items-center gap-1">
          <button onClick={() => onPageChange(1)} disabled={page === 1} className={btnCls}>«</button>
          <button onClick={() => onPageChange(page - 1)} disabled={page === 1} className={btnCls}>‹</button>
          {range.map((p) => (
            <button
              key={p}
              onClick={() => onPageChange(p)}
              className={`flex h-8 w-8 items-center justify-center rounded text-xs font-medium transition-colors ${
                p === page
                  ? "bg-primary text-white"
                  : "text-dark-5 hover:bg-gray-2 dark:text-dark-6 dark:hover:bg-dark-3"
              }`}
            >
              {p}
            </button>
          ))}
          <button onClick={() => onPageChange(page + 1)} disabled={page >= totalPages} className={btnCls}>›</button>
          <button onClick={() => onPageChange(totalPages)} disabled={page >= totalPages} className={btnCls}>»</button>
        </div>
      )}
    </div>
  );
}

// ── Skeleton ──────────────────────────────────────────────────────────────────

function SkeletonRows({ rows, cols }: { rows: number; cols: number }) {
  return (
    <>
      {Array.from({ length: rows }).map((_, i) => (
        <tr key={i}>
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

// ── DataTable ─────────────────────────────────────────────────────────────────

export function DataTable<T>({
  columns,
  data,
  loading = false,
  emptyIcon,
  emptyTitle = "Kayıt bulunamadı.",
  emptyDescription,
  pagination,
  onRowClick,
  rowClassName,
  getRowId,
  skeletonRows = 8,
  initialSorting = [],
}: DataTableProps<T>) {
  const [sorting, setSorting] = useState<SortingState>(initialSorting);

  const table = useReactTable({
    data,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getRowId: getRowId ? (row) => getRowId(row) : undefined,
    manualPagination: true,
  });

  const headerGroups = table.getHeaderGroups();
  const rows = table.getRowModel().rows;
  const colCount = columns.length;

  return (
    <>
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            {headerGroups.map((hg) => (
              <tr
                key={hg.id}
                className="border-b border-stroke text-left text-xs font-medium uppercase tracking-wider text-dark-5 dark:border-dark-3 dark:text-dark-6"
              >
                {hg.headers.map((header) => {
                  const canSort = header.column.getCanSort();
                  const sorted = header.column.getIsSorted();
                  const meta = header.column.columnDef.meta as Record<string, any> | undefined;
                  const thCls = meta?.thClassName || "px-4 py-3";

                  return (
                    <th
                      key={header.id}
                      className={`${thCls} ${canSort ? "cursor-pointer select-none" : ""}`}
                      onClick={canSort ? header.column.getToggleSortingHandler() : undefined}
                      style={header.column.columnDef.size ? { width: header.column.columnDef.size } : undefined}
                    >
                      <span className="inline-flex items-center gap-0.5">
                        {header.isPlaceholder
                          ? null
                          : flexRender(header.column.columnDef.header, header.getContext())}
                        {canSort && <SortIcon direction={sorted} />}
                      </span>
                    </th>
                  );
                })}
              </tr>
            ))}
          </thead>
          <tbody className="divide-y divide-stroke dark:divide-dark-3">
            {loading ? (
              <SkeletonRows rows={skeletonRows} cols={colCount} />
            ) : rows.length > 0 ? (
              rows.map((row) => (
                <tr
                  key={row.id}
                  className={`text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2 ${
                    onRowClick ? "cursor-pointer" : ""
                  } ${rowClassName ? rowClassName(row) : ""}`}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                >
                  {row.getVisibleCells().map((cell) => {
                    const meta = cell.column.columnDef.meta as Record<string, any> | undefined;
                    const tdCls = meta?.tdClassName || "px-4 py-3.5";
                    return (
                      <td key={cell.id} className={tdCls}>
                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                      </td>
                    );
                  })}
                </tr>
              ))
            ) : (
              <tr>
                <td className="px-5 py-12 text-center text-sm text-dark-5 dark:text-dark-6" colSpan={colCount}>
                  <div className="flex flex-col items-center gap-2">
                    {emptyIcon}
                    <p className="font-medium text-dark dark:text-white">{emptyTitle}</p>
                    {emptyDescription && <p className="mt-0.5 text-xs">{emptyDescription}</p>}
                  </div>
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      {!loading && pagination && pagination.total > 0 && (
        <PaginationBar {...pagination} />
      )}
    </>
  );
}