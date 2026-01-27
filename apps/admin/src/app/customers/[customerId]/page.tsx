"use client";

import React, { useEffect, useState } from "react";
import { useParams } from "next/navigation";

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

export default function CustomerDetailPage() {
  const params = useParams<{ customerId: string }>();
  const customerId = params?.customerId;

  const [cust, setCust] = useState<CustomerDto | null>(null);
  const [orders, setOrders] = useState<OrderRow[]>([]);
  const [err, setErr] = useState<string | null>(null);

  const load = async (id: string) => {
    setErr(null);

    const cRes = await fetch(`/api/customers/${id}`, { cache: "no-store" });
    const cPayload = await cRes.json().catch(() => null);

    if (!cRes.ok) {
      setErr(cPayload?.message || "Failed to load customer.");
      return;
    }

    setCust(cPayload);

    const oRes = await fetch(`/api/customers/${id}/orders`, { cache: "no-store" });
    const oPayload = await oRes.json().catch(() => null);

    if (!oRes.ok) {
      setErr(oPayload?.message || "Failed to load orders.");
      return;
    }

    setOrders(oPayload?.items || []);
  };

  useEffect(() => {
    if (!customerId) return;
    load(customerId);
  }, [customerId]);

  if (!customerId) {
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

  if (!cust) {
    return (
      <div className="p-6">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          Loading...
        </div>
      </div>
    );
  }

  const name = `${cust.firstName || ""} ${cust.lastName || ""}`.trim() || cust.customerId;

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4 rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h2 className="text-lg font-semibold text-dark dark:text-white">Customer 360</h2>
        <div className="mt-2 text-sm text-body-color dark:text-dark-6">
          <div><b>Name:</b> {name}</div>
          <div><b>CustomerId:</b> {cust.customerId}</div>
          <div><b>First Seen:</b> {new Date(cust.firstSeenAtUtc).toLocaleString()}</div>
          <div><b>Last Seen:</b> {new Date(cust.lastSeenAtUtc).toLocaleString()}</div>
        </div>
      </div>

      <div className="mb-4 rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h3 className="text-md font-semibold text-dark dark:text-white">Identities</h3>
        <div className="mt-3 overflow-x-auto">
          <table className="w-full table-auto">
            <thead>
              <tr className="text-left text-xs text-body-color dark:text-dark-6">
                <th className="px-2 py-2">Type</th>
                <th className="px-2 py-2">Hash</th>
                <th className="px-2 py-2">Source</th>
                <th className="px-2 py-2">ExternalId</th>
                <th className="px-2 py-2">Last Seen</th>
              </tr>
            </thead>
            <tbody>
              {cust.identities.map((i, idx) => (
                <tr key={idx} className="border-t border-stroke dark:border-dark-3 text-xs">
                  <td className="px-2 py-2">{i.type}</td>
                  <td className="px-2 py-2 font-mono">{i.valueHash.slice(0, 16)}…</td>
                  <td className="px-2 py-2">{i.sourceProvider || "-"}</td>
                  <td className="px-2 py-2">{i.sourceExternalId || "-"}</td>
                  <td className="px-2 py-2">{new Date(i.lastSeenAtUtc).toLocaleString()}</td>
                </tr>
              ))}
              {!cust.identities.length && (
                <tr className="border-t border-stroke dark:border-dark-3 text-xs">
                  <td className="px-2 py-2" colSpan={5}>No identities</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h3 className="text-md font-semibold text-dark dark:text-white">Orders</h3>
        <div className="mt-3 overflow-x-auto">
          <table className="w-full table-auto">
            <thead>
              <tr className="text-left text-xs text-body-color dark:text-dark-6">
                <th className="px-2 py-2">Order</th>
                <th className="px-2 py-2">Channel</th>
                <th className="px-2 py-2">Placed</th>
                <th className="px-2 py-2">Total</th>
                <th className="px-2 py-2">Lines</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((o) => (
                <tr key={o.orderId} className="border-t border-stroke dark:border-dark-3 text-xs">
                  <td className="px-2 py-2">
                    <a className="underline" href={`/orders/${o.orderId}`}>
                      {o.providerOrderId || o.orderId}
                    </a>
                  </td>
                  <td className="px-2 py-2">{o.channel}</td>
                  <td className="px-2 py-2">{new Date(o.placedAtUtc).toLocaleString()}</td>
                  <td className="px-2 py-2">{o.totalAmount} {o.totalCurrency}</td>
                  <td className="px-2 py-2">{o.lineCount}</td>
                </tr>
              ))}
              {!orders.length && (
                <tr className="border-t border-stroke dark:border-dark-3 text-xs">
                  <td className="px-2 py-2" colSpan={5}>No orders</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
