"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type StatusItem = {
  provider: string;
  connectionId: string;
  status: string;
  displayName: string;
  externalAccountId: string | null;
  healthy: boolean;
  cursors?: {
    customersUpdatedAtMs?: string | null;
    ordersUpdatedAtMs?: string | null;
    abandonedLastActivityMs?: string | null;
  } | null;
};

function providerMeta(p: string): {
  label: string;
  icon: string;
  desc: string;
  color: string;
  bgColor: string;
  route: string;
} {
  const lower = (p || "").toLowerCase();
  if (lower === "ikas")
    return {
      label: "ikas",
      icon: "🛒",
      desc: "E-ticaret Admin API",
      color: "text-primary",
      bgColor: "bg-primary/10 border-primary/20",
      route: "/integrations/ikas",
    };
  if (lower === "trendyol")
    return {
      label: "Trendyol",
      icon: "🟠",
      desc: "Marketplace Connector",
      color: "text-orange-light",
      bgColor: "bg-orange-light/10 border-orange-light/20",
      route: "/integrations/trendyol",
    };
  if (lower === "whatsapp")
    return {
      label: "WhatsApp",
      icon: "💬",
      desc: "Business API Messaging",
      color: "text-green",
      bgColor: "bg-green/10 border-green/20",
      route: "/integrations/whatsapp",
    };
  return {
    label: p,
    icon: "🔌",
    desc: "Connector",
    color: "text-dark-5",
    bgColor: "bg-gray-200 border-gray-300",
    route: "#",
  };
}

function cursorTime(ms: string | null | undefined): string {
  if (!ms) return "-";
  const num = Number(ms);
  if (isNaN(num) || num === 0) return "-";
  return new Date(num).toLocaleDateString("tr-TR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export default function IntegrationsHubPage() {
  const router = useRouter();
  const [items, setItems] = useState<StatusItem[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      setErr(null);
      setLoading(true);
      const res = await fetch("/api/integrations/status", {
        cache: "no-store",
      });
      const payload = await res.json().catch(() => null);

      if (cancelled) return;

      if (!res.ok) {
        setErr(payload?.message || "Failed to load integrations status.");
        setItems([]);
        setLoading(false);
        return;
      }

      setItems(payload?.items || []);
      setLoading(false);
    };

    load();

    return () => {
      cancelled = true;
    };
  }, []);

  const providerCards = ["ikas", "trendyol", "whatsapp"];
  const providerMap = Object.fromEntries(
    items.map((x) => [x.provider.toLowerCase(), x])
  );

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-dark dark:text-white">
          Entegrasyonlar
        </h1>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
          Veri kaynaklarınızı ve bağlantı durumlarını yönetin.
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

      {/* Provider Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        {providerCards.map((key) => {
          const meta = providerMeta(key);
          const item = providerMap[key];
          const connected = item?.healthy;

          return (
            <button
              key={key}
              onClick={() => router.push(meta.route)}
              className={`group relative rounded-xl border bg-white p-5 text-left shadow-1 transition-all hover:shadow-lg hover:border-primary/40 dark:bg-gray-dark dark:shadow-card ${
                connected
                  ? "border-stroke dark:border-dark-3"
                  : "border-dashed border-dark-6 dark:border-dark-4"
              }`}
            >
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <div
                    className={`flex h-12 w-12 items-center justify-center rounded-xl border text-xl ${meta.bgColor}`}
                  >
                    {meta.icon}
                  </div>
                  <div>
                    <h3 className="text-base font-semibold text-dark dark:text-white">
                      {meta.label}
                    </h3>
                    <p className="mt-0.5 text-xs text-dark-5 dark:text-dark-6">
                      {meta.desc}
                    </p>
                  </div>
                </div>

                {/* Connection status */}
                <div className="flex items-center gap-1.5">
                  {connected && (
                    <span className="relative flex h-2.5 w-2.5">
                      <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-green opacity-75" />
                      <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-green" />
                    </span>
                  )}
                  <span
                    className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${
                      connected
                        ? "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3"
                        : "bg-gray-200 text-dark-4 dark:bg-dark-3 dark:text-dark-6"
                    }`}
                  >
                    {connected ? "Bağlı" : "Bağlantı Yok"}
                  </span>
                </div>
              </div>

              {item && (
                <div className="mt-4 flex items-center gap-4 text-xs text-dark-5 dark:text-dark-6">
                  <span>
                    Mağaza:{" "}
                    <span className="font-medium text-dark dark:text-white">
                      {item.displayName || "-"}
                    </span>
                  </span>
                </div>
              )}

              {/* Hover arrow */}
              <div className="absolute right-4 top-1/2 -translate-y-1/2 opacity-0 transition-opacity group-hover:opacity-100">
                <svg
                  className="h-5 w-5 text-primary"
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
              </div>
            </button>
          );
        })}
      </div>

      {/* Connected Providers Table */}
      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex items-center justify-between border-b border-stroke px-5 py-4 dark:border-dark-3">
          <h2 className="text-base font-semibold text-dark dark:text-white">
            Bağlantı Durumları
          </h2>
          <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">
            {items.length} bağlantı
          </span>
        </div>

        {loading ? (
          <div className="flex items-center justify-center py-12">
            <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          </div>
        ) : items.length === 0 ? (
          <div className="px-5 py-12 text-center text-sm text-dark-5 dark:text-dark-6">
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
                  d="M13.19 8.688a4.5 4.5 0 011.242 7.244l-4.5 4.5a4.5 4.5 0 01-6.364-6.364l1.757-1.757m9.86-9.86a4.5 4.5 0 00-6.364 0l-4.5 4.5a4.5 4.5 0 001.242 7.244"
                />
              </svg>
              <p>Aktif bağlantı bulunamadı.</p>
            </div>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-stroke text-left text-xs font-medium uppercase tracking-wider text-dark-5 dark:border-dark-3 dark:text-dark-6">
                  <th className="px-5 py-3">Provider</th>
                  <th className="px-4 py-3">Sağlık</th>
                  <th className="px-4 py-3">Mağaza</th>
                  <th className="px-4 py-3">Son Müşteri Sync</th>
                  <th className="px-4 py-3">Son Sipariş Sync</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-stroke dark:divide-dark-3">
                {items.map((x) => {
                  const meta = providerMeta(x.provider);
                  return (
                    <tr
                      key={x.connectionId}
                      className="text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2"
                    >
                      <td className="px-5 py-3.5">
                        <div className="flex items-center gap-2.5">
                          <span className="text-base">{meta.icon}</span>
                          <span className="font-medium text-dark dark:text-white">
                            {meta.label}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3.5">
                        <div className="flex items-center gap-1.5">
                          {x.healthy && (
                            <span className="relative flex h-2 w-2">
                              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-green opacity-75" />
                              <span className="relative inline-flex h-2 w-2 rounded-full bg-green" />
                            </span>
                          )}
                          <span
                            className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${
                              x.healthy
                                ? "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3"
                                : "bg-red-light-6 text-red-dark dark:bg-red/10 dark:text-red-light"
                            }`}
                          >
                            {x.healthy ? "Sağlıklı" : x.status || "Sorunlu"}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3.5 text-dark dark:text-white">
                        {x.displayName || "-"}
                      </td>
                      <td className="px-4 py-3.5 text-dark-5 dark:text-dark-6">
                        {cursorTime(x.cursors?.customersUpdatedAtMs)}
                      </td>
                      <td className="px-4 py-3.5 text-dark-5 dark:text-dark-6">
                        {cursorTime(x.cursors?.ordersUpdatedAtMs)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
