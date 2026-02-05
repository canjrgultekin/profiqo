"use client";

import Breadcrumb from "@/components/Breadcrumbs/Breadcrumb";
import { useEffect, useState } from "react";

type RunRow = {
  batchId: string;
  ruleId: string;
  scheduledAtUtc: string;
  status: "queued" | "running" | "succeeded" | "failed";
  totalJobs: number;
  lastError: string | null;
};

type RunDetail = {
  batchId: string;
  ruleId: string;
  scheduledAtUtc: string;
  jobs: Array<{
    jobId: string;
    kind: string;
    status: string;
    connectionId: string;
    processedItems: number;
    createdAtUtc: string;
    startedAtUtc: string | null;
    finishedAtUtc: string | null;
    lastError: string | null;
  }>;
};

function fmtDate(d: string): string {
  if (!d) return "-";
  return new Date(d).toLocaleDateString("tr-TR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

function statusMeta(s: string): { label: string; className: string; icon: string } {
  const lower = (s || "").toLowerCase();
  if (lower === "succeeded")
    return {
      label: "Başarılı",
      className: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3",
      icon: "✓",
    };
  if (lower === "running")
    return {
      label: "Çalışıyor",
      className: "bg-blue-light-5 text-blue-dark dark:bg-blue/10 dark:text-blue-light",
      icon: "⟳",
    };
  if (lower === "failed")
    return {
      label: "Başarısız",
      className: "bg-red-light-6 text-red-dark dark:bg-red/10 dark:text-red-light",
      icon: "✕",
    };
  if (lower === "queued")
    return {
      label: "Sırada",
      className: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light",
      icon: "◷",
    };
  return {
    label: s,
    className: "bg-gray-200 text-dark-4 dark:bg-dark-3 dark:text-dark-6",
    icon: "?",
  };
}

function duration(start: string | null, end: string | null): string {
  if (!start || !end) return "-";
  const ms = new Date(end).getTime() - new Date(start).getTime();
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
  return `${Math.round(ms / 60000)}m`;
}

export default function SyncRunsPage() {
  const [runs, setRuns] = useState<RunRow[]>([]);
  const [detail, setDetail] = useState<RunDetail | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  async function load() {
    setErr(null);
    setLoading(true);
    const res = await fetch("/api/automation/sync/runs?take=50", {
      cache: "no-store",
    });
    if (!res.ok) {
      setErr(await res.text());
      setLoading(false);
      return;
    }
    const json = await res.json();
    setRuns(json.items ?? []);
    setLoading(false);
  }

  async function open(batchId: string) {
    setErr(null);
    if (detail?.batchId === batchId) {
      setDetail(null);
      return;
    }
    const res = await fetch(`/api/automation/sync/runs/${batchId}`, {
      cache: "no-store",
    });
    if (!res.ok) {
      setErr(await res.text());
      return;
    }
    const json = await res.json();
    setDetail(json);
  }

  useEffect(() => {
    void load();
  }, []);

  return (
    <div className="space-y-6">
      <Breadcrumb pageName="Automation / Sync Runs" />

      {err && (
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <svg className="h-5 w-5 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-red-600 dark:text-red-300">{err}</p>
        </div>
      )}

      {/* Runs List */}
      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex items-center justify-between border-b border-stroke px-5 py-4 dark:border-dark-3">
          <h2 className="text-base font-semibold text-dark dark:text-white">
            Çalıştırma Geçmişi
          </h2>
          <button
            onClick={load}
            className="inline-flex items-center gap-1.5 rounded-lg border border-stroke px-3.5 py-2 text-sm font-medium text-dark transition-colors hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
            </svg>
            Yenile
          </button>
        </div>

        {loading ? (
          <div className="flex items-center justify-center py-12">
            <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          </div>
        ) : (
          <div className="divide-y divide-stroke dark:divide-dark-3">
            {runs.map((r) => {
              const sm = statusMeta(r.status);
              const isOpen = detail?.batchId === r.batchId;

              return (
                <div key={r.batchId}>
                  <div className="flex items-center justify-between px-5 py-4 text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2">
                    <div className="flex items-center gap-3">
                      <span
                        className={`inline-flex h-7 w-7 items-center justify-center rounded-lg text-xs font-bold ${sm.className}`}
                      >
                        {sm.icon}
                      </span>
                      <div>
                        <p className="font-medium text-dark dark:text-white">
                          <span className="font-mono text-xs text-dark-5 dark:text-dark-6">
                            {r.batchId.slice(0, 8)}…
                          </span>
                        </p>
                        <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-dark-5 dark:text-dark-6">
                          <span>{fmtDate(r.scheduledAtUtc)}</span>
                          <span
                            className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${sm.className}`}
                          >
                            {sm.label}
                          </span>
                          <span>{r.totalJobs} job</span>
                        </div>
                        {r.lastError && (
                          <p className="mt-1 text-xs text-red">
                            {r.lastError}
                          </p>
                        )}
                      </div>
                    </div>
                    <button
                      onClick={() => open(r.batchId)}
                      className={`inline-flex items-center gap-1 rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors ${
                        isOpen
                          ? "border-primary bg-primary/5 text-primary"
                          : "border-stroke text-dark hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2"
                      }`}
                    >
                      {isOpen ? "Kapat" : "Detay"}
                    </button>
                  </div>

                  {/* Detail Panel */}
                  {isOpen && detail && (
                    <div className="border-t border-stroke bg-gray-1 px-5 py-4 dark:border-dark-3 dark:bg-dark-2">
                      <div className="overflow-x-auto">
                        <table className="w-full">
                          <thead>
                            <tr className="text-left text-xs font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">
                              <th className="px-3 py-2">Job Türü</th>
                              <th className="px-3 py-2">Durum</th>
                              <th className="px-3 py-2">Bağlantı</th>
                              <th className="px-3 py-2 text-right">İşlenen</th>
                              <th className="px-3 py-2 text-right">Süre</th>
                              <th className="px-3 py-2">Hata</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-stroke dark:divide-dark-3">
                            {detail.jobs.map((j) => {
                              const jm = statusMeta(j.status);
                              return (
                                <tr
                                  key={j.jobId}
                                  className="text-sm"
                                >
                                  <td className="px-3 py-2.5 font-medium text-dark dark:text-white">
                                    {j.kind}
                                  </td>
                                  <td className="px-3 py-2.5">
                                    <span
                                      className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${jm.className}`}
                                    >
                                      {jm.label}
                                    </span>
                                  </td>
                                  <td className="px-3 py-2.5 font-mono text-xs text-dark-5 dark:text-dark-6">
                                    {j.connectionId.slice(0, 8)}…
                                  </td>
                                  <td className="px-3 py-2.5 text-right font-medium text-dark dark:text-white">
                                    {j.processedItems}
                                  </td>
                                  <td className="px-3 py-2.5 text-right text-dark-5 dark:text-dark-6">
                                    {duration(j.startedAtUtc, j.finishedAtUtc)}
                                  </td>
                                  <td className="max-w-[200px] truncate px-3 py-2.5 text-xs text-red">
                                    {j.lastError ?? ""}
                                  </td>
                                </tr>
                              );
                            })}
                            {detail.jobs.length === 0 && (
                              <tr>
                                <td
                                  className="px-3 py-4 text-center text-sm text-dark-5"
                                  colSpan={6}
                                >
                                  Job yok
                                </td>
                              </tr>
                            )}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}

            {runs.length === 0 && (
              <div className="px-5 py-12 text-center text-sm text-dark-5 dark:text-dark-6">
                <div className="flex flex-col items-center gap-2">
                  <svg className="h-10 w-10 text-dark-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
                  </svg>
                  <p>Henüz çalıştırma kaydı yok.</p>
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
