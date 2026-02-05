"use client";

import React, { useEffect, useState } from "react";
import { use } from "react";

type MoneyDto = { amount: number; currency: string };
type OrderLineDto = { sku: string; productName: string; quantity: number; unitPrice: MoneyDto; lineTotal: MoneyDto };
type OrderDto = { orderId: string; providerOrderId?: string | null; channel: string; status: string; placedAtUtc: string; totalAmount: MoneyDto; shippingAddress?: any | null; billingAddress?: any | null; lines: OrderLineDto[]; customerId?: string };

const statusCfg: Record<string, { label: string; color: string }> = {
  Pending: { label: "Beklemede", color: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/20 dark:text-yellow-400" },
  Paid: { label: "Ödendi", color: "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400" },
  Fulfilled: { label: "Tamamlandı", color: "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400" },
  Cancelled: { label: "İptal", color: "bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400" },
  Refunded: { label: "İade", color: "bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400" },
};
const channelIcons: Record<string, string> = { Ikas: "🛒", Trendyol: "🟠", Shopify: "🟢", Instagram: "📸" };

function fmtDate(s: string) { try { return new Date(s).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }); } catch { return s; } }
function fmtMoney(m: MoneyDto) { return `${m.amount.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${m.currency}`; }

function AddressCard({ title, icon, addr }: { title: string; icon: string; addr: any }) {
  if (!addr) return (
    <div className="rounded-lg border border-stroke p-4 dark:border-dark-3">
      <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-dark dark:text-white">{icon} {title}</div>
      <div className="text-sm text-body-color dark:text-dark-6">Adres bilgisi yok</div>
    </div>
  );
  const parts = [addr.fullName, addr.addressLine1, addr.addressLine2, addr.district, addr.city, addr.postalCode, addr.country].filter(Boolean);
  return (
    <div className="rounded-lg border border-stroke p-4 dark:border-dark-3">
      <div className="mb-2 flex items-center gap-2 text-sm font-semibold text-dark dark:text-white">{icon} {title}</div>
      {parts.length > 0 ? (
        <div className="space-y-0.5 text-sm text-body-color dark:text-dark-6">
          {addr.fullName && <div className="font-medium text-dark dark:text-white">{addr.fullName}</div>}
          {addr.addressLine1 && <div>{addr.addressLine1}</div>}
          {addr.addressLine2 && <div>{addr.addressLine2}</div>}
          <div>{[addr.district, addr.city, addr.postalCode].filter(Boolean).join(", ")}</div>
          {addr.country && <div>{addr.country}</div>}
        </div>
      ) : (
        <pre className="whitespace-pre-wrap text-xs text-body-color dark:text-dark-6">{JSON.stringify(addr, null, 2)}</pre>
      )}
    </div>
  );
}

export default function OrderDetailPage({ params }: any) {
  const p = use(params);
  const orderId = p.orderId as string;
  const [order, setOrder] = useState<OrderDto | null>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    let cancel = false;
    const load = async () => {
      setErr(null);
      const res = await fetch(`/api/orders/${orderId}`, { cache: "no-store" });
      const payload = await res.json().catch(() => null);
      if (cancel) return;
      if (!res.ok) { setErr(payload?.message || JSON.stringify(payload)); return; }
      setOrder(payload);
    };
    load();
    return () => { cancel = true; };
  }, [orderId]);

  if (err) return (
    <div className="p-4 sm:p-6">
      <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 dark:border-red-900/30 dark:bg-red-900/10">
        <svg className="h-5 w-5 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
        <span className="text-sm text-red-600 dark:text-red-400">{err}</span>
      </div>
    </div>
  );

  if (!order) return (
    <div className="p-4 sm:p-6">
      <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark">
        <div className="space-y-3"><div className="h-6 w-48 animate-pulse rounded bg-gray-200 dark:bg-dark-3" /><div className="h-4 w-32 animate-pulse rounded bg-gray-200 dark:bg-dark-3" /></div>
      </div>
    </div>
  );

  const sc = statusCfg[order.status] || { label: order.status, color: "bg-gray-100 text-gray-600 dark:bg-dark-3 dark:text-dark-6" };

  return (
    <div className="p-4 sm:p-6">
      {/* Header */}
      <div className="mb-6 rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <a href="/orders" className="text-body-color hover:text-primary dark:text-dark-6">
                <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" /></svg>
              </a>
              <h2 className="text-xl font-bold text-dark dark:text-white">
                Sipariş: {order.providerOrderId || order.orderId.slice(0, 8)}
              </h2>
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-3 text-sm text-body-color dark:text-dark-6">
              <span className="inline-flex items-center gap-1">{channelIcons[order.channel] || "📍"} {order.channel}</span>
              <span className="text-body-color/30">|</span>
              <span>{fmtDate(order.placedAtUtc)}</span>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <span className={`rounded-full px-3 py-1 text-xs font-semibold ${sc.color}`}>{sc.label}</span>
            <div className="text-right">
              <div className="text-xs text-body-color dark:text-dark-6">Toplam</div>
              <div className="text-lg font-bold text-dark dark:text-white">{fmtMoney(order.totalAmount)}</div>
            </div>
          </div>
        </div>
      </div>

      {/* Addresses */}
      <div className="mb-6 grid gap-6 md:grid-cols-2">
        <AddressCard title="Teslimat Adresi" icon="📦" addr={order.shippingAddress} />
        <AddressCard title="Fatura Adresi" icon="🧾" addr={order.billingAddress} />
      </div>

      {/* Lines */}
      <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-base font-semibold text-dark dark:text-white">🛍️ Sipariş Kalemleri</h3>
          <span className="rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-semibold text-primary">{order.lines?.length || 0} kalem</span>
        </div>

        {!order.lines?.length ? (
          <div className="py-8 text-center text-sm text-body-color dark:text-dark-6">Sipariş kalemi yok</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full table-auto">
              <thead>
                <tr className="border-b border-stroke dark:border-dark-3">
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">SKU</th>
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Ürün</th>
                  <th className="px-3 py-3 text-center text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Adet</th>
                  <th className="px-3 py-3 text-right text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Birim</th>
                  <th className="px-3 py-3 text-right text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Toplam</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-stroke dark:divide-dark-3">
                {order.lines.map((l, idx) => (
                  <tr key={idx} className="text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2">
                    <td className="px-3 py-2.5 font-mono text-xs text-body-color dark:text-dark-6">{l.sku}</td>
                    <td className="px-3 py-2.5 font-medium text-dark dark:text-white">{l.productName}</td>
                    <td className="px-3 py-2.5 text-center text-dark dark:text-white">{l.quantity}</td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-right text-body-color dark:text-dark-6">{fmtMoney(l.unitPrice)}</td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-right font-semibold text-dark dark:text-white">{fmtMoney(l.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="border-t-2 border-stroke dark:border-dark-3">
                  <td colSpan={4} className="px-3 py-3 text-right text-sm font-semibold text-dark dark:text-white">Genel Toplam</td>
                  <td className="px-3 py-3 text-right text-base font-bold text-primary">{fmtMoney(order.totalAmount)}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
