"use client";

import React, { useEffect, useMemo, useState } from "react";

type OrderRow = {
  orderId: string;
  customerId: string;
  channel: string;
  status: string;
  providerOrderId: string | null;
  placedAtUtc: string;
  completedAtUtc: string | null;

  totalAmount: number;
  totalCurrency: string;

  netProfit: number;
  netProfitCurrency: string;

  lineCount: number;
};

type OrdersResponse = {
  page: number;
  pageSize: number;
  total: number;
  items: OrderRow[];
};

export default function OrdersPage() {
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [data, setData] = useState<OrdersResponse | null>(null);

  const totalPages = useMemo(() => {
    if (!data) return 1;
    return Math.max(1, Math.ceil(data.total / data.pageSize));
  }, [data]);

  const load = async () => {
    setLoading(true);
    setErr(null);

    const qs = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    });

    if (q.trim()) qs.set("q", q.trim());

    try {
      const res = await fetch(`/api/orders?${qs.toString()}`, {
        method: "GET",
        cache: "no-store",
      });

      const payload = (await res.json().catch(() => null)) as OrdersResponse | any;

      if (!res.ok) {
        setErr(payload?.message || "Orders load failed.");
        setData(null);
        return;
      }

      setData(payload);
    } catch {
      setErr("Orders load failed (network).");
      setData(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div className="flex flex-col">
          <label className="text-sm text-body-color dark:text-dark-6">Search</label>
          <input
            className="w-[320px] rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Provider order id"
          />
        </div>

        <button
          onClick={() => {
            setPage(1);
            load();
          }}
          className="rounded-lg bg-primary px-4 py-2 font-medium text-white hover:bg-opacity-90 disabled:opacity-60"
          disabled={loading}
        >
          Search
        </button>
      </div>

      {err && (
        <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-600 dark:text-red-400">
          {err}
        </div>
      )}

      <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-dark dark:text-white">Orders</h2>
          <div className="text-sm text-body-color dark:text-dark-6">
            {data ? `${data.total} total` : ""}
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full table-auto">
            <thead>
              <tr className="text-left text-sm text-body-color dark:text-dark-6">
                <th className="px-3 py-2">Order</th>
                <th className="px-3 py-2">Channel</th>
                <th className="px-3 py-2">Status</th>
                <th className="px-3 py-2">Total</th>
                <th className="px-3 py-2">Profit</th>
                <th className="px-3 py-2">Placed</th>
              </tr>
            </thead>
            <tbody>
              {loading && (
                <tr>
                  <td className="px-3 py-3 text-sm" colSpan={6}>
                    Loading...
                  </td>
                </tr>
              )}

              {!loading && data?.items?.length ? (
                data.items.map((x) => (
                  <tr key={x.orderId} className="border-t border-stroke dark:border-dark-3">
                    <td className="px-3 py-3 text-sm text-dark dark:text-white">
                      <div className="font-medium">{x.providerOrderId || x.orderId}</div>
                      <div className="text-xs text-body-color dark:text-dark-6">{x.customerId}</div>
                    </td>
                    <td className="px-3 py-3 text-sm">{x.channel}</td>
                    <td className="px-3 py-3 text-sm">{x.status}</td>
                    <td className="px-3 py-3 text-sm">
                      {x.totalAmount} {x.totalCurrency}
                    </td>
                    <td className="px-3 py-3 text-sm">
                      {x.netProfit} {x.netProfitCurrency}
                    </td>
                    <td className="px-3 py-3 text-sm">
                      {new Date(x.placedAtUtc).toLocaleString()}
                    </td>
                  </tr>
                ))
              ) : (
                !loading && (
                  <tr>
                    <td className="px-3 py-3 text-sm" colSpan={6}>
                      No orders.
                    </td>
                  </tr>
                )
              )}
            </tbody>
          </table>
        </div>

        <div className="mt-4 flex items-center justify-between">
          <button
            className="rounded-lg border border-stroke px-3 py-2 text-sm dark:border-dark-3"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={loading || page <= 1}
          >
            Prev
          </button>

          <div className="text-sm text-body-color dark:text-dark-6">
            Page {page} / {totalPages}
          </div>

          <button
            className="rounded-lg border border-stroke px-3 py-2 text-sm dark:border-dark-3"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={loading || page >= totalPages}
          >
            Next
          </button>
        </div>
      </div>
    </div>
  );
}
