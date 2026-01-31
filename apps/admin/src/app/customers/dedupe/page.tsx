// Path: apps/admin/src/app/customers/dedupe/page.tsx
"use client";

import React, { useMemo, useState } from "react";

type ChannelSummary = { channel: string; ordersCount: number; totalAmount: number; currency: string };
type AddressSnapshot = {
  country?: string | null;
  city?: string | null;
  district?: string | null;
  postalCode?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  fullName?: string | null;
};

type Candidate = {
  customerId: string;
  firstName?: string | null;
  lastName?: string | null;
  channels: ChannelSummary[];
  shippingAddress?: AddressSnapshot | null;
  billingAddress?: AddressSnapshot | null;
};

type Group = {
  groupKey: string;
  confidence: number;
  normalizedName: string;
  rationale: string;
  candidates: Candidate[];
};

type Result = { groups: Group[] };

export default function CustomerDedupePage() {
  const [threshold, setThreshold] = useState(0.88);
  const [loading, setLoading] = useState(false);
  const [res, setRes] = useState<Result | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const groups = useMemo(() => res?.groups ?? [], [res]);

  const analyze = async () => {
    setLoading(true);
    setErr(null);
    setRes(null);

    const r = await fetch("/api/customers/dedupe/analyze", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ threshold }),
    });

    const payload = await r.json().catch(() => null);

    setLoading(false);

    if (!r.ok) {
      setErr(payload?.message || JSON.stringify(payload));
      return;
    }

    setRes(payload);
  };

  return (
    <div className="p-4 sm:p-6">
      <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <div className="flex flex-wrap items-center gap-3">
          <h2 className="text-lg font-semibold text-dark dark:text-white">Customer Identity Analyze</h2>

          <div className="ml-auto flex items-center gap-2">
            <label className="text-sm text-body-color dark:text-dark-6">Threshold</label>
            <input
              type="number"
              step="0.01"
              min="0.5"
              max="0.99"
              value={threshold}
              onChange={(e) => setThreshold(Number(e.target.value || 0.88))}
              className="w-[110px] rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm text-dark outline-none dark:border-dark-3 dark:text-white"
            />
            <button
              className="rounded-lg bg-primary px-4 py-2 font-medium text-white hover:bg-opacity-90 disabled:opacity-60"
              onClick={analyze}
              disabled={loading}
            >
              {loading ? "Analyzing..." : "Analyze"}
            </button>
          </div>
        </div>

        {err && <div className="mt-3 rounded-lg border border-red-500 p-2 text-sm text-red-600">{err}</div>}

        {!groups.length && !loading && (
          <div className="mt-4 text-sm text-body-color dark:text-dark-6">
            Analiz sonucunu görmek için “Analyze” bas.
          </div>
        )}

        {groups.length > 0 && (
          <div className="mt-4 space-y-3">
            {groups.map((g) => (
              <div key={g.groupKey} className="rounded-lg border border-stroke p-3 dark:border-dark-3">
                <div className="flex flex-wrap items-center gap-2">
                  <div className="text-sm font-semibold text-dark dark:text-white">{g.normalizedName}</div>
                  <div className="text-xs text-body-color dark:text-dark-6">
                    confidence: <span className="font-mono">{g.confidence}</span>
                  </div>
                  <div className="text-xs text-body-color dark:text-dark-6">{g.rationale}</div>
                </div>

                <div className="mt-3 overflow-x-auto">
                  <table className="w-full table-auto">
                    <thead>
                      <tr className="text-left text-xs text-body-color dark:text-dark-6">
                        <th className="px-2 py-2">CustomerId</th>
                        <th className="px-2 py-2">Name</th>
                        <th className="px-2 py-2">Channels</th>
                        <th className="px-2 py-2">City</th>
                        <th className="px-2 py-2">District</th>
                        <th className="px-2 py-2">Postal</th>
                        <th className="px-2 py-2">Address</th>
                      </tr>
                    </thead>
                    <tbody>
                      {g.candidates.map((c) => {
                        const a = c.shippingAddress || c.billingAddress || {};
                        const ch = (c.channels || [])
                          .map((x) => `${x.channel}:${x.ordersCount}`)
                          .join(", ");

                        return (
                          <tr key={c.customerId} className="border-t border-stroke dark:border-dark-3 text-xs">
                            <td className="px-2 py-2 font-mono">{c.customerId}</td>
                            <td className="px-2 py-2">{`${c.firstName ?? ""} ${c.lastName ?? ""}`.trim()}</td>
                            <td className="px-2 py-2">{ch || "-"}</td>
                            <td className="px-2 py-2">{a.city ?? "-"}</td>
                            <td className="px-2 py-2">{a.district ?? "-"}</td>
                            <td className="px-2 py-2">{a.postalCode ?? "-"}</td>
                            <td className="px-2 py-2">{a.addressLine1 ?? "-"}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
