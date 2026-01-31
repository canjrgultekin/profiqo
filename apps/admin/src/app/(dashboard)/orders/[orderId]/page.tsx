// Path: apps/admin/src/app/orders/[orderId]/page.tsx
"use client";

import React, { useEffect, useState } from "react";
import { use } from "react";

type MoneyDto = { amount: number; currency: string };

type OrderLineDto = {
  sku: string;
  productName: string;
  quantity: number;
  unitPrice: MoneyDto;
  lineTotal: MoneyDto;
};

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
};

function pretty(obj: any) {
  try {
    return JSON.stringify(obj, null, 2);
  } catch {
    return String(obj);
  }
}

export default function OrderDetailPage({ params }: any) {
  // Next 16: params promise olabilir
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

      if (!res.ok) {
        setErr(payload?.message || JSON.stringify(payload));
        return;
      }

      setOrder(payload);
    };

    load();
    return () => {
      cancel = true;
    };
  }, [orderId]);

  if (err) {
    return (
      <div className="p-4 sm:p-6">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          <div className="text-sm text-red-600">{err}</div>
        </div>
      </div>
    );
  }

  if (!order) {
    return (
      <div className="p-4 sm:p-6">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          <div className="text-sm text-body-color dark:text-dark-6">Loading...</div>
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6">
      <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h2 className="mb-4 text-lg font-semibold text-dark dark:text-white">
          Order: {order.providerOrderId || order.orderId}
        </h2>

        <div className="grid gap-3 md:grid-cols-2 text-sm">
          <div className="rounded-lg border border-stroke p-3 dark:border-dark-3">
            <div className="text-xs text-body-color dark:text-dark-6">Meta</div>
            <div className="mt-2">
              <div>Channel: {order.channel}</div>
              <div>Status: {order.status}</div>
              <div>Placed: {new Date(order.placedAtUtc).toLocaleString()}</div>
              <div>
                Total: {order.totalAmount.amount} {order.totalAmount.currency}
              </div>
            </div>
          </div>

          <div className="rounded-lg border border-stroke p-3 dark:border-dark-3">
            <div className="text-xs text-body-color dark:text-dark-6">Shipping Address</div>
            <pre className="mt-2 whitespace-pre-wrap text-xs">{order.shippingAddress ? pretty(order.shippingAddress) : "-"}</pre>
          </div>

          <div className="rounded-lg border border-stroke p-3 dark:border-dark-3 md:col-span-2">
            <div className="text-xs text-body-color dark:text-dark-6">Billing Address</div>
            <pre className="mt-2 whitespace-pre-wrap text-xs">{order.billingAddress ? pretty(order.billingAddress) : "-"}</pre>
          </div>
        </div>

        <div className="mt-4 rounded-lg border border-stroke p-3 dark:border-dark-3">
          <div className="mb-2 text-xs text-body-color dark:text-dark-6">Lines</div>

          <div className="overflow-x-auto">
            <table className="w-full table-auto">
              <thead>
                <tr className="text-left text-xs text-body-color dark:text-dark-6">
                  <th className="px-2 py-2">SKU</th>
                  <th className="px-2 py-2">Name</th>
                  <th className="px-2 py-2">Qty</th>
                  <th className="px-2 py-2">Unit</th>
                  <th className="px-2 py-2">Total</th>
                </tr>
              </thead>
              <tbody>
                {(order.lines || []).map((l, idx) => (
                  <tr key={idx} className="border-t border-stroke dark:border-dark-3 text-xs">
                    <td className="px-2 py-2">{l.sku}</td>
                    <td className="px-2 py-2">{l.productName}</td>
                    <td className="px-2 py-2">{l.quantity}</td>
                    <td className="px-2 py-2">
                      {l.unitPrice.amount} {l.unitPrice.currency}
                    </td>
                    <td className="px-2 py-2">
                      {l.lineTotal.amount} {l.lineTotal.currency}
                    </td>
                  </tr>
                ))}

                {!order.lines?.length && (
                  <tr className="border-t border-stroke dark:border-dark-3 text-xs">
                    <td className="px-2 py-2" colSpan={5}>
                      No lines.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
