"use client";

import React, { useEffect, useMemo, useState } from "react";
import Link from "next/link";

type MoneyDto = { amount: number; currency: string };
type MiniAddr = {
  city?: string | null;
  district?: string | null;
  postalCode?: string | null;
  country?: string | null;
};

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

type OrdersResponse = {
  page: number;
  pageSize: number;
  total: number;
  items: OrderRow[];
};

// ─── Helpers ──────────────────────────────────────────────────────────────────
function statusBadge(status: string): { label: string; className: string } {
  const s = (status || "").toLowerCase();
  if (s === "paid" || s === "fulfilled")
    return {
      label: status,
      className:
        "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3",
    };
  if (s === "pending")
    return {
      label: status,
      className:
        "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light",
    };
  if (s === "cancelled" || s === "refunded")
    return {
      label: status,
      className:
        "bg-red-light-6 text-red-dark dark:bg-red/10 dark:text-red-light",
    };
  return {
    label: status,
    className: "bg-gray-200 text-dark-4 dark:bg-dark-3 dark:text-dark-6",
  };
}

function channelBadge(ch: string): { icon: string; className: string } {
  const c = (ch || "").toLowerCase();
  if (c === "ikas")
    return { icon: "🛒", className: "bg-primary/10 text-primary" };
  if (c === "trendyol")
    return { icon: "🟠", className: "bg-orange-light/10 text-orange-light" };
  if (c === "shopify")
    return { icon: "🟢", className: "bg-green/10 text-green" };
  return { icon: "📦", className: "bg-gray-200 text-dark-4 dark:bg-dark-3 dark:text-dark-6" };
}

function fmtMoney(m: MoneyDto | null | undefined): string {
  if (!m) return "-";
  return `${new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(m.amount)} ${m.currency}`;
}

function fmtDate(d: string): string {
  if (!d) return "-";
  return new Date(d).toLocaleDateString("tr-TR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

// ─── Component ────────────────────────────────────────────────────────────────
export default function OrdersPage() {
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const [loading, setLoading] = useState(true);
  const [data, setData] = useState<OrdersResponse | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const totalPages = useMemo(() => {
    if (!data) return 1;
    return Math.max(1, Math.ceil(data.total / pageSize));
  }, [data]);

  const load = async () => {
    setLoading(true);
    setErr(null);

    const res = await fetch(
      `/api/orders?page=${page}&pageSize=${pageSize}`,
      { cache: "no-store" }
    );
    const payload = await res.json().catch(() => null);

    if (!res.ok) {
      setErr(payload?.message || JSON.stringify(payload));
      setData(null);
      setLoading(false);
      return;
    }

    setData(payload);
    setLoading(false);
  };

  useEffect(() => {
    load();
  }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  const items = data?.items ?? [];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-dark dark:text-white">
          Siparişler
        </h1>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
          Tüm kanallardan gelen sipariş kayıtları
        </p>
      </div>

      {err && (
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <svg
            className="h-5 w-5 shrink-0 text-red-600"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
          <p className="text-sm text-red-600 dark:text-red-300">{err}</p>
        </div>
      )}

      {/* Table */}
      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex items-center justify-between border-b border-stroke px-5 py-4 dark:border-dark-3">
          <h2 className="text-base font-semibold text-dark dark:text-white">
            Sipariş Listesi
          </h2>
          <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">
            {data ? `${data.total} sipariş` : "..."}
          </span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-stroke text-left text-xs font-medium uppercase tracking-wider text-dark-5 dark:border-dark-3 dark:text-dark-6">
                <th className="w-8 px-2 py-3" />
                <th className="px-4 py-3">Sipariş No</th>
                <th className="px-4 py-3">Kanal</th>
                <th className="px-4 py-3">Durum</th>
                <th className="px-4 py-3">Tarih</th>
                <th className="px-4 py-3 text-right">Tutar</th>
                <th className="px-4 py-3">Teslimat</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-stroke dark:divide-dark-3">
              {loading && (
                <tr>
                  <td className="px-5 py-8 text-center text-sm" colSpan={7}>
                    <div className="flex items-center justify-center gap-2 text-dark-5">
                      <div className="h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" />
                      Yükleniyor...
                    </div>
                  </td>
                </tr>
              )}

              {!loading &&
                items.map((o) => {
                  const sBadge = statusBadge(o.status);
                  const cBadge = channelBadge(o.channel);
                  const isExpanded = expandedId === o.orderId;

                  return (
                    <React.Fragment key={o.orderId}>
                      <tr className="text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2">
                        <td className="px-2 py-3">
                          <button
                            onClick={() =>
                              setExpandedId(isExpanded ? null : o.orderId)
                            }
                            className="flex h-6 w-6 items-center justify-center rounded transition-colors hover:bg-gray-200 dark:hover:bg-dark-3"
                          >
                            <svg
                              className={`h-3.5 w-3.5 text-dark-5 transition-transform ${isExpanded ? "rotate-90" : ""}`}
                              fill="none"
                              viewBox="0 0 24 24"
                              stroke="currentColor"
                              strokeWidth={2}
                            >
                              <path
                                strokeLinecap="round"
                                strokeLinejoin="round"
                                d="M8.25 4.5l7.5 7.5-7.5 7.5"
                              />
                            </svg>
                          </button>
                        </td>
                        <td className="px-4 py-3.5">
                          <Link
                            className="font-medium text-primary hover:underline"
                            href={`/orders/${o.orderId}`}
                          >
                            {o.providerOrderId || o.orderId.slice(0, 8)}
                          </Link>
                        </td>
                        <td className="px-4 py-3.5">
                          <span
                            className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium ${cBadge.className}`}
                          >
                            <span>{cBadge.icon}</span>
                            {o.channel}
                          </span>
                        </td>
                        <td className="px-4 py-3.5">
                          <span
                            className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${sBadge.className}`}
                          >
                            {sBadge.label}
                          </span>
                        </td>
                        <td className="px-4 py-3.5 text-dark-5 dark:text-dark-6">
                          {fmtDate(o.placedAtUtc)}
                        </td>
                        <td className="px-4 py-3.5 text-right font-semibold text-dark dark:text-white">
                          {fmtMoney(o.totalAmount)}
                        </td>
                        <td className="px-4 py-3.5 text-dark-5 dark:text-dark-6">
                          {o.shipping?.city || "-"}
                          {o.shipping?.district
                            ? `, ${o.shipping.district}`
                            : ""}
                        </td>
                      </tr>
                      {isExpanded && (
                        <tr className="bg-gray-1 dark:bg-dark-2">
                          <td colSpan={7} className="px-5 py-4">
                            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                              <div>
                                <p className="text-xs font-medium uppercase text-dark-5 dark:text-dark-6">
                                  Sipariş ID
                                </p>
                                <p className="mt-1 font-mono text-xs text-dark dark:text-white">
                                  {o.orderId}
                                </p>
                              </div>
                              <div>
                                <p className="text-xs font-medium uppercase text-dark-5 dark:text-dark-6">
                                  Provider ID
                                </p>
                                <p className="mt-1 font-mono text-xs text-dark dark:text-white">
                                  {o.providerOrderId || "-"}
                                </p>
                              </div>
                              <div>
                                <p className="text-xs font-medium uppercase text-dark-5 dark:text-dark-6">
                                  Teslimat Adresi
                                </p>
                                <p className="mt-1 text-xs text-dark dark:text-white">
                                  {[
                                    o.shipping?.district,
                                    o.shipping?.city,
                                    o.shipping?.postalCode,
                                    o.shipping?.country,
                                  ]
                                    .filter(Boolean)
                                    .join(", ") || "-"}
                                </p>
                              </div>
                              <div>
                                <p className="text-xs font-medium uppercase text-dark-5 dark:text-dark-6">
                                  Fatura Adresi
                                </p>
                                <p className="mt-1 text-xs text-dark dark:text-white">
                                  {[
                                    o.billing?.district,
                                    o.billing?.city,
                                    o.billing?.postalCode,
                                    o.billing?.country,
                                  ]
                                    .filter(Boolean)
                                    .join(", ") || "-"}
                                </p>
                              </div>
                            </div>
                            <div className="mt-3">
                              <Link
                                href={`/orders/${o.orderId}`}
                                className="text-xs font-medium text-primary hover:underline"
                              >
                                Detaya Git →
                              </Link>
                            </div>
                          </td>
                        </tr>
                      )}
                    </React.Fragment>
                  );
                })}

              {!loading && !items.length && (
                <tr>
                  <td
                    className="px-5 py-12 text-center text-sm text-dark-5 dark:text-dark-6"
                    colSpan={7}
                  >
                    <div className="flex flex-col items-center gap-2">
                      <svg
                        className="h-10 w-10 text-dark-6"
                        fill="none"
                        viewBox="0 0 24 24"
                        stroke="currentColor"
                        strokeWidth={1}
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          d="M15.75 10.5V6a3.75 3.75 0 10-7.5 0v4.5m11.356-1.993l1.263 12c.07.665-.45 1.243-1.119 1.243H4.25a1.125 1.125 0 01-1.12-1.243l1.264-12A1.125 1.125 0 015.513 7.5h12.974c.576 0 1.059.435 1.119 1.007zM8.625 10.5a.375.375 0 11-.75 0 .375.375 0 01.75 0zm7.5 0a.375.375 0 11-.75 0 .375.375 0 01.75 0z"
                        />
                      </svg>
                      <p>Henüz sipariş yok.</p>
                    </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="flex items-center justify-between border-t border-stroke px-5 py-4 dark:border-dark-3">
          <button
            className="inline-flex items-center gap-1.5 rounded-lg border border-stroke px-3.5 py-2 text-sm font-medium text-dark transition-colors hover:bg-gray-1 disabled:cursor-not-allowed disabled:opacity-50 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={loading || page <= 1}
          >
            <svg
              className="h-4 w-4"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={2}
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M15.75 19.5L8.25 12l7.5-7.5"
              />
            </svg>
            Önceki
          </button>

          <div className="flex items-center gap-2 text-sm">
            <span className="text-dark-5 dark:text-dark-6">Sayfa</span>
            <span className="rounded-md bg-primary/10 px-2.5 py-0.5 font-semibold text-primary">
              {page}
            </span>
            <span className="text-dark-5 dark:text-dark-6">/ {totalPages}</span>
          </div>

          <button
            className="inline-flex items-center gap-1.5 rounded-lg border border-stroke px-3.5 py-2 text-sm font-medium text-dark transition-colors hover:bg-gray-1 disabled:cursor-not-allowed disabled:opacity-50 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={loading || page >= totalPages}
          >
            Sonraki
            <svg
              className="h-4 w-4"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={2}
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M8.25 4.5l7.5 7.5-7.5 7.5"
              />
            </svg>
          </button>
        </div>
      </div>
    </div>
  );
}
