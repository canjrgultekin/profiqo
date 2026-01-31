"use client";

import React, { useEffect, useState } from "react";

type Overview = {
  windowDays: number;
  totalCustomers: number;
  activeCustomers30: number;
  totalOrders30: number;
  grossRevenue30: number;
  netProfit30: number;
  currency: string;
};

type ChannelRow = { channel: string; orders: number; gross: number; profit: number; };
type ChannelBreakdown = { windowDays: number; currency: string; items: ChannelRow[]; };

export default function DashboardPage() {
  const [overview, setOverview] = useState<Overview | null>(null);
  const [channels, setChannels] = useState<ChannelBreakdown | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const load = async () => {
    setErr(null);

    const oRes = await fetch("/api/reports/overview", { cache: "no-store" });
    const oPayload = await oRes.json().catch(() => null);
    if (!oRes.ok) { setErr(oPayload?.message || "Overview failed."); return; }
    setOverview(oPayload);

    const cRes = await fetch("/api/reports/channel-breakdown", { cache: "no-store" });
    const cPayload = await cRes.json().catch(() => null);
    if (!cRes.ok) { setErr(cPayload?.message || "Channel breakdown failed."); return; }
    setChannels(cPayload);
  };

  useEffect(() => { load(); }, []);

  if (err) {
    return (
      <div className="p-6">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card text-red-600">
          {err}
        </div>
      </div>
    );
  }

  if (!overview || !channels) {
    return (
      <div className="p-6">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          Loading dashboard...
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 sm:p-6">
      <h1 className="mb-4 text-xl font-semibold text-dark dark:text-white">Dashboard</h1>

      <div className="grid gap-4 md:grid-cols-4">
        <Card title="Total Customers" value={overview.totalCustomers} />
        <Card title={`Active Customers (${overview.windowDays}d)`} value={overview.activeCustomers30} />
        <Card title={`Orders (${overview.windowDays}d)`} value={overview.totalOrders30} />
        <Card title={`Net Profit (${overview.windowDays}d)`} value={`${overview.netProfit30} ${overview.currency}`} />
      </div>

      <div className="mt-6 rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h2 className="text-lg font-semibold text-dark dark:text-white">Channel Breakdown (30d)</h2>

        <div className="mt-3 overflow-x-auto">
          <table className="w-full table-auto">
            <thead>
              <tr className="text-left text-xs text-body-color dark:text-dark-6">
                <th className="px-2 py-2">Channel</th>
                <th className="px-2 py-2">Orders</th>
                <th className="px-2 py-2">Gross</th>
                <th className="px-2 py-2">Profit</th>
              </tr>
            </thead>
            <tbody>
              {channels.items.map((r) => (
                <tr key={r.channel} className="border-t border-stroke dark:border-dark-3 text-sm">
                  <td className="px-2 py-2">{r.channel}</td>
                  <td className="px-2 py-2">{r.orders}</td>
                  <td className="px-2 py-2">{r.gross} {channels.currency}</td>
                  <td className="px-2 py-2">{r.profit} {channels.currency}</td>
                </tr>
              ))}
              {!channels.items.length && (
                <tr className="border-t border-stroke dark:border-dark-3 text-sm">
                  <td className="px-2 py-3" colSpan={4}>No data</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      <div className="mt-4 rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h2 className="text-lg font-semibold text-dark dark:text-white">Revenue vs Profit (30d)</h2>
        <div className="mt-2 text-sm text-body-color dark:text-dark-6">
          Gross: {overview.grossRevenue30} {overview.currency}, Net Profit: {overview.netProfit30} {overview.currency}
        </div>
      </div>
    </div>
  );
}

function Card({ title, value }: { title: string; value: any }) {
  return (
    <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
      <div className="text-xs text-body-color dark:text-dark-6">{title}</div>
      <div className="mt-1 text-lg font-semibold text-dark dark:text-white">{value}</div>
    </div>
  );
}
