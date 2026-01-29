// Path: apps/admin/src/app/orders/page.tsx
"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";

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

export default function OrdersPage() {
  const [items, setItems] = useState<OrderRow[]>([]);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    let cancel = false;

    const load = async () => {
      setErr(null);
      const res = await fetch(`/api/orders?page=1&pageSize=50`, { cache: "no-store" });
      const payload = await res.json().catch(() => null);

      if (cancel) return;

      if (!res.ok) {
        setErr(payload?.message || JSON.stringify(payload));
        return;
      }

      setItems(payload?.items ?? []);
    };

    load();
    return () => {
      cancel = true;
    };
  }, []);

  return (
    <div className="p-4 sm:p-6">
      <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h2 className="mb-4 text-lg font-semibold text-dark dark:text-white">Orders</h2>

        {err && <div className="mb-3 rounded-lg border border-red-500 p-2 text-sm text-red-600">{err}</div>}

        <div className="overflow-x-auto">
          <table className="w-full table-auto">
            <thead>
              <tr className="text-left text-xs text-body-color dark:text-dark-6">
                <th className="px-2 py-2">Order</th>
                <th className="px-2 py-2">Channel</th>
                <th className="px-2 py-2">Status</th>
                <th className="px-2 py-2">Placed</th>
                <th className="px-2 py-2">Total</th>
                <th className="px-2 py-2">Shipping City</th>
                <th className="px-2 py-2">Shipping District</th>
                <th className="px-2 py-2">Postal</th>
              </tr>
            </thead>
            <tbody>
              {items.map((o) => (
                <tr key={o.orderId} className="border-t border-stroke dark:border-dark-3 text-xs">
                  <td className="px-2 py-2">
                    <Link className="text-primary underline" href={`/orders/${o.orderId}`}>
                      {o.providerOrderId || o.orderId}
                    </Link>
                  </td>
                  <td className="px-2 py-2">{o.channel}</td>
                  <td className="px-2 py-2">{o.status}</td>
                  <td className="px-2 py-2">{o.placedAtUtc ? new Date(o.placedAtUtc).toLocaleString() : "-"}</td>
                  <td className="px-2 py-2">
                    {o.totalAmount?.amount?.toFixed?.(2) ?? o.totalAmount?.amount} {o.totalAmount?.currency}
                  </td>
                  <td className="px-2 py-2">{o.shipping?.city ?? "-"}</td>
                  <td className="px-2 py-2">{o.shipping?.district ?? "-"}</td>
                  <td className="px-2 py-2">{o.shipping?.postalCode ?? "-"}</td>
                </tr>
              ))}
              {!items.length && (
                <tr className="border-t border-stroke dark:border-dark-3 text-xs">
                  <td className="px-2 py-3" colSpan={8}>
                    No orders yet.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
