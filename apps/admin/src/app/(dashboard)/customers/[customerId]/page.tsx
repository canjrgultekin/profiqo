"use client";

import React, { useEffect, useState } from "react";
import { useParams } from "next/navigation";

type IdentityDto = { type: string; valueHash: string; sourceProvider: string | null; sourceExternalId: string | null; firstSeenAtUtc: string; lastSeenAtUtc: string };
type CustomerDto = { customerId: string; canonicalCustomerId?: string; mergedFromCustomerIds?: string[]; firstName: string | null; lastName: string | null; firstSeenAtUtc: string; lastSeenAtUtc: string; createdAtUtc: string; updatedAtUtc: string; identities: IdentityDto[]; rfm: any; ai: any };
type OrderRow = { orderId: string; providerOrderId: string | null; channel: string; status: string; placedAtUtc: string; totalAmount: number; totalCurrency: string; netProfit: number; netProfitCurrency: string; lineCount: number };

const rfmSegments: Record<number, { label: string; color: string }> = {
  1: { label: "Champions", color: "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400" },
  2: { label: "Loyal", color: "bg-blue-100 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400" },
  3: { label: "Potential Loyalist", color: "bg-cyan-100 text-cyan-700 dark:bg-cyan-900/20 dark:text-cyan-400" },
  4: { label: "New Customer", color: "bg-purple-100 text-purple-700 dark:bg-purple-900/20 dark:text-purple-400" },
  8: { label: "At Risk", color: "bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400" },
  9: { label: "Can't Lose", color: "bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400" },
  10: { label: "Hibernating", color: "bg-gray-100 text-gray-600 dark:bg-dark-3 dark:text-dark-6" },
  11: { label: "Lost", color: "bg-gray-100 text-gray-500 dark:bg-dark-3 dark:text-dark-6" },
};

const channelIcons: Record<string, string> = { Ikas: "🛒", Trendyol: "🟠", Shopify: "🟢", Instagram: "📸" };

function fmtDate(s: string) { try { return new Date(s).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }); } catch { return s; } }
function fmtMoney(a: number, c: string) { return `${a.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${c}`; }

function getInitials(f: string | null, l: string | null): string {
  const fi = (f || "").trim().charAt(0).toUpperCase();
  const li = (l || "").trim().charAt(0).toUpperCase();
  return fi + li || "?";
}

export default function CustomerDetailPage() {
  const params = useParams<{ customerId: string }>();
  const customerId = params?.customerId;

  const [cust, setCust] = useState<CustomerDto | null>(null);
  const [orders, setOrders] = useState<OrderRow[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async (id: string) => {
    setErr(null); setLoading(true);
    const cRes = await fetch(`/api/customers/${id}`, { cache: "no-store" });
    const cPayload = await cRes.json().catch(() => null);
    if (!cRes.ok) { setErr(cPayload?.message || "Müşteri yüklenemedi."); setLoading(false); return; }
    setCust(cPayload);
    const oRes = await fetch(`/api/customers/${id}/orders`, { cache: "no-store" });
    const oPayload = await oRes.json().catch(() => null);
    if (!oRes.ok) { setErr(oPayload?.message || "Siparişler yüklenemedi."); setLoading(false); return; }
    setOrders(oPayload?.items || []);
    setLoading(false);
  };

  useEffect(() => { if (customerId) load(customerId); }, [customerId]);

  if (loading || !cust) {
    return (
      <div className="p-4 sm:p-6">
        <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark">
          <div className="flex items-center gap-3">
            <div className="h-12 w-12 animate-pulse rounded-full bg-gray-200 dark:bg-dark-3" />
            <div className="space-y-2"><div className="h-4 w-40 animate-pulse rounded bg-gray-200 dark:bg-dark-3" /><div className="h-3 w-24 animate-pulse rounded bg-gray-200 dark:bg-dark-3" /></div>
          </div>
        </div>
      </div>
    );
  }

  if (err) {
    return (
      <div className="p-4 sm:p-6">
        <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 dark:border-red-900/30 dark:bg-red-900/10">
          <svg className="h-5 w-5 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          <span className="text-sm text-red-600 dark:text-red-400">{err}</span>
        </div>
      </div>
    );
  }

  const fullName = `${cust.firstName || ""} ${cust.lastName || ""}`.trim() || cust.customerId;
  const rfm = cust.rfm;
  const ai = cust.ai;
  const rfmSeg = rfm?.segment ? rfmSegments[rfm.segment] : null;
  const totalSpent = orders.reduce((s, o) => s + (o.totalAmount || 0), 0);
  const totalProfit = orders.reduce((s, o) => s + (o.netProfit || 0), 0);
  const currency = orders[0]?.totalCurrency || "TRY";

  return (
    <div className="p-4 sm:p-6">
      {/* Customer Header */}
      <div className="mb-6 rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <div className="flex h-14 w-14 items-center justify-center rounded-full bg-primary/10 text-xl font-bold text-primary">
              {getInitials(cust.firstName, cust.lastName)}
            </div>
            <div>
              <h2 className="text-xl font-bold text-dark dark:text-white">{fullName}</h2>
              <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-body-color dark:text-dark-6">
                <span>İlk görülme: {fmtDate(cust.firstSeenAtUtc)}</span>
                <span className="text-body-color/30">|</span>
                <span>Son görülme: {fmtDate(cust.lastSeenAtUtc)}</span>
              </div>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            {rfmSeg && <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${rfmSeg.color}`}>{rfmSeg.label}</span>}
            {ai?.churnRiskScore != null && (
              <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${ai.churnRiskScore > 0.6 ? "bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400" : ai.churnRiskScore > 0.3 ? "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/20 dark:text-yellow-400" : "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400"}`}>
                Churn: {Math.round(ai.churnRiskScore * 100)}%
              </span>
            )}
          </div>
        </div>

        {/* KPI Cards */}
        <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div className="rounded-lg bg-primary/5 p-3">
            <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Sipariş</div>
            <div className="mt-1 text-lg font-bold text-dark dark:text-white">{orders.length}</div>
          </div>
          <div className="rounded-lg bg-green-50 p-3 dark:bg-green-900/10">
            <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Toplam Ciro</div>
            <div className="mt-1 text-lg font-bold text-dark dark:text-white">{fmtMoney(totalSpent, currency)}</div>
          </div>
          <div className="rounded-lg bg-blue-50 p-3 dark:bg-blue-900/10">
            <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Net Kâr</div>
            <div className="mt-1 text-lg font-bold text-dark dark:text-white">{fmtMoney(totalProfit, currency)}</div>
          </div>
          <div className="rounded-lg bg-yellow-50 p-3 dark:bg-yellow-900/10">
            <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">LTV (12ay)</div>
            <div className="mt-1 text-lg font-bold text-dark dark:text-white">{ai?.ltv12mProfit != null ? fmtMoney(ai.ltv12mProfit, currency) : "-"}</div>
          </div>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Identities */}
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-4 text-base font-semibold text-dark dark:text-white">🔗 Kimlikler</h3>
          {cust.identities.length === 0 ? (
            <div className="py-6 text-center text-sm text-body-color dark:text-dark-6">Identity bulunamadı</div>
          ) : (
            <div className="space-y-2">
              {cust.identities.map((i, idx) => (
                <div key={idx} className="rounded-lg border border-stroke px-3 py-2.5 dark:border-dark-3">
                  <div className="flex items-center justify-between">
                    <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${i.type === "Email" || i.type === "1" ? "bg-blue-100 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400" : i.type === "Phone" || i.type === "2" ? "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400" : "bg-gray-100 text-gray-600 dark:bg-dark-3 dark:text-dark-6"}`}>
                      {i.type === "1" ? "Email" : i.type === "2" ? "Phone" : i.type === "3" ? "Device" : i.type === "4" ? "Provider" : i.type}
                    </span>
                    <span className="text-[10px] text-body-color dark:text-dark-6">{fmtDate(i.lastSeenAtUtc)}</span>
                  </div>
                  <div className="mt-1 flex items-center justify-between">
                    <span className="font-mono text-xs text-dark dark:text-white">{i.valueHash.slice(0, 20)}…</span>
                    <span className="text-[10px] text-body-color dark:text-dark-6">{i.sourceProvider || ""}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* RFM & AI */}
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-4 text-base font-semibold text-dark dark:text-white">🧠 AI & RFM</h3>
          <div className="grid gap-3 sm:grid-cols-2">
            {rfm && (
              <>
                <div className="rounded-lg bg-gray-1/50 p-3 dark:bg-dark-2/30">
                  <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Recency (R)</div>
                  <div className="mt-1 text-lg font-bold text-dark dark:text-white">{rfm.r ?? "-"}</div>
                </div>
                <div className="rounded-lg bg-gray-1/50 p-3 dark:bg-dark-2/30">
                  <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Frequency (F)</div>
                  <div className="mt-1 text-lg font-bold text-dark dark:text-white">{rfm.f ?? "-"}</div>
                </div>
                <div className="rounded-lg bg-gray-1/50 p-3 dark:bg-dark-2/30">
                  <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Monetary (M)</div>
                  <div className="mt-1 text-lg font-bold text-dark dark:text-white">{rfm.m ?? "-"}</div>
                </div>
              </>
            )}
            {ai?.nextPurchaseAtUtc && (
              <div className="rounded-lg bg-yellow-50/50 p-3 dark:bg-yellow-900/5">
                <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Tahmini Sonraki Alım</div>
                <div className="mt-1 text-sm font-semibold text-dark dark:text-white">{fmtDate(ai.nextPurchaseAtUtc)}</div>
              </div>
            )}
            {ai?.discountSensitivityScore != null && (
              <div className="rounded-lg bg-purple-50/50 p-3 dark:bg-purple-900/5">
                <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">İndirim Hassasiyeti</div>
                <div className="mt-1 text-lg font-bold text-dark dark:text-white">{Math.round(ai.discountSensitivityScore * 100)}%</div>
              </div>
            )}
          </div>
          {!rfm && !ai && <div className="py-6 text-center text-sm text-body-color dark:text-dark-6">Henüz AI/RFM verisi yok</div>}
        </div>
      </div>

      {/* Orders */}
      <div className="mt-6 rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-base font-semibold text-dark dark:text-white">📦 Siparişler</h3>
          <span className="rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-semibold text-primary">{orders.length}</span>
        </div>
        {orders.length === 0 ? (
          <div className="py-8 text-center text-sm text-body-color dark:text-dark-6">Sipariş bulunamadı</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full table-auto">
              <thead>
                <tr className="border-b border-stroke dark:border-dark-3">
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Sipariş</th>
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Kanal</th>
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Tarih</th>
                  <th className="px-3 py-3 text-right text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Tutar</th>
                  <th className="px-3 py-3 text-right text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Kâr</th>
                  <th className="px-3 py-3 text-center text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Satır</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-stroke dark:divide-dark-3">
                {orders.map((o) => (
                  <tr key={o.orderId} className="text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2">
                    <td className="px-3 py-2.5">
                      <a className="font-medium text-primary hover:underline" href={`/orders/${o.orderId}`}>{o.providerOrderId || o.orderId.slice(0, 8)}</a>
                    </td>
                    <td className="px-3 py-2.5">
                      <span className="inline-flex items-center gap-1 text-xs">{channelIcons[o.channel] || "📍"} {o.channel}</span>
                    </td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-xs text-body-color dark:text-dark-6">{fmtDate(o.placedAtUtc)}</td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-right text-sm font-medium text-dark dark:text-white">{fmtMoney(o.totalAmount, o.totalCurrency)}</td>
                    <td className={`whitespace-nowrap px-3 py-2.5 text-right text-sm font-medium ${o.netProfit > 0 ? "text-green-600" : o.netProfit < 0 ? "text-red-500" : "text-body-color dark:text-dark-6"}`}>{fmtMoney(o.netProfit, o.netProfitCurrency)}</td>
                    <td className="px-3 py-2.5 text-center text-xs text-body-color dark:text-dark-6">{o.lineCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
