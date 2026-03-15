// Path: apps/admin/src/app/(dashboard)/customers/[customerId]/page.tsx
"use client";

import React, { useEffect, useMemo, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { type ColumnDef } from "@tanstack/react-table";
import { DataTable } from "@/components/DataTable";

// ── Types ─────────────────────────────────────────────────────────────────────

type IdentityDto = {
  type: string;
  valueHash: string;
  sourceProvider: string | null;
  sourceExternalId: string | null;
  firstSeenAtUtc: string;
  lastSeenAtUtc: string;
};
type CustomerDto = {
  customerId: string;
  canonicalCustomerId?: string;
  mergedFromCustomerIds?: string[];
  firstName: string | null;
  lastName: string | null;
  firstSeenAtUtc: string;
  lastSeenAtUtc: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  identities: IdentityDto[];
  rfm: any;
  ai: any;
};
type OrderRow = {
  orderId: string;
  providerOrderId: string | null;
  channel: string;
  status: string;
  placedAtUtc: string;
  totalAmount: number;
  totalCurrency: string;
  netProfit: number;
  netProfitCurrency: string;
  lineCount: number;
};

// ── Helpers ───────────────────────────────────────────────────────────────────

const IDENTITY_TYPES: Record<string, { label: string; cls: string }> = {
  "1": { label: "E-posta", cls: "bg-blue-light-5 text-blue-dark dark:bg-blue/10 dark:text-blue-light" },
  Email: { label: "E-posta", cls: "bg-blue-light-5 text-blue-dark dark:bg-blue/10 dark:text-blue-light" },
  "2": { label: "Telefon", cls: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" },
  Phone: { label: "Telefon", cls: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" },
  "3": { label: "Cihaz", cls: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" },
  Device: { label: "Cihaz", cls: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" },
  "4": { label: "Sağlayıcı", cls: "bg-accent/10 text-accent-dark dark:bg-accent/10 dark:text-accent-light" },
  Provider: { label: "Sağlayıcı", cls: "bg-accent/10 text-accent-dark dark:bg-accent/10 dark:text-accent-light" },
};
function identityMeta(type: string) { return IDENTITY_TYPES[type] || { label: type, cls: "bg-gray-3 text-dark-4 dark:bg-dark-3 dark:text-dark-6" }; }

const RFM_SEGMENTS: Record<number, { label: string; cls: string }> = {
  1: { label: "Champions", cls: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" },
  2: { label: "Loyal", cls: "bg-blue-light-5 text-blue-dark dark:bg-blue/10 dark:text-blue-light" },
  3: { label: "Potential Loyalist", cls: "bg-primary/10 text-primary" },
  4: { label: "New Customer", cls: "bg-accent/10 text-accent-dark dark:bg-accent/10 dark:text-accent-light" },
  8: { label: "At Risk", cls: "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" },
  9: { label: "Can't Lose", cls: "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" },
  10: { label: "Hibernating", cls: "bg-gray-3 text-dark-4 dark:bg-dark-3 dark:text-dark-6" },
  11: { label: "Lost", cls: "bg-gray-3 text-dark-5 dark:bg-dark-3 dark:text-dark-6" },
};

const CHANNEL_ICONS: Record<string, string> = { Ikas: "🛒", Trendyol: "🟠", Shopify: "🟢", Instagram: "📸" };

function fmtDate(s: string) {
  try { return new Date(s).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }); }
  catch { return s; }
}
function fmtMoney(a: number, c: string) {
  return `${a.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${c}`;
}
function getInitials(f: string | null, l: string | null): string {
  const fi = (f || "").trim().charAt(0).toUpperCase();
  const li = (l || "").trim().charAt(0).toUpperCase();
  return fi + li || "?";
}

// ── Orders Columns ────────────────────────────────────────────────────────────

const orderColumns: ColumnDef<OrderRow, any>[] = [
  {
    accessorKey: "providerOrderId",
    header: "Sipariş No",
    enableSorting: false,
    cell: ({ row }) => <span className="font-medium text-primary">{row.original.providerOrderId || row.original.orderId.slice(0, 8)}</span>,
  },
  {
    accessorKey: "channel",
    header: "Kanal",
    enableSorting: true,
    cell: ({ getValue }) => {
      const ch = getValue() as string;
      return <span className="inline-flex items-center gap-1 text-xs">{CHANNEL_ICONS[ch] || "📍"} {ch}</span>;
    },
  },
  {
    accessorKey: "placedAtUtc",
    header: "Tarih",
    enableSorting: true,
    meta: { tdClassName: "whitespace-nowrap px-4 py-3 text-xs text-dark-5 dark:text-dark-6" },
    cell: ({ getValue }) => fmtDate(getValue() as string),
  },
  {
    accessorKey: "totalAmount",
    header: "Tutar",
    enableSorting: true,
    meta: { thClassName: "px-4 py-3 text-right", tdClassName: "whitespace-nowrap px-4 py-3 text-right font-medium text-dark dark:text-white" },
    cell: ({ row }) => fmtMoney(row.original.totalAmount, row.original.totalCurrency),
    sortingFn: (a, b) => a.original.totalAmount - b.original.totalAmount,
  },
  {
    accessorKey: "netProfit",
    header: "Kâr",
    enableSorting: true,
    meta: { thClassName: "px-4 py-3 text-right", tdClassName: "whitespace-nowrap px-4 py-3 text-right font-medium" },
    cell: ({ row }) => {
      const p = row.original.netProfit;
      const cls = p > 0 ? "text-green dark:text-green-light" : p < 0 ? "text-red dark:text-red-light" : "text-dark-5 dark:text-dark-6";
      return <span className={cls}>{fmtMoney(p, row.original.netProfitCurrency)}</span>;
    },
    sortingFn: (a, b) => a.original.netProfit - b.original.netProfit,
  },
  {
    accessorKey: "lineCount",
    header: "Satır",
    enableSorting: true,
    meta: { thClassName: "px-4 py-3 text-center", tdClassName: "px-4 py-3 text-center text-xs text-dark-5 dark:text-dark-6" },
  },
];

// ── Component ─────────────────────────────────────────────────────────────────

export default function CustomerDetailPage() {
  const params = useParams<{ customerId: string }>();
  const router = useRouter();
  const customerId = params?.customerId;

  const [cust, setCust] = useState<CustomerDto | null>(null);
  const [orders, setOrders] = useState<OrderRow[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!customerId) return;
    setErr(null); setLoading(true);
    Promise.all([
      fetch(`/api/customers/${customerId}`, { cache: "no-store" }).then(async (r) => { if (!r.ok) throw new Error((await r.json().catch(() => null))?.message || "Müşteri yüklenemedi."); return r.json(); }),
      fetch(`/api/customers/${customerId}/orders`, { cache: "no-store" }).then(async (r) => { if (!r.ok) throw new Error((await r.json().catch(() => null))?.message || "Siparişler yüklenemedi."); return r.json(); }),
    ])
      .then(([custData, ordersData]) => { setCust(custData); setOrders(ordersData?.items || []); })
      .catch((e) => setErr(e.message))
      .finally(() => setLoading(false));
  }, [customerId]);

  if (loading) {
    return (
      <div className="space-y-6">
        <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark">
          <div className="flex items-center gap-4">
            <div className="h-14 w-14 animate-pulse rounded-full bg-gray-200 dark:bg-dark-3" />
            <div className="space-y-2"><div className="h-5 w-48 animate-pulse rounded bg-gray-200 dark:bg-dark-3" /><div className="h-3 w-32 animate-pulse rounded bg-gray-200 dark:bg-dark-3" /></div>
          </div>
        </div>
      </div>
    );
  }

  if (err || !cust) {
    return (
      <div className="space-y-4">
        <button onClick={() => router.push("/customers")} className="inline-flex items-center gap-1.5 text-sm text-primary hover:underline">
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M15.75 19.5L8.25 12l7.5-7.5" /></svg>
          Müşterilere Dön
        </button>
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <svg className="h-5 w-5 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          <p className="text-sm text-red-600 dark:text-red-300">{err || "Müşteri bulunamadı."}</p>
        </div>
      </div>
    );
  }

  const name = `${cust.firstName || ""} ${cust.lastName || ""}`.trim() || "(İsimsiz Müşteri)";
  const rfm = cust.rfm;
  const ai = cust.ai;
  const rfmSeg = rfm?.segment ? RFM_SEGMENTS[rfm.segment] : null;
  const totalSpent = orders.reduce((s, o) => s + (o.totalAmount || 0), 0);
  const totalProfit = orders.reduce((s, o) => s + (o.netProfit || 0), 0);
  const currency = orders[0]?.totalCurrency || "TRY";
  const isMerged = cust.canonicalCustomerId && cust.canonicalCustomerId !== cust.customerId;

  return (
    <div className="space-y-6">
      <button onClick={() => router.push("/customers")} className="inline-flex items-center gap-1.5 text-sm text-primary hover:underline">
        <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M15.75 19.5L8.25 12l7.5-7.5" /></svg>
        Müşterilere Dön
      </button>

      {/* Customer Header */}
      <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xl font-bold text-primary">{getInitials(cust.firstName, cust.lastName)}</div>
            <div>
              <h1 className="text-xl font-bold text-dark dark:text-white">{name}</h1>
              <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-dark-5 dark:text-dark-6">
                <span>İlk görülme: {fmtDate(cust.firstSeenAtUtc)}</span>
                <span className="text-dark-6/40">|</span>
                <span>Son görülme: {fmtDate(cust.lastSeenAtUtc)}</span>
              </div>
              {isMerged && <span className="mt-1.5 inline-flex items-center gap-1 rounded-full bg-accent/10 px-2.5 py-0.5 text-[10px] font-semibold text-accent-dark">Birleştirilmiş Profil</span>}
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            {rfmSeg && <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${rfmSeg.cls}`}>{rfmSeg.label}</span>}
            {ai?.churnRiskScore != null && (
              <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${ai.churnRiskScore > 0.6 ? "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" : ai.churnRiskScore > 0.3 ? "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" : "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3"}`}>
                Kayıp Riski: %{Math.round(ai.churnRiskScore * 100)}
              </span>
            )}
          </div>
        </div>

        {/* KPI Cards */}
        <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div className="rounded-lg bg-primary/5 p-3 dark:bg-primary/10"><div className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Sipariş</div><div className="mt-1 text-lg font-bold text-dark dark:text-white">{orders.length}</div></div>
          <div className="rounded-lg bg-green-light-7 p-3 dark:bg-green/5"><div className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Toplam Ciro</div><div className="mt-1 text-lg font-bold text-dark dark:text-white">{fmtMoney(totalSpent, currency)}</div></div>
          <div className="rounded-lg bg-blue-light-5 p-3 dark:bg-blue/5"><div className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Net Kâr</div><div className="mt-1 text-lg font-bold text-dark dark:text-white">{fmtMoney(totalProfit, currency)}</div></div>
          <div className="rounded-lg bg-yellow-light-4 p-3 dark:bg-yellow-dark/5"><div className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">LTV (12ay)</div><div className="mt-1 text-lg font-bold text-dark dark:text-white">{ai?.ltv12mProfit != null ? fmtMoney(ai.ltv12mProfit, currency) : "-"}</div></div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Identities */}
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-4 text-base font-semibold text-dark dark:text-white">Kimlik Bilgileri</h3>
          {cust.identities.length === 0 ? (
            <div className="py-6 text-center text-sm text-dark-5 dark:text-dark-6">Kimlik bilgisi bulunamadı.</div>
          ) : (
            <div className="space-y-2.5">
              {cust.identities.map((ident, idx) => {
                const meta = identityMeta(ident.type);
                const source = ident.sourceProvider ? (ident.sourceProvider === "1" ? "Ikas" : ident.sourceProvider === "2" ? "Trendyol" : ident.sourceProvider === "3" ? "Shopify" : ident.sourceProvider) : null;
                return (
                  <div key={idx} className="rounded-lg border border-stroke px-4 py-3 dark:border-dark-3">
                    <div className="flex items-center justify-between">
                      <span className={`rounded-full px-2.5 py-0.5 text-[10px] font-semibold ${meta.cls}`}>{meta.label}</span>
                      <span className="text-[10px] text-dark-5 dark:text-dark-6">{fmtDate(ident.lastSeenAtUtc)}</span>
                    </div>
                    <div className="mt-2 space-y-1">
                      {source && <div className="flex items-center gap-2 text-xs"><span className="text-dark-5 dark:text-dark-6">Kaynak:</span><span className="font-medium text-dark dark:text-white">{source}</span></div>}
                      {ident.sourceExternalId && <div className="flex items-center gap-2 text-xs"><span className="text-dark-5 dark:text-dark-6">Harici ID:</span><span className="font-mono text-dark dark:text-white">{ident.sourceExternalId}</span></div>}
                      <div className="flex items-center gap-2 text-xs"><span className="text-dark-5 dark:text-dark-6">Hash:</span><span className="font-mono text-dark-6 dark:text-dark-5">{ident.valueHash.slice(0, 12)}…</span></div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* RFM & AI */}
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-4 text-base font-semibold text-dark dark:text-white">AI & RFM Analizi</h3>
          {!rfm && !ai ? (
            <div className="py-6 text-center text-sm text-dark-5 dark:text-dark-6">Henüz AI/RFM verisi hesaplanmamış.</div>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2">
              {rfm && (<>
                <div className="rounded-lg bg-gray-1 p-3 dark:bg-dark-2/50"><div className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Recency (R)</div><div className="mt-1 text-lg font-bold text-dark dark:text-white">{rfm.r ?? "-"}</div></div>
                <div className="rounded-lg bg-gray-1 p-3 dark:bg-dark-2/50"><div className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Frequency (F)</div><div className="mt-1 text-lg font-bold text-dark dark:text-white">{rfm.f ?? "-"}</div></div>
                <div className="rounded-lg bg-gray-1 p-3 dark:bg-dark-2/50"><div className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Monetary (M)</div><div className="mt-1 text-lg font-bold text-dark dark:text-white">{rfm.m ?? "-"}</div></div>
              </>)}
              {ai?.nextPurchaseAtUtc && <div className="rounded-lg bg-yellow-light-4/50 p-3 dark:bg-yellow-dark/5"><div className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Tahmini Sonraki Alım</div><div className="mt-1 text-sm font-semibold text-dark dark:text-white">{fmtDate(ai.nextPurchaseAtUtc)}</div></div>}
              {ai?.discountSensitivityScore != null && <div className="rounded-lg bg-accent/5 p-3 dark:bg-accent/5"><div className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">İndirim Hassasiyeti</div><div className="mt-1 text-lg font-bold text-dark dark:text-white">%{Math.round(ai.discountSensitivityScore * 100)}</div></div>}
            </div>
          )}
        </div>
      </div>

      {/* Orders — DataTable */}
      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex items-center justify-between border-b border-stroke px-5 py-4 dark:border-dark-3">
          <h3 className="text-base font-semibold text-dark dark:text-white">Siparişler</h3>
          <span className="rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-semibold text-primary">{orders.length}</span>
        </div>
        <DataTable
          columns={orderColumns}
          data={orders}
          getRowId={(row) => row.orderId}
          onRowClick={(row) => router.push(`/orders/${row.original.orderId}`)}
          emptyTitle="Bu müşteriye ait sipariş bulunamadı."
          initialSorting={[{ id: "placedAtUtc", desc: true }]}
        />
      </div>
    </div>
  );
}