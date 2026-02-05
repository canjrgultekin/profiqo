"use client";

import React, { useEffect, useMemo, useState } from "react";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Legend,
} from "recharts";

// ─── Types ────────────────────────────────────────────────────────────────────
type Overview = {
  windowDays: number;
  totalCustomers: number;
  activeCustomers30: number;
  totalOrders30: number;
  grossRevenue30: number;
  netProfit30: number;
  currency: string;
};

type ChannelRow = {
  channel: string;
  orders: number;
  gross: number;
  profit: number;
};
type ChannelBreakdown = {
  windowDays: number;
  currency: string;
  items: ChannelRow[];
};

// ─── Helpers ──────────────────────────────────────────────────────────────────
function greeting(): string {
  const h = new Date().getHours();
  if (h < 6) return "İyi geceler";
  if (h < 12) return "Günaydın";
  if (h < 18) return "İyi günler";
  return "İyi akşamlar";
}

function fmt(n: number, currency?: string): string {
  const formatted = new Intl.NumberFormat("tr-TR", {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(n);
  return currency ? `${formatted} ${currency}` : formatted;
}

const CHANNEL_COLORS = [
  "#5750F1",
  "#22AD5C",
  "#3C50E0",
  "#F59E0B",
  "#F23030",
  "#8B5CF6",
  "#EC4899",
  "#06B6D4",
];

// ─── Main ─────────────────────────────────────────────────────────────────────
export default function DashboardPage() {
  const [overview, setOverview] = useState<Overview | null>(null);
  const [channels, setChannels] = useState<ChannelBreakdown | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const load = async () => {
    setErr(null);

    const oRes = await fetch("/api/reports/overview", { cache: "no-store" });
    const oPayload = await oRes.json().catch(() => null);
    if (!oRes.ok) {
      setErr(oPayload?.message || "Overview failed.");
      return;
    }
    setOverview(oPayload);

    const cRes = await fetch("/api/reports/channel-breakdown", {
      cache: "no-store",
    });
    const cPayload = await cRes.json().catch(() => null);
    if (!cRes.ok) {
      setErr(cPayload?.message || "Channel breakdown failed.");
      return;
    }
    setChannels(cPayload);
  };

  useEffect(() => {
    load();
  }, []);

  const donutData = useMemo(() => {
    if (!channels?.items?.length) return [];
    return channels.items.map((r) => ({
      name: r.channel,
      value: r.gross,
    }));
  }, [channels]);

  const revenueSummary = useMemo(() => {
    if (!overview || !channels) return [];
    if (!channels.items.length) return [];
    return channels.items.map((r) => ({
      channel: r.channel,
      Gelir: r.gross,
      "Kâr": r.profit,
      Sipariş: r.orders,
    }));
  }, [overview, channels]);

  const alerts = useMemo(() => {
    if (!overview) return [];
    const list: { type: "warning" | "info" | "success"; message: string }[] =
      [];

    if (overview.netProfit30 === 0 && overview.grossRevenue30 > 0) {
      list.push({
        type: "warning",
        message:
          "Net kâr hesaplanamıyor — ürün maliyetlerini kontrol edin.",
      });
    }

    if (overview.totalOrders30 === 0) {
      list.push({
        type: "info",
        message:
          "Son 30 günde sipariş alınmadı. Entegrasyonları kontrol edin.",
      });
    }

    const activeRatio =
      overview.totalCustomers > 0
        ? overview.activeCustomers30 / overview.totalCustomers
        : 0;
    if (activeRatio > 0 && activeRatio < 0.3) {
      list.push({
        type: "warning",
        message: `Aktif müşteri oranı düşük (%${Math.round(activeRatio * 100)}). Winback kampanyası düşünün.`,
      });
    }

    if (overview.totalCustomers > 0 && overview.totalOrders30 > 0) {
      list.push({
        type: "success",
        message: `Son ${overview.windowDays} günde ${fmt(overview.totalOrders30)} sipariş, ${fmt(overview.grossRevenue30, overview.currency)} ciro.`,
      });
    }

    return list;
  }, [overview]);

  // ── Error state ───────────────────────────────────────────────────────────
  if (err) {
    return (
      <div className="p-4 sm:p-6">
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-red-100 dark:bg-red-900/40">
            <svg
              className="h-5 w-5 text-red-600"
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
          </div>
          <div>
            <p className="text-sm font-medium text-red-800 dark:text-red-200">
              Dashboard yüklenemedi
            </p>
            <p className="mt-0.5 text-sm text-red-600 dark:text-red-300">
              {err}
            </p>
          </div>
        </div>
      </div>
    );
  }

  // ── Loading state ─────────────────────────────────────────────────────────
  if (!overview || !channels) {
    return (
      <div className="p-4 sm:p-6">
        <div className="mb-6">
          <div className="h-8 w-48 animate-pulse rounded-lg bg-gray-200 dark:bg-dark-3" />
          <div className="mt-2 h-4 w-72 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
        </div>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {[1, 2, 3, 4].map((i) => (
            <div
              key={i}
              className="rounded-xl bg-white p-5 shadow-1 dark:bg-gray-dark dark:shadow-card"
            >
              <div className="h-4 w-24 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
              <div className="mt-3 h-7 w-32 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
            </div>
          ))}
        </div>
      </div>
    );
  }

  const kpis = [
    {
      title: "Toplam Müşteri",
      value: fmt(overview.totalCustomers),
      color: "text-primary",
      bgColor: "bg-primary/10",
      icon: (
        <svg
          className="h-5 w-5"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={1.5}
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M15 19.128a9.38 9.38 0 002.625.372 9.337 9.337 0 004.121-.952 4.125 4.125 0 00-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 018.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0111.964-3.07M12 6.375a3.375 3.375 0 11-6.75 0 3.375 3.375 0 016.75 0zm8.25 2.25a2.625 2.625 0 11-5.25 0 2.625 2.625 0 015.25 0z"
          />
        </svg>
      ),
    },
    {
      title: `Aktif Müşteri (${overview.windowDays}g)`,
      value: fmt(overview.activeCustomers30),
      color: "text-green",
      bgColor: "bg-green/10",
      icon: (
        <svg
          className="h-5 w-5"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={1.5}
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M15.75 6a3.75 3.75 0 11-7.5 0 3.75 3.75 0 017.5 0zM4.501 20.118a7.5 7.5 0 0114.998 0A17.933 17.933 0 0112 21.75c-2.676 0-5.216-.584-7.499-1.632z"
          />
        </svg>
      ),
    },
    {
      title: `Sipariş (${overview.windowDays}g)`,
      value: fmt(overview.totalOrders30),
      color: "text-blue",
      bgColor: "bg-blue/10",
      icon: (
        <svg
          className="h-5 w-5"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={1.5}
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M15.75 10.5V6a3.75 3.75 0 10-7.5 0v4.5m11.356-1.993l1.263 12c.07.665-.45 1.243-1.119 1.243H4.25a1.125 1.125 0 01-1.12-1.243l1.264-12A1.125 1.125 0 015.513 7.5h12.974c.576 0 1.059.435 1.119 1.007zM8.625 10.5a.375.375 0 11-.75 0 .375.375 0 01.75 0zm7.5 0a.375.375 0 11-.75 0 .375.375 0 01.75 0z"
          />
        </svg>
      ),
    },
    {
      title: `Ciro (${overview.windowDays}g)`,
      value: fmt(overview.grossRevenue30, overview.currency),
      color: "text-yellow-dark",
      bgColor: "bg-yellow-dark/10",
      icon: (
        <svg
          className="h-5 w-5"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={1.5}
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M2.25 18.75a60.07 60.07 0 0115.797 2.101c.727.198 1.453-.342 1.453-1.096V18.75M3.75 4.5v.75A.75.75 0 013 6h-.75m0 0v-.375c0-.621.504-1.125 1.125-1.125H20.25M2.25 6v9m18-10.5v.75c0 .414.336.75.75.75h.75m-1.5-1.5h.375c.621 0 1.125.504 1.125 1.125v9.75c0 .621-.504 1.125-1.125 1.125h-.375m1.5-1.5H21a.75.75 0 00-.75.75v.75m0 0H3.75m0 0h-.375a1.125 1.125 0 01-1.125-1.125V15m1.5 1.5v-.75A.75.75 0 003 15h-.75M15 10.5a3 3 0 11-6 0 3 3 0 016 0zm3 0h.008v.008H18V10.5zm-12 0h.008v.008H6V10.5z"
          />
        </svg>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      {/* Greeting */}
      <div>
        <h1 className="text-2xl font-bold text-dark dark:text-white">
          {greeting()} 👋
        </h1>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
          Profiqo kontrol panelinize hoş geldiniz. İşte son{" "}
          {overview.windowDays} günün özeti.
        </p>
      </div>

      {/* Alerts */}
      {alerts.length > 0 && (
        <div className="space-y-2">
          {alerts.map((a, i) => (
            <div
              key={i}
              className={`flex items-center gap-3 rounded-lg px-4 py-3 text-sm ${
                a.type === "warning"
                  ? "border border-yellow-dark/20 bg-yellow-light-4 text-yellow-dark-2 dark:border-yellow-dark/30 dark:bg-yellow-dark/10 dark:text-yellow-light"
                  : a.type === "success"
                    ? "border border-green/20 bg-green-light-7 text-green-dark dark:border-green/30 dark:bg-green/10 dark:text-green-light-3"
                    : "border border-blue/20 bg-blue-light-5 text-blue-dark dark:border-blue/30 dark:bg-blue/10 dark:text-blue-light"
              }`}
            >
              {a.type === "warning" && (
                <svg
                  className="h-4 w-4 shrink-0"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  strokeWidth={2}
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z"
                  />
                </svg>
              )}
              {a.type === "success" && (
                <svg
                  className="h-4 w-4 shrink-0"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  strokeWidth={2}
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                  />
                </svg>
              )}
              {a.type === "info" && (
                <svg
                  className="h-4 w-4 shrink-0"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                  strokeWidth={2}
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M11.25 11.25l.041-.02a.75.75 0 011.063.852l-.708 2.836a.75.75 0 001.063.853l.041-.021M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-9-3.75h.008v.008H12V8.25z"
                  />
                </svg>
              )}
              <span>{a.message}</span>
            </div>
          ))}
        </div>
      )}

      {/* KPI Cards */}
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {kpis.map((kpi) => (
          <div
            key={kpi.title}
            className="rounded-xl border border-stroke bg-white p-5 shadow-1 transition-shadow hover:shadow-lg dark:border-dark-3 dark:bg-gray-dark dark:shadow-card"
          >
            <div className="flex items-center justify-between">
              <span className="text-sm font-medium text-dark-5 dark:text-dark-6">
                {kpi.title}
              </span>
              <div
                className={`flex h-9 w-9 items-center justify-center rounded-full ${kpi.bgColor} ${kpi.color}`}
              >
                {kpi.icon}
              </div>
            </div>
            <div className={`mt-3 text-2xl font-bold ${kpi.color}`}>
              {kpi.value}
            </div>
          </div>
        ))}
      </div>

      {/* Charts Row */}
      <div className="grid gap-4 xl:grid-cols-3">
        {/* Revenue Area Chart */}
        <div className="xl:col-span-2 rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h2 className="text-lg font-semibold text-dark dark:text-white">
                Kanal Bazlı Gelir ve Kâr
              </h2>
              <p className="mt-0.5 text-sm text-dark-5 dark:text-dark-6">
                Son {overview.windowDays} günlük performans
              </p>
            </div>
            <div className="flex items-center gap-4 text-xs text-dark-5 dark:text-dark-6">
              <span className="flex items-center gap-1.5">
                <span className="inline-block h-2.5 w-2.5 rounded-full bg-primary" />
                Gelir
              </span>
              <span className="flex items-center gap-1.5">
                <span className="inline-block h-2.5 w-2.5 rounded-full bg-green" />
                Kâr
              </span>
            </div>
          </div>

          {revenueSummary.length > 0 ? (
            <ResponsiveContainer width="100%" height={320}>
              <AreaChart
                data={revenueSummary}
                margin={{ top: 10, right: 10, left: 0, bottom: 0 }}
              >
                <defs>
                  <linearGradient id="colorGelir" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#5750F1" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="#5750F1" stopOpacity={0} />
                  </linearGradient>
                  <linearGradient id="colorKar" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#22AD5C" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="#22AD5C" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#E6EBF1" />
                <XAxis
                  dataKey="channel"
                  tick={{ fontSize: 12, fill: "#6B7280" }}
                  axisLine={{ stroke: "#E6EBF1" }}
                  tickLine={false}
                />
                <YAxis
                  tick={{ fontSize: 12, fill: "#6B7280" }}
                  axisLine={false}
                  tickLine={false}
                  tickFormatter={(v: number) => fmt(v)}
                />
                <Tooltip
                  contentStyle={{
                    backgroundColor: "#1F2A37",
                    border: "none",
                    borderRadius: "8px",
                    color: "#fff",
                    fontSize: "13px",
                  }}
                  formatter={(value: number) => [
                    fmt(value, channels?.currency),
                  ]}
                />
                <Area
                  type="monotone"
                  dataKey="Gelir"
                  stroke="#5750F1"
                  strokeWidth={2}
                  fillOpacity={1}
                  fill="url(#colorGelir)"
                />
                <Area
                  type="monotone"
                  dataKey="Kâr"
                  stroke="#22AD5C"
                  strokeWidth={2}
                  fillOpacity={1}
                  fill="url(#colorKar)"
                />
              </AreaChart>
            </ResponsiveContainer>
          ) : (
            <div className="flex h-[320px] items-center justify-center text-sm text-dark-5 dark:text-dark-6">
              Henüz kanal verisi yok.
            </div>
          )}
        </div>

        {/* Donut Chart */}
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <h2 className="text-lg font-semibold text-dark dark:text-white">
            Kanal Dağılımı
          </h2>
          <p className="mt-0.5 text-sm text-dark-5 dark:text-dark-6">
            Ciro bazında ({overview.currency})
          </p>

          {donutData.length > 0 ? (
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie
                  data={donutData}
                  cx="50%"
                  cy="50%"
                  innerRadius={60}
                  outerRadius={100}
                  paddingAngle={4}
                  dataKey="value"
                  stroke="none"
                >
                  {donutData.map((_, index) => (
                    <Cell
                      key={`cell-${index}`}
                      fill={CHANNEL_COLORS[index % CHANNEL_COLORS.length]}
                    />
                  ))}
                </Pie>
                <Tooltip
                  contentStyle={{
                    backgroundColor: "#1F2A37",
                    border: "none",
                    borderRadius: "8px",
                    color: "#fff",
                    fontSize: "13px",
                  }}
                  formatter={(value: number) => [
                    fmt(value, channels?.currency),
                  ]}
                />
                <Legend
                  verticalAlign="bottom"
                  iconType="circle"
                  iconSize={8}
                  formatter={(value: string) => (
                    <span className="text-xs text-dark-5 dark:text-dark-6">
                      {value}
                    </span>
                  )}
                />
              </PieChart>
            </ResponsiveContainer>
          ) : (
            <div className="flex h-[300px] items-center justify-center text-sm text-dark-5 dark:text-dark-6">
              Veri yok
            </div>
          )}
        </div>
      </div>

      {/* Channel Breakdown Table */}
      <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-dark dark:text-white">
            Kanal Detayları ({overview.windowDays}g)
          </h2>
          <span className="text-sm text-dark-5 dark:text-dark-6">
            {channels.items.length} kanal
          </span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-stroke text-left text-xs font-medium uppercase tracking-wider text-dark-5 dark:border-dark-3 dark:text-dark-6">
                <th className="px-4 py-3">Kanal</th>
                <th className="px-4 py-3 text-right">Sipariş</th>
                <th className="px-4 py-3 text-right">Ciro</th>
                <th className="px-4 py-3 text-right">Kâr</th>
                <th className="px-4 py-3 text-right">Kâr Marjı</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-stroke dark:divide-dark-3">
              {channels.items.map((r, idx) => {
                const margin =
                  r.gross > 0 ? ((r.profit / r.gross) * 100).toFixed(1) : "0";
                return (
                  <tr
                    key={r.channel}
                    className="text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2"
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2.5">
                        <span
                          className="inline-block h-2.5 w-2.5 rounded-full"
                          style={{
                            backgroundColor:
                              CHANNEL_COLORS[idx % CHANNEL_COLORS.length],
                          }}
                        />
                        <span className="font-medium text-dark dark:text-white">
                          {r.channel}
                        </span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-right font-medium text-dark dark:text-white">
                      {fmt(r.orders)}
                    </td>
                    <td className="px-4 py-3 text-right font-medium text-dark dark:text-white">
                      {fmt(r.gross, channels.currency)}
                    </td>
                    <td className="px-4 py-3 text-right font-medium text-dark dark:text-white">
                      {fmt(r.profit, channels.currency)}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <span
                        className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                          Number(margin) > 20
                            ? "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3"
                            : Number(margin) > 0
                              ? "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light"
                              : "bg-red-light-6 text-red-dark dark:bg-red/10 dark:text-red-light"
                        }`}
                      >
                        {margin}%
                      </span>
                    </td>
                  </tr>
                );
              })}
              {!channels.items.length && (
                <tr>
                  <td
                    className="px-4 py-8 text-center text-sm text-dark-5 dark:text-dark-6"
                    colSpan={5}
                  >
                    Henüz kanal verisi yok.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Revenue vs Profit Summary */}
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-full bg-primary/10">
              <svg
                className="h-5 w-5 text-primary"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth={1.5}
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M2.25 18L9 11.25l4.306 4.307a11.95 11.95 0 015.814-5.519l2.74-1.22m0 0l-5.94-2.28m5.94 2.28l-2.28 5.941"
                />
              </svg>
            </div>
            <div>
              <p className="text-sm text-dark-5 dark:text-dark-6">
                Brüt Ciro ({overview.windowDays}g)
              </p>
              <p className="text-xl font-bold text-dark dark:text-white">
                {fmt(overview.grossRevenue30, overview.currency)}
              </p>
            </div>
          </div>
        </div>
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-full bg-green/10">
              <svg
                className="h-5 w-5 text-green"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth={1.5}
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M12 6v12m-3-2.818l.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                />
              </svg>
            </div>
            <div>
              <p className="text-sm text-dark-5 dark:text-dark-6">
                Net Kâr ({overview.windowDays}g)
              </p>
              <p className="text-xl font-bold text-dark dark:text-white">
                {fmt(overview.netProfit30, overview.currency)}
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
