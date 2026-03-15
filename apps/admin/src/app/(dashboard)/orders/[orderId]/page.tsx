// Path: apps/admin/src/app/(dashboard)/orders/[orderId]/page.tsx
"use client";

import React, { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";

// ── Types ─────────────────────────────────────────────────────────────────────

type MoneyDto = { amount: number; currency: string };
type OrderLineDto = { sku: string; productName: string; quantity: number; unitPrice: MoneyDto; lineTotal: MoneyDto };
type OrderDto = {
  orderId: string;
  providerOrderId?: string | null;
  channel: string;
  status: string;
  placedAtUtc: string;
  totalAmount: MoneyDto;
  shippingAddress?: any | null;
  billingAddress?: any | null;
  lines: OrderLineDto[];
  customerId?: string;
};

// ── Helpers ───────────────────────────────────────────────────────────────────

const STATUS_MAP: Record<string, { label: string; cls: string }> = {
  Pending:   { label: "Beklemede",  cls: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" },
  Paid:      { label: "Ödendi",     cls: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" },
  Fulfilled: { label: "Tamamlandı", cls: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" },
  Cancelled: { label: "İptal",      cls: "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" },
  Refunded:  { label: "İade",       cls: "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" },
};

const CHANNEL_ICONS: Record<string, string> = { Ikas: "🛒", Trendyol: "🟠", Shopify: "🟢", Instagram: "📸" };

function fmtDate(s: string) {
  try { return new Date(s).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" }); }
  catch { return s; }
}
function fmtMoney(m: MoneyDto) {
  return `${m.amount.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${m.currency}`;
}

function normalizeAddress(addr: any): { fullName?: string; addressLine1?: string; addressLine2?: string; district?: string; city?: string; postalCode?: string; country?: string; phone?: string } | null {
  if (!addr) return null;
  const extractField = (field: any): string | undefined => {
    if (!field) return undefined;
    if (typeof field === "string") return field;
    if (typeof field === "object" && field.name) return field.name;
    return undefined;
  };
  let fullName = addr.fullName;
  if (!fullName && (addr.firstName || addr.lastName)) {
    fullName = [addr.firstName, addr.lastName].filter(Boolean).join(" ");
  }
  return {
    fullName,
    addressLine1: addr.addressLine1,
    addressLine2: addr.addressLine2,
    district: extractField(addr.district),
    city: extractField(addr.city),
    postalCode: addr.postalCode,
    country: extractField(addr.country),
    phone: addr.phone,
  };
}

function AddressCard({ title, addr }: { title: string; addr: any }) {
  const n = normalizeAddress(addr);

  if (!n) return (
    <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
      <h4 className="mb-3 text-sm font-semibold text-dark dark:text-white">{title}</h4>
      <p className="text-sm text-dark-5 dark:text-dark-6">Adres bilgisi mevcut değil.</p>
    </div>
  );

  const hasData = n.fullName || n.addressLine1 || n.district || n.city || n.country;

  if (!hasData) return (
    <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
      <h4 className="mb-3 text-sm font-semibold text-dark dark:text-white">{title}</h4>
      <pre className="whitespace-pre-wrap text-xs text-dark-5 dark:text-dark-6">{JSON.stringify(addr, null, 2)}</pre>
    </div>
  );

  return (
    <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
      <h4 className="mb-3 text-sm font-semibold text-dark dark:text-white">{title}</h4>
      <div className="space-y-1 text-sm text-dark-5 dark:text-dark-6">
        {n.fullName && <p className="font-medium text-dark dark:text-white">{n.fullName}</p>}
        {n.phone && <p className="text-xs">Tel: {n.phone}</p>}
        {n.addressLine1 && <p>{n.addressLine1}</p>}
        {n.addressLine2 && <p>{n.addressLine2}</p>}
        {(n.district || n.city || n.postalCode) && (
          <p>{[n.district, n.city, n.postalCode].filter(Boolean).join(", ")}</p>
        )}
        {n.country && <p>{n.country}</p>}
      </div>
    </div>
  );
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function OrderDetailPage() {
  const params = useParams<{ orderId: string }>();
  const router = useRouter();
  const orderId = params?.orderId;

  const [order, setOrder] = useState<OrderDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!orderId) return;
    setErr(null);
    setLoading(true);
    fetch(`/api/orders/${orderId}`, { cache: "no-store" })
      .then(async (r) => {
        if (!r.ok) throw new Error((await r.json().catch(() => null))?.message || "Sipariş yüklenemedi.");
        return r.json();
      })
      .then((data) => setOrder(data))
      .catch((e) => setErr(e.message))
      .finally(() => setLoading(false));
  }, [orderId]);

  if (loading) {
    return (
      <div className="space-y-6">
        <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark">
          <div className="space-y-3">
            <div className="h-6 w-48 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
            <div className="h-4 w-32 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
          </div>
        </div>
      </div>
    );
  }

  if (err || !order) {
    return (
      <div className="space-y-4">
        <button onClick={() => router.push("/orders")} className="inline-flex items-center gap-1.5 text-sm text-primary hover:underline">
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M15.75 19.5L8.25 12l7.5-7.5" /></svg>
          Siparişlere Dön
        </button>
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <svg className="h-5 w-5 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-red-600 dark:text-red-300">{err || "Sipariş bulunamadı."}</p>
        </div>
      </div>
    );
  }

  const sc = STATUS_MAP[order.status] || { label: order.status, cls: "bg-gray-3 text-dark-4 dark:bg-dark-3 dark:text-dark-6" };

  return (
    <div className="space-y-6">
      {/* Back */}
      <button onClick={() => router.push("/orders")} className="inline-flex items-center gap-1.5 text-sm text-primary hover:underline">
        <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M15.75 19.5L8.25 12l7.5-7.5" /></svg>
        Siparişlere Dön
      </button>

      {/* Header */}
      <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-xl font-bold text-dark dark:text-white">
              Sipariş: {order.providerOrderId || order.orderId.slice(0, 8)}
            </h1>
            <div className="mt-2 flex flex-wrap items-center gap-3 text-sm text-dark-5 dark:text-dark-6">
              <span className="inline-flex items-center gap-1">{CHANNEL_ICONS[order.channel] || "📍"} {order.channel}</span>
              <span className="text-dark-6/40">|</span>
              <span>{fmtDate(order.placedAtUtc)}</span>
              {order.customerId && (
                <>
                  <span className="text-dark-6/40">|</span>
                  <button onClick={() => router.push(`/customers/${order.customerId}`)} className="text-primary hover:underline">
                    Müşteriyi Görüntüle
                  </button>
                </>
              )}
            </div>
          </div>
          <div className="flex items-center gap-3">
            <span className={`rounded-full px-3 py-1 text-xs font-semibold ${sc.cls}`}>{sc.label}</span>
            <div className="text-right">
              <div className="text-xs text-dark-5 dark:text-dark-6">Toplam</div>
              <div className="text-lg font-bold text-dark dark:text-white">{fmtMoney(order.totalAmount)}</div>
            </div>
          </div>
        </div>
      </div>

      {/* Addresses */}
      <div className="grid gap-6 md:grid-cols-2">
        <AddressCard title="Teslimat Adresi" addr={order.shippingAddress} />
        <AddressCard title="Fatura Adresi" addr={order.billingAddress} />
      </div>

      {/* Lines */}
      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex items-center justify-between border-b border-stroke px-5 py-4 dark:border-dark-3">
          <h3 className="text-base font-semibold text-dark dark:text-white">Sipariş Kalemleri</h3>
          <span className="rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-semibold text-primary">{order.lines?.length || 0} kalem</span>
        </div>

        {!order.lines?.length ? (
          <div className="py-12 text-center text-sm text-dark-5 dark:text-dark-6">Sipariş kalemi bulunamadı.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-stroke text-left text-xs font-medium uppercase tracking-wider text-dark-5 dark:border-dark-3 dark:text-dark-6">
                  <th className="px-4 py-3">SKU</th>
                  <th className="px-4 py-3">Ürün</th>
                  <th className="px-4 py-3 text-center">Adet</th>
                  <th className="px-4 py-3 text-right">Birim Fiyat</th>
                  <th className="px-4 py-3 text-right">Toplam</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-stroke dark:divide-dark-3">
                {order.lines.map((l, idx) => (
                  <tr key={idx} className="text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2">
                    <td className="px-4 py-3 font-mono text-xs text-dark-5 dark:text-dark-6">{l.sku}</td>
                    <td className="px-4 py-3 font-medium text-dark dark:text-white">{l.productName}</td>
                    <td className="px-4 py-3 text-center text-dark dark:text-white">{l.quantity}</td>
                    <td className="whitespace-nowrap px-4 py-3 text-right text-dark-5 dark:text-dark-6">{fmtMoney(l.unitPrice)}</td>
                    <td className="whitespace-nowrap px-4 py-3 text-right font-semibold text-dark dark:text-white">{fmtMoney(l.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                <tr className="border-t-2 border-stroke dark:border-dark-3">
                  <td colSpan={4} className="px-4 py-3 text-right text-sm font-semibold text-dark dark:text-white">Genel Toplam</td>
                  <td className="px-4 py-3 text-right text-base font-bold text-primary">{fmtMoney(order.totalAmount)}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}