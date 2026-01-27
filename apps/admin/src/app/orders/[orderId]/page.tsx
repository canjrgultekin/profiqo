"use client";

import React, { useEffect, useState } from "react";
import { useParams } from "next/navigation";

type OrderLineDto = {
  sku: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  unitCurrency: string;
  lineTotal: number;
  lineTotalCurrency: string;
};

type OrderDto = {
  orderId: string;
  customerId: string;
  providerOrderId: string | null;
  channel: string;
  status: string;
  placedAtUtc: string;
  completedAtUtc: string | null;
  totalAmount: number;
  totalCurrency: string;
  netProfit: number;
  netProfitCurrency: string;
  costBreakdownJson: string;
  lines: OrderLineDto[];
};

export default function OrderDetailPage() {
  const params = useParams<{ orderId: string }>();
  const orderId = params?.orderId;

  const [order, setOrder] = useState<OrderDto | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const load = async (id: string) => {
    setErr(null);

    const res = await fetch(`/api/orders/${id}`, { cache: "no-store" });
    const payload = await res.json().catch(() => null);

    if (!res.ok) {
      setErr(payload?.message || "Failed to load order.");
      return;
    }

    setOrder(payload);
  };

  useEffect(() => {
    if (!orderId) return;
    load(orderId);
  }, [orderId]);

  if (!orderId) {
    return (
      <div className="p-6">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          Loading...
        </div>
      </div>
    );
  }

  if (err) {
    return (
      <div className="p-6">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card text-red-600">
          {err}
        </div>
      </div>
    );
  }

  if (!order) {
    return (
      <div className="p-6">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          Loading...
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4 rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h2 className="text-lg font-semibold text-dark dark:text-white">Order Detail</h2>
        <div className="mt-2 text-sm text-body-color dark:text-dark-6">
          <div><b>Order:</b> {order.providerOrderId || order.orderId}</div>
          <div><b>Channel:</b> {order.channel}</div>
          <div><b>Status:</b> {order.status}</div>
          <div><b>Placed:</b> {new Date(order.placedAtUtc).toLocaleString()}</div>
          <div><b>Total:</b> {order.totalAmount} {order.totalCurrency}</div>
          <div><b>Customer:</b> <a className="underline" href={`/customers/${order.customerId}`}>{order.customerId}</a></div>
        </div>
      </div>

      <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h3 className="text-md font-semibold text-dark dark:text-white">Order Lines</h3>
        <div className="mt-3 overflow-x-auto">
          <table className="w-full table-auto">
            <thead>
              <tr className="text-left text-xs text-body-color dark:text-dark-6">
                <th className="px-2 py-2">SKU</th>
                <th className="px-2 py-2">Product</th>
                <th className="px-2 py-2">Qty</th>
                <th className="px-2 py-2">Unit</th>
                <th className="px-2 py-2">Line Total</th>
              </tr>
            </thead>
            <tbody>
              {order.lines.map((l, idx) => (
                <tr key={idx} className="border-t border-stroke dark:border-dark-3 text-xs">
                  <td className="px-2 py-2">{l.sku}</td>
                  <td className="px-2 py-2">{l.productName}</td>
                  <td className="px-2 py-2">{l.quantity}</td>
                  <td className="px-2 py-2">{l.unitPrice} {l.unitCurrency}</td>
                  <td className="px-2 py-2">{l.lineTotal} {l.lineTotalCurrency}</td>
                </tr>
              ))}
              {!order.lines.length && (
                <tr className="border-t border-stroke dark:border-dark-3 text-xs">
                  <td className="px-2 py-2" colSpan={5}>No lines</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
