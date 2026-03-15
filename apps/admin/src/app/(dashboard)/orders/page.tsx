// Path: apps/admin/src/app/(dashboard)/orders/page.tsx
"use client";

import React, { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { type ColumnDef } from "@tanstack/react-table";
import { DataTable } from "@/components/DataTable";

// ── Types ─────────────────────────────────────────────────────────────────────

type MoneyDto = { amount: number; currency: string };
type MiniAddr = { city?: string | null; district?: string | null; postalCode?: string | null; country?: string | null };
type OrderRow = {
  orderId: string;
  providerOrderId?: string | null;
  channel: string;
  status: string;
  placedAtUtc: string;
  totalAmount: MoneyDto;
  shipping?: MiniAddr;
  billing?: MiniAddr;
};
type OrdersResponse = { page: number; pageSize: number; total: number; items: OrderRow[] };

// ── Helpers ───────────────────────────────────────────────────────────────────

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
  Pending:   { label: "Beklemede",   cls: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" },
  Paid:      { label: "Ödendi",      cls: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" },
  Fulfilled: { label: "Tamamlandı",  cls: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" },
  Cancelled: { label: "İptal",       cls: "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" },
  Refunded:  { label: "İade",        cls: "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" },
};
function statusBadge(status: string) { return STATUS_MAP[status] || { label: status, cls: "bg-gray-3 text-dark-4 dark:bg-dark-3 dark:text-dark-6" }; }

const CHANNEL_CFG: Record<string, { icon: string; cls: string }> = {
  Ikas:     { icon: "🛒", cls: "bg-primary/10 text-primary" },
  Trendyol: { icon: "🟠", cls: "bg-accent/10 text-accent-dark" },
  Shopify:  { icon: "🟢", cls: "bg-green/10 text-green" },
};
function channelBadge(ch: string) { return CHANNEL_CFG[ch] || { icon: "📦", cls: "bg-gray-3 text-dark-4 dark:bg-dark-3 dark:text-dark-6" }; }

function fmtMoney(m: MoneyDto | null | undefined): string {
  if (!m) return "-";
  return `${new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(m.amount)} ${m.currency}`;
}

function fmtDate(d: string): string {
  if (!d) return "-";
  try { return new Date(d).toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }); }
  catch { return d; }
}

function exportCSV(items: OrderRow[]) {
  const bom = "\uFEFF";
  const headers = ["Sipariş No", "Kanal", "Durum", "Tarih", "Tutar", "Para Birimi", "Teslimat Şehri"];
  const rows = items.map((o) => [
    o.providerOrderId || o.orderId.slice(0, 8), o.channel, statusBadge(o.status).label,
    fmtDate(o.placedAtUtc), o.totalAmount?.amount?.toString() || "", o.totalAmount?.currency || "", o.shipping?.city || "",
  ]);
  const csv = bom + [headers, ...rows].map((r) => r.map((c) => `"${c}"`).join(";")).join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url; a.download = `profiqo-siparisler-${new Date().toISOString().slice(0, 10)}.csv`; a.click();
  URL.revokeObjectURL(url);
}

// ── Columns ───────────────────────────────────────────────────────────────────

const columns: ColumnDef<OrderRow, any>[] = [
  {
    accessorKey: "providerOrderId",
    header: "Sipariş No",
    enableSorting: false,
    cell: ({ row }) => (
      <span className="font-medium text-primary">{row.original.providerOrderId || row.original.orderId.slice(0, 8)}</span>
    ),
  },
  {
    accessorKey: "channel",
    header: "Kanal",
    enableSorting: true,
    cell: ({ getValue }) => {
      const ch = getValue() as string;
      const cb = channelBadge(ch);
      return <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium ${cb.cls}`}><span>{cb.icon}</span> {ch}</span>;
    },
  },
  {
    accessorKey: "status",
    header: "Durum",
    enableSorting: true,
    cell: ({ getValue }) => {
      const sb = statusBadge(getValue() as string);
      return <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${sb.cls}`}>{sb.label}</span>;
    },
  },
  {
    accessorKey: "placedAtUtc",
    header: "Tarih",
    enableSorting: true,
    meta: { tdClassName: "whitespace-nowrap px-4 py-3.5 text-dark-5 dark:text-dark-6" },
    cell: ({ getValue }) => fmtDate(getValue() as string),
  },
  {
    accessorKey: "totalAmount",
    header: "Tutar",
    enableSorting: true,
    meta: { thClassName: "px-4 py-3 text-right", tdClassName: "whitespace-nowrap px-4 py-3.5 text-right font-semibold text-dark dark:text-white" },
    cell: ({ getValue }) => fmtMoney(getValue() as MoneyDto),
    sortingFn: (a, b) => (a.original.totalAmount?.amount ?? 0) - (b.original.totalAmount?.amount ?? 0),
  },
  {
    id: "delivery",
    header: "Teslimat",
    enableSorting: false,
    meta: { tdClassName: "px-4 py-3.5 text-dark-5 dark:text-dark-6" },
    cell: ({ row }) => {
      const s = row.original.shipping;
      return [s?.district, s?.city].filter(Boolean).join(", ") || "-";
    },
  },
];

// ── Component ─────────────────────────────────────────────────────────────────

export default function OrdersPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState<OrdersResponse | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const load = useCallback(async (p: number) => {
    setLoading(true); setErr(null);
    try {
      const res = await fetch(`/api/orders?page=${p}&pageSize=${pageSize}`, { cache: "no-store" });
      const payload = await res.json().catch(() => null);
      if (!res.ok) { setErr(payload?.message || "Siparişler yüklenemedi."); setData(null); return; }
      setData(payload);
    } catch { setErr("Bağlantı hatası oluştu."); setData(null); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { load(page); }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-dark dark:text-white">Siparişler</h1>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">Tüm kanallardan gelen sipariş kayıtları.</p>
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
            <h2 className="text-base font-semibold text-dark dark:text-white">Sipariş Listesi</h2>
            {data && <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">{data.total} sipariş</span>}
          </div>
          {data && data.items.length > 0 && (
            <button onClick={() => exportCSV(data.items)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-stroke px-3 text-xs font-medium text-dark-5 transition-colors hover:border-primary hover:text-primary dark:border-dark-3 dark:text-dark-6 dark:hover:border-primary dark:hover:text-primary">
              <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3" /></svg>
              CSV
            </button>
          )}
        </div>

        <DataTable
          columns={columns}
          data={data?.items ?? []}
          loading={loading}
          getRowId={(row) => row.orderId}
          onRowClick={(row) => router.push(`/orders/${row.original.orderId}`)}
          emptyTitle="Henüz sipariş kaydı yok."
          pagination={data ? { page, pageSize, total: data.total, onPageChange: setPage } : undefined}
        />
      </div>
    </div>
  );
}