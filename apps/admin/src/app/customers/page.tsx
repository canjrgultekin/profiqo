"use client";

import React, { useEffect, useMemo, useState } from "react";

type CustomerRow = {
  customerId: string;
  firstName: string | null;
  lastName: string | null;
  firstSeenAtUtc: string;
  lastSeenAtUtc: string;
  rfmSegment: string | null;
  churnRisk: number | null;
  ltv12mProfit: number | null;
};

type CustomersResponse = {
  page: number;
  pageSize: number;
  total: number;
  items: CustomerRow[];
};

export default function CustomersPage() {
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [data, setData] = useState<CustomersResponse | null>(null);

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
      const res = await fetch(`/api/customers?${qs.toString()}`, { method: "GET", cache: "no-store" });
      const payload = (await res.json().catch(() => null)) as any;

      if (!res.ok) {
        setErr(payload?.message || "Customers load failed.");
        setData(null);
        return;
      }

      setData(payload);
    } catch {
      setErr("Customers load failed (network).");
      setData(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div className="flex flex-col">
          <label className="text-sm text-body-color dark:text-dark-6">Search</label>
          <input
            className="w-[320px] rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="First name / last name"
          />
        </div>

        <button
          onClick={() => { setPage(1); load(); }}
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
          <h2 className="text-lg font-semibold text-dark dark:text-white">Customers</h2>
          <div className="text-sm text-body-color dark:text-dark-6">{data ? `${data.total} total` : ""}</div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full table-auto">
            <thead>
              <tr className="text-left text-sm text-body-color dark:text-dark-6">
                <th className="px-3 py-2">Name</th>
                <th className="px-3 py-2">RFM</th>
                <th className="px-3 py-2">Churn</th>
                <th className="px-3 py-2">LTV (12m)</th>
                <th className="px-3 py-2">Last Seen</th>
              </tr>
            </thead>
            <tbody>
              {loading && (
                <tr><td className="px-3 py-3 text-sm" colSpan={5}>Loading...</td></tr>
              )}

              {!loading && data?.items?.length ? (
                data.items.map((x) => (
                  <tr key={x.customerId} className="border-t border-stroke dark:border-dark-3">
                    <td className="px-3 py-3 text-sm text-dark dark:text-white">
                      <a className="underline" href={`/customers/${x.customerId}`}>
                        {`${x.firstName || ""} ${x.lastName || ""}`.trim() || x.customerId}
                      </a>
                    </td>
                    <td className="px-3 py-3 text-sm">{x.rfmSegment || "-"}</td>
                    <td className="px-3 py-3 text-sm">{x.churnRisk ?? "-"}</td>
                    <td className="px-3 py-3 text-sm">{x.ltv12mProfit ?? "-"}</td>
                    <td className="px-3 py-3 text-sm">{new Date(x.lastSeenAtUtc).toLocaleString()}</td>
                  </tr>
                ))
              ) : (
                !loading && (
                  <tr><td className="px-3 py-3 text-sm" colSpan={5}>No customers.</td></tr>
                )
              )}
            </tbody>
          </table>
        </div>

        <div className="mt-4 flex items-center justify-between">
          <button className="rounded-lg border border-stroke px-3 py-2 text-sm dark:border-dark-3"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={loading || page <= 1}>
            Prev
          </button>

          <div className="text-sm text-body-color dark:text-dark-6">
            Page {page} / {totalPages}
          </div>

          <button className="rounded-lg border border-stroke px-3 py-2 text-sm dark:border-dark-3"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={loading || page >= totalPages}>
            Next
          </button>
        </div>
      </div>
    </div>
  );
}
