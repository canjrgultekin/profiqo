// Path: apps/admin/src/app/(dashboard)/reports/abandoned-carts/page.tsx
"use client";

import React, { useEffect, useState } from "react";
import { type ColumnDef } from "@tanstack/react-table";
import { DataTable } from "@/components/DataTable";

// ── Types ─────────────────────────────────────────────────────────────────────

type Row = {
  id: string;
  externalId: string;
  providerType: number;
  customerEmail: string | null;
  customerPhone: string | null;
  lastActivityDateMs: number;
  lastActivityAtUtc: string;
  currencyCode: string | null;
  totalFinalPrice: number | null;
  status: string | null;
  updatedAtUtc: string;
};

type Resp = { page: number; pageSize: number; total: number; items: Row[] };

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmtDate(d: string): string {
  if (!d) return "-";
  return new Date(d).toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

function fmtMoney(amount: number | null, currency: string | null): string {
  if (amount === null || amount === undefined) return "-";
  const formatted = new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount);
  return currency ? `${formatted} ${currency}` : formatted;
}

// ── Columns ───────────────────────────────────────────────────────────────────

const columns: ColumnDef<Row, any>[] = [
  {
    accessorKey: "lastActivityAtUtc",
    header: "Son Aktivite",
    enableSorting: true,
    meta: { thClassName: "px-5 py-3", tdClassName: "px-5 py-3.5 text-dark-5 dark:text-dark-6" },
    cell: ({ getValue }) => fmtDate(getValue() as string),
  },
  {
    accessorKey: "customerEmail",
    header: "E-posta",
    enableSorting: true,
    meta: { tdClassName: "px-4 py-3.5 text-dark dark:text-white" },
    cell: ({ getValue }) => (getValue() as string | null) || <span className="text-dark-6">-</span>,
  },
  {
    accessorKey: "customerPhone",
    header: "Telefon",
    enableSorting: false,
    meta: { tdClassName: "px-4 py-3.5 text-dark dark:text-white" },
    cell: ({ getValue }) => (getValue() as string | null) || <span className="text-dark-6">-</span>,
  },
  {
    accessorKey: "totalFinalPrice",
    header: "Tutar",
    enableSorting: true,
    meta: { thClassName: "px-4 py-3 text-right", tdClassName: "px-4 py-3.5 text-right font-semibold text-dark dark:text-white" },
    cell: ({ row }) => fmtMoney(row.original.totalFinalPrice, row.original.currencyCode),
    sortingFn: (a, b) => (a.original.totalFinalPrice ?? 0) - (b.original.totalFinalPrice ?? 0),
  },
  {
    accessorKey: "status",
    header: "Durum",
    enableSorting: false,
    cell: ({ getValue }) => (
      <span className="inline-flex rounded-full bg-yellow-light-4 px-2.5 py-0.5 text-xs font-medium text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light">
        {(getValue() as string | null) || "Abandoned"}
      </span>
    ),
  },
  {
    accessorKey: "externalId",
    header: "External ID",
    enableSorting: false,
    cell: ({ getValue }) => <span className="font-mono text-xs text-dark-5 dark:text-dark-6">{getValue() as string}</span>,
  },
];

// ── Component ─────────────────────────────────────────────────────────────────

export default function AbandonedCartsReportPage() {
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [data, setData] = useState<Resp | null>(null);

  const load = async () => {
    setLoading(true); setErr(null);
    const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (q.trim()) qs.set("q", q.trim());
    const res = await fetch(`/api/reports/abandoned-checkouts?${qs.toString()}`, { cache: "no-store" });
    const payload = await res.json().catch(() => null);
    if (!res.ok) { setErr(payload?.message || "Terk edilen sepetler yüklenemedi."); setData(null); setLoading(false); return; }
    setData(payload); setLoading(false);
  };

  useEffect(() => { load(); }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  const emptyCartIcon = (
    <svg className="h-12 w-12 text-dark-7 dark:text-dark-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1}>
      <path strokeLinecap="round" strokeLinejoin="round" d="M2.25 3h1.386c.51 0 .955.343 1.087.835l.383 1.437M7.5 14.25a3 3 0 00-3 3h15.75m-12.75-3h11.218c1.121-2.3 2.1-4.684 2.924-7.138a60.114 60.114 0 00-16.536-1.84M7.5 14.25L5.106 5.272M6 20.25a.75.75 0 11-1.5 0 .75.75 0 011.5 0zm12.75 0a.75.75 0 11-1.5 0 .75.75 0 011.5 0z" />
    </svg>
  );

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-dark dark:text-white">Terk Edilen Sepetler</h1>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">Satın alma tamamlanmayan checkout kayıtları</p>
      </div>

      <div className="rounded-xl border border-stroke bg-white p-4 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex flex-wrap items-center gap-3">
          <div className="relative min-w-[200px] max-w-md flex-1">
            <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
              <svg className="h-4 w-4 text-dark-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
              </svg>
            </div>
            <input
              className="w-full rounded-lg border border-stroke bg-transparent py-2.5 pl-10 pr-4 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white"
              value={q} onChange={(e) => setQ(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && (() => { setPage(1); load(); })()}
              placeholder="E-posta, telefon veya externalId ile ara..."
            />
          </div>
          <button onClick={() => { setPage(1); load(); }} className="rounded-lg bg-primary px-5 py-2.5 text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-60" disabled={loading}>Ara</button>
        </div>
      </div>

      {err && (
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red/10">
          <svg className="h-5 w-5 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-red-600 dark:text-red-300">{err}</p>
        </div>
      )}

      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex items-center justify-between border-b border-stroke px-5 py-4 dark:border-dark-3">
          <h2 className="text-base font-semibold text-dark dark:text-white">Sepet Kayıtları</h2>
          <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">{data ? `${data.total} kayıt` : "..."}</span>
        </div>

        <DataTable
          columns={columns}
          data={data?.items ?? []}
          loading={loading}
          getRowId={(row) => row.id}
          emptyIcon={emptyCartIcon}
          emptyTitle="Terk edilen sepet bulunamadı"
          emptyDescription="Henüz terk edilen checkout kaydı yok veya arama kriterinize uygun sonuç bulunamadı."
          pagination={data ? { page, pageSize, total: data.total, onPageChange: setPage } : undefined}
        />
      </div>
    </div>
  );
}