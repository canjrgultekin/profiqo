// Path: apps/admin/src/app/(dashboard)/customers/page.tsx
"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { type ColumnDef } from "@tanstack/react-table";
import { DataTable } from "@/components/DataTable";

// ── Types ─────────────────────────────────────────────────────────────────────

type CustomerRow = {
  customerId: string;
  firstName: string | null;
  lastName: string | null;
  firstSeenAtUtc: string;
  lastSeenAtUtc: string;
  rfmSegment: string | null;
  churnRisk: number | null;
  ltv12mProfit: number | null;
};

type CustomersResponse = {
  page: number;
  pageSize: number;
  total: number;
  items: CustomerRow[];
};

// ── Helpers ───────────────────────────────────────────────────────────────────

function initials(first: string | null, last: string | null): string {
  const f = (first || "").trim();
  const l = (last || "").trim();
  if (f && l) return `${f[0]}${l[0]}`.toUpperCase();
  if (f) return f[0].toUpperCase();
  if (l) return l[0].toUpperCase();
  return "?";
}

const AVATAR_COLORS = [
  "bg-primary/15 text-primary",
  "bg-green/15 text-green",
  "bg-blue/15 text-blue",
  "bg-accent/15 text-accent-dark",
  "bg-red/15 text-red",
];

function avatarColor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = id.charCodeAt(i) + ((hash << 5) - hash);
  return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}

function fullName(r: CustomerRow): string {
  return `${r.firstName || ""} ${r.lastName || ""}`.trim() || "(İsimsiz)";
}

function rfmBadge(segment: string | null): { label: string; cls: string } | null {
  if (!segment) return null;
  const s = segment.toLowerCase();
  if (s.includes("champion")) return { label: segment, cls: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" };
  if (s.includes("loyal")) return { label: segment, cls: "bg-blue-light-5 text-blue-dark dark:bg-blue/10 dark:text-blue-light" };
  if (s.includes("risk") || s.includes("cant")) return { label: segment, cls: "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" };
  if (s.includes("sleep") || s.includes("hibernat") || s.includes("lost")) return { label: segment, cls: "bg-gray-3 text-dark-4 dark:bg-dark-3 dark:text-dark-6" };
  return { label: segment, cls: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" };
}

function churnBadge(risk: number | null): { label: string; cls: string } | null {
  if (risk === null || risk === undefined) return null;
  const pct = Math.round(risk * 100);
  if (pct <= 30) return { label: `%${pct}`, cls: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" };
  if (pct <= 60) return { label: `%${pct}`, cls: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" };
  return { label: `%${pct}`, cls: "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" };
}

function fmtDate(d: string): string {
  try {
    return new Date(d).toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
  } catch { return d; }
}

function fmtNumber(n: number): string {
  return new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 0, maximumFractionDigits: 2 }).format(n);
}

function exportCSV(items: CustomerRow[]) {
  const bom = "\uFEFF";
  const headers = ["Ad Soyad", "RFM Segment", "Kayıp Riski", "LTV 12ay", "Son Görülme"];
  const rows = items.map((x) => [
    fullName(x),
    x.rfmSegment || "",
    x.churnRisk !== null ? `%${Math.round(x.churnRisk * 100)}` : "",
    x.ltv12mProfit?.toString() || "",
    x.lastSeenAtUtc ? fmtDate(x.lastSeenAtUtc) : "",
  ]);
  const csv = bom + [headers, ...rows].map((r) => r.map((c) => `"${c}"`).join(";")).join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `profiqo-musteriler-${new Date().toISOString().slice(0, 10)}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

// ── Columns ───────────────────────────────────────────────────────────────────

const columns: ColumnDef<CustomerRow, any>[] = [
  {
    accessorKey: "firstName",
    header: "Müşteri",
    enableSorting: true,
    meta: { thClassName: "px-5 py-3", tdClassName: "px-5 py-3.5" },
    cell: ({ row }) => {
      const x = row.original;
      const name = fullName(x);
      return (
        <div className="flex items-center gap-3">
          <div className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold ${avatarColor(x.customerId)}`}>
            {initials(x.firstName, x.lastName)}
          </div>
          <span className="font-medium text-dark dark:text-white">{name}</span>
        </div>
      );
    },
    sortingFn: (a, b) => fullName(a.original).localeCompare(fullName(b.original), "tr"),
  },
  {
    accessorKey: "rfmSegment",
    header: "RFM Segment",
    enableSorting: true,
    cell: ({ getValue }) => {
      const rfm = rfmBadge(getValue() as string | null);
      if (!rfm) return <span className="text-dark-6">-</span>;
      return <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${rfm.cls}`}>{rfm.label}</span>;
    },
  },
  {
    accessorKey: "churnRisk",
    header: "Kayıp Riski",
    enableSorting: true,
    cell: ({ getValue }) => {
      const churn = churnBadge(getValue() as number | null);
      if (!churn) return <span className="text-dark-6">-</span>;
      return <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${churn.cls}`}>{churn.label}</span>;
    },
  },
  {
    accessorKey: "ltv12mProfit",
    header: "LTV (12ay)",
    enableSorting: true,
    meta: { thClassName: "px-4 py-3 text-right", tdClassName: "px-4 py-3.5 text-right font-medium text-dark dark:text-white" },
    cell: ({ getValue }) => {
      const v = getValue() as number | null;
      return v !== null ? fmtNumber(v) : "-";
    },
  },
  {
    accessorKey: "lastSeenAtUtc",
    header: "Son Görülme",
    enableSorting: true,
    meta: { tdClassName: "whitespace-nowrap px-4 py-3.5 text-sm text-dark-5 dark:text-dark-6" },
    cell: ({ getValue }) => fmtDate(getValue() as string),
  },
];

// ── Component ─────────────────────────────────────────────────────────────────

export default function CustomersPage() {
  const router = useRouter();
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [data, setData] = useState<CustomersResponse | null>(null);

  const load = useCallback(async (p: number, search: string) => {
    setLoading(true);
    setErr(null);
    const qs = new URLSearchParams({ page: String(p), pageSize: String(pageSize) });
    if (search.trim()) qs.set("q", search.trim());
    try {
      const res = await fetch(`/api/customers?${qs}`, { cache: "no-store" });
      const payload = await res.json().catch(() => null);
      if (!res.ok) { setErr(payload?.message || "Müşteri listesi yüklenemedi."); setData(null); return; }
      setData(payload);
    } catch {
      setErr("Bağlantı hatası oluştu."); setData(null);
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { load(page, q); }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleSearch = () => { setPage(1); load(1, q); };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-dark dark:text-white">Müşteriler</h1>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">Tüm kanallardan birleştirilmiş müşteri verileri.</p>
      </div>

      {err && (
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <svg className="h-5 w-5 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-red-600 dark:text-red-300">{err}</p>
        </div>
      )}

      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex flex-col gap-4 border-b border-stroke px-5 py-4 dark:border-dark-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <h2 className="text-base font-semibold text-dark dark:text-white">Müşteri Listesi</h2>
            {data && <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">{data.total} kayıt</span>}
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative">
              <input
                type="text" value={q} onChange={(e) => setQ(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && handleSearch()}
                placeholder="İsim ile ara..."
                className="h-9 w-full rounded-lg border border-stroke bg-transparent pl-9 pr-3 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white sm:w-56"
              />
              <svg className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-dark-5 dark:text-dark-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" />
              </svg>
            </div>
            <button onClick={handleSearch} disabled={loading} className="h-9 rounded-lg bg-primary px-4 text-xs font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-60">Ara</button>
            {data && data.items.length > 0 && (
              <button onClick={() => exportCSV(data.items)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-stroke px-3 text-xs font-medium text-dark-5 transition-colors hover:border-primary hover:text-primary dark:border-dark-3 dark:text-dark-6 dark:hover:border-primary dark:hover:text-primary">
                <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3" /></svg>
                CSV
              </button>
            )}
          </div>
        </div>

        <DataTable
          columns={columns}
          data={data?.items ?? []}
          loading={loading}
          getRowId={(row) => row.customerId}
          onRowClick={(row) => router.push(`/customers/${row.original.customerId}`)}
          emptyTitle={q.trim() ? `"${q}" için müşteri bulunamadı.` : "Henüz müşteri kaydı yok."}
          pagination={data ? { page, pageSize, total: data.total, onPageChange: setPage } : undefined}
        />
      </div>
    </div>
  );
}