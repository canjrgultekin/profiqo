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

// ─── Helpers ──────────────────────────────────────────────────────────────────
function initials(first: string | null, last: string | null): string {
  const f = (first || "").trim();
  const l = (last || "").trim();
  if (f && l) return `${f[0]}${l[0]}`.toUpperCase();
  if (f) return f[0].toUpperCase();
  if (l) return l[0].toUpperCase();
  return "?";
}

const AVATAR_COLORS = [
  "bg-primary/15 text-primary",
  "bg-green/15 text-green",
  "bg-blue/15 text-blue",
  "bg-yellow-dark/15 text-yellow-dark",
  "bg-red/15 text-red",
  "bg-purple-500/15 text-purple-600",
  "bg-pink-500/15 text-pink-600",
  "bg-cyan-500/15 text-cyan-600",
];

function avatarColor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) {
    hash = id.charCodeAt(i) + ((hash << 5) - hash);
  }
  return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}

function rfmBadge(segment: string | null): { label: string; className: string } {
  if (!segment) return { label: "-", className: "text-dark-5 dark:text-dark-6" };
  const s = segment.toLowerCase();
  if (s.includes("champion"))
    return { label: segment, className: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" };
  if (s.includes("loyal"))
    return { label: segment, className: "bg-blue-light-5 text-blue-dark dark:bg-blue/10 dark:text-blue-light" };
  if (s.includes("risk") || s.includes("cant"))
    return { label: segment, className: "bg-red-light-6 text-red-dark dark:bg-red/10 dark:text-red-light" };
  if (s.includes("sleep") || s.includes("hibernat") || s.includes("lost"))
    return { label: segment, className: "bg-gray-200 text-dark-4 dark:bg-dark-3 dark:text-dark-6" };
  return { label: segment, className: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" };
}

function churnBadge(risk: number | null): { label: string; className: string } {
  if (risk === null || risk === undefined)
    return { label: "-", className: "text-dark-5 dark:text-dark-6" };
  const pct = Math.round(risk * 100);
  if (pct <= 30)
    return { label: `${pct}%`, className: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" };
  if (pct <= 60)
    return { label: `${pct}%`, className: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" };
  return { label: `${pct}%`, className: "bg-red-light-6 text-red-dark dark:bg-red/10 dark:text-red-light" };
}

function fmtDate(d: string): string {
  return new Date(d).toLocaleDateString("tr-TR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function exportCSV(items: CustomerRow[]) {
  const headers = ["Name", "RFM Segment", "Churn Risk", "LTV 12m", "Last Seen"];
  const rows = items.map((x) => [
    `${x.firstName || ""} ${x.lastName || ""}`.trim(),
    x.rfmSegment || "",
    x.churnRisk !== null ? `${Math.round(x.churnRisk * 100)}%` : "",
    x.ltv12mProfit?.toString() || "",
    x.lastSeenAtUtc ? new Date(x.lastSeenAtUtc).toISOString() : "",
  ]);
  const csv = [headers, ...rows].map((r) => r.map((c) => `"${c}"`).join(",")).join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `profiqo-customers-${new Date().toISOString().slice(0, 10)}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

// ─── Component ────────────────────────────────────────────────────────────────
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
      const res = await fetch(`/api/customers?${qs.toString()}`, {
        method: "GET",
        cache: "no-store",
      });
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

  useEffect(() => {
    load();
  }, [page]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleSearch = () => {
    setPage(1);
    load();
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-dark dark:text-white">
            Müşteriler
          </h1>
          <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
            Tüm kanallardan birleştirilmiş müşteri verileri
          </p>
        </div>
        {data && data.items.length > 0 && (
          <button
            onClick={() => exportCSV(data.items)}
            className="inline-flex items-center gap-2 rounded-lg border border-stroke bg-white px-4 py-2.5 text-sm font-medium text-dark shadow-sm transition-colors hover:bg-gray-1 dark:border-dark-3 dark:bg-gray-dark dark:text-white dark:hover:bg-dark-2"
          >
            <svg
              className="h-4 w-4"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={2}
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3"
              />
            </svg>
            CSV İndir
          </button>
        )}
      </div>

      {/* Search */}
      <div className="rounded-xl border border-stroke bg-white p-4 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex flex-wrap items-center gap-3">
          <div className="relative flex-1 min-w-[200px] max-w-md">
            <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
              <svg
                className="h-4 w-4 text-dark-5"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth={2}
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z"
                />
              </svg>
            </div>
            <input
              className="w-full rounded-lg border border-stroke bg-transparent py-2.5 pl-10 pr-4 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white"
              value={q}
              onChange={(e) => setQ(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleSearch()}
              placeholder="İsim ile ara..."
            />
          </div>
          <button
            onClick={handleSearch}
            className="rounded-lg bg-primary px-5 py-2.5 text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-60"
            disabled={loading}
          >
            Ara
          </button>
        </div>
      </div>

      {/* Error */}
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

      {/* Table */}
      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex items-center justify-between border-b border-stroke px-5 py-4 dark:border-dark-3">
          <h2 className="text-base font-semibold text-dark dark:text-white">
            Müşteri Listesi
          </h2>
          <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">
            {data ? `${data.total} kayıt` : "..."}
          </span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-stroke text-left text-xs font-medium uppercase tracking-wider text-dark-5 dark:border-dark-3 dark:text-dark-6">
                <th className="px-5 py-3">Müşteri</th>
                <th className="px-4 py-3">RFM Segment</th>
                <th className="px-4 py-3">Kayıp Riski</th>
                <th className="px-4 py-3 text-right">LTV (12ay)</th>
                <th className="px-4 py-3">Son Görülme</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-stroke dark:divide-dark-3">
              {loading && (
                <tr>
                  <td className="px-5 py-8 text-center text-sm text-dark-5" colSpan={5}>
                    <div className="flex items-center justify-center gap-2">
                      <div className="h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" />
                      Yükleniyor...
                    </div>
                  </td>
                </tr>
              )}

              {!loading && data?.items?.length
                ? data.items.map((x) => {
                    const name =
                      `${x.firstName || ""} ${x.lastName || ""}`.trim() ||
                      x.customerId.slice(0, 8);
                    const rfm = rfmBadge(x.rfmSegment);
                    const churn = churnBadge(x.churnRisk);

                    return (
                      <tr
                        key={x.customerId}
                        className="text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2"
                      >
                        <td className="px-5 py-3.5">
                          <a
                            href={`/customers/${x.customerId}`}
                            className="flex items-center gap-3"
                          >
                            <div
                              className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold ${avatarColor(x.customerId)}`}
                            >
                              {initials(x.firstName, x.lastName)}
                            </div>
                            <span className="font-medium text-dark hover:text-primary dark:text-white dark:hover:text-primary">
                              {name}
                            </span>
                          </a>
                        </td>
                        <td className="px-4 py-3.5">
                          {rfm.label !== "-" ? (
                            <span
                              className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${rfm.className}`}
                            >
                              {rfm.label}
                            </span>
                          ) : (
                            <span className="text-dark-6">-</span>
                          )}
                        </td>
                        <td className="px-4 py-3.5">
                          {churn.label !== "-" ? (
                            <span
                              className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${churn.className}`}
                            >
                              {churn.label}
                            </span>
                          ) : (
                            <span className="text-dark-6">-</span>
                          )}
                        </td>
                        <td className="px-4 py-3.5 text-right font-medium text-dark dark:text-white">
                          {x.ltv12mProfit !== null
                            ? new Intl.NumberFormat("tr-TR", {
                                minimumFractionDigits: 0,
                                maximumFractionDigits: 2,
                              }).format(x.ltv12mProfit)
                            : "-"}
                        </td>
                        <td className="px-4 py-3.5 text-sm text-dark-5 dark:text-dark-6">
                          {fmtDate(x.lastSeenAtUtc)}
                        </td>
                      </tr>
                    );
                  })
                : !loading && (
                    <tr>
                      <td
                        className="px-5 py-12 text-center text-sm text-dark-5 dark:text-dark-6"
                        colSpan={5}
                      >
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
                              d="M15 19.128a9.38 9.38 0 002.625.372 9.337 9.337 0 004.121-.952 4.125 4.125 0 00-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 018.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0111.964-3.07M12 6.375a3.375 3.375 0 11-6.75 0 3.375 3.375 0 016.75 0zm8.25 2.25a2.625 2.625 0 11-5.25 0 2.625 2.625 0 015.25 0z"
                            />
                          </svg>
                          <p>Müşteri bulunamadı.</p>
                        </div>
                      </td>
                    </tr>
                  )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="flex items-center justify-between border-t border-stroke px-5 py-4 dark:border-dark-3">
          <button
            className="inline-flex items-center gap-1.5 rounded-lg border border-stroke px-3.5 py-2 text-sm font-medium text-dark transition-colors hover:bg-gray-1 disabled:cursor-not-allowed disabled:opacity-50 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={loading || page <= 1}
          >
            <svg
              className="h-4 w-4"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={2}
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M15.75 19.5L8.25 12l7.5-7.5"
              />
            </svg>
            Önceki
          </button>

          <div className="flex items-center gap-2 text-sm">
            <span className="text-dark-5 dark:text-dark-6">Sayfa</span>
            <span className="rounded-md bg-primary/10 px-2.5 py-0.5 font-semibold text-primary">
              {page}
            </span>
            <span className="text-dark-5 dark:text-dark-6">/ {totalPages}</span>
          </div>

          <button
            className="inline-flex items-center gap-1.5 rounded-lg border border-stroke px-3.5 py-2 text-sm font-medium text-dark transition-colors hover:bg-gray-1 disabled:cursor-not-allowed disabled:opacity-50 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={loading || page >= totalPages}
          >
            Sonraki
            <svg
              className="h-4 w-4"
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
          </button>
        </div>
      </div>
    </div>
  );
}
