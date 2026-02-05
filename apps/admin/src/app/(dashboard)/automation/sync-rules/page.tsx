"use client";

import Breadcrumb from "@/components/Breadcrumbs/Breadcrumb";
import { useEffect, useMemo, useState } from "react";

type Connection = {
  connectionId: string;
  providerType: string;
  displayName: string;
  status: string;
};

type Rule = {
  id: string;
  name: string;
  status: "active" | "paused";
  intervalMinutes: number;
  pageSize: number;
  maxPages: number;
  nextRunAtUtc: string;
  lastEnqueuedAtUtc: string | null;
  connectionIds: string[];
};

const intervalOptions = [
  { label: "10 dakikada 1", value: 10 },
  { label: "3 saatte 1", value: 180 },
  { label: "6 saatte 1", value: 360 },
  { label: "12 saatte 1", value: 720 },
  { label: "24 saatte 1 (günlük)", value: 1440 },
  { label: "Haftada 1", value: 10080 },
];

function fmtInterval(min: number): string {
  if (min < 60) return `${min} dk`;
  if (min < 1440) return `${Math.round(min / 60)} saat`;
  return `${Math.round(min / 1440)} gün`;
}

function fmtDate(d: string): string {
  if (!d) return "-";
  return new Date(d).toLocaleDateString("tr-TR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export default function SyncRulesPage() {
  const [connections, setConnections] = useState<Connection[]>([]);
  const [rules, setRules] = useState<Rule[]>([]);
  const [loading, setLoading] = useState(true);

  const [name, setName] = useState("Auto Sync");
  const [intervalMinutes, setIntervalMinutes] = useState(360);
  const [selected, setSelected] = useState<Record<string, boolean>>({});
  const [pageSize, setPageSize] = useState(100);
  const [maxPages, setMaxPages] = useState(50);

  const [err, setErr] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);

  const selectedIds = useMemo(
    () => Object.entries(selected).filter(([, v]) => v).map(([k]) => k),
    [selected]
  );

  async function load() {
    setLoading(true);
    setErr(null);

    try {
      const [cRes, rRes] = await Promise.all([
        fetch("/api/automation/sync/connections", { cache: "no-store" }),
        fetch("/api/automation/sync/rules", { cache: "no-store" }),
      ]);

      if (!cRes.ok) throw new Error(await cRes.text());
      if (!rRes.ok) throw new Error(await rRes.text());

      const cJson = await cRes.json();
      const rJson = await rRes.json();

      setConnections(cJson.items ?? []);
      setRules(rJson.items ?? []);

      const init: Record<string, boolean> = {};
      (cJson.items ?? []).forEach(
        (x: Connection) => (init[x.connectionId] = true)
      );
      setSelected(init);
    } catch (e: any) {
      setErr(e?.message ?? "Load failed");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function createRule() {
    setErr(null);
    setInfo(null);

    try {
      const res = await fetch("/api/automation/sync/rules", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          name,
          intervalMinutes,
          connectionIds: selectedIds,
          pageSize,
          maxPages,
        }),
      });

      if (!res.ok) throw new Error(await res.text());

      setInfo("Rule oluşturuldu.");
      await load();
    } catch (e: any) {
      setErr(e?.message ?? "Create failed");
    }
  }

  async function toggle(ruleId: string, action: "pause" | "activate") {
    setErr(null);
    setInfo(null);

    try {
      const res = await fetch(
        `/api/automation/sync/rules/${ruleId}/${action}`,
        { method: "POST" }
      );
      if (!res.ok) throw new Error(await res.text());
      await load();
    } catch (e: any) {
      setErr(e?.message ?? "Update failed");
    }
  }

  return (
    <div className="space-y-6">
      <Breadcrumb pageName="Automation / Sync Rules" />

      {err && (
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <svg className="h-5 w-5 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-red-600 dark:text-red-300">{err}</p>
        </div>
      )}
      {info && (
        <div className="flex items-center gap-3 rounded-xl border border-green/20 bg-green-light-7 px-5 py-4 dark:border-green/30 dark:bg-green/10">
          <svg className="h-5 w-5 shrink-0 text-green" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-green-dark dark:text-green-light-3">{info}</p>
        </div>
      )}

      {/* Create Rule Form */}
      <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <h2 className="text-lg font-semibold text-dark dark:text-white">
          Yeni Sync Rule
        </h2>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
          Otomatik veri senkronizasyon kuralı tanımlayın.
        </p>

        <div className="mt-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-dark dark:text-white">
              Rule Adı
            </label>
            <input
              className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2.5 text-sm text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-dark dark:text-white">
              Periyot
            </label>
            <select
              className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2.5 text-sm text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white dark:bg-gray-dark"
              value={intervalMinutes}
              onChange={(e) => setIntervalMinutes(Number(e.target.value))}
            >
              {intervalOptions.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-dark dark:text-white">
              PageSize
            </label>
            <input
              type="number"
              min={10}
              max={500}
              className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2.5 text-sm text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value))}
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-dark dark:text-white">
              MaxPages
            </label>
            <input
              type="number"
              min={1}
              max={500}
              className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2.5 text-sm text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={maxPages}
              onChange={(e) => setMaxPages(Number(e.target.value))}
            />
          </div>
        </div>

        <div className="mt-5">
          <p className="mb-2.5 text-sm font-medium text-dark dark:text-white">
            Çalışacak Bağlantılar
          </p>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {connections.map((c) => (
              <label
                key={c.connectionId}
                className={`flex cursor-pointer items-center gap-3 rounded-lg border p-3 transition-colors ${
                  selected[c.connectionId]
                    ? "border-primary bg-primary/5 dark:border-primary/50"
                    : "border-stroke dark:border-dark-3"
                }`}
              >
                <input
                  type="checkbox"
                  checked={!!selected[c.connectionId]}
                  onChange={(e) =>
                    setSelected((p) => ({
                      ...p,
                      [c.connectionId]: e.target.checked,
                    }))
                  }
                  className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary"
                />
                <div>
                  <span className="text-sm font-medium text-dark dark:text-white">
                    {c.displayName}
                  </span>
                  <span className="ml-2 text-xs text-dark-5 dark:text-dark-6">
                    ({c.providerType}, {c.status})
                  </span>
                </div>
              </label>
            ))}
          </div>
        </div>

        <button
          onClick={createRule}
          disabled={selectedIds.length === 0}
          className="mt-5 inline-flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-50"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
          </svg>
          Rule Oluştur
        </button>
      </div>

      {/* Existing Rules */}
      <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-dark dark:text-white">
            Mevcut Rule'lar
          </h2>
          {loading && (
            <div className="h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          )}
        </div>

        <div className="mt-4 space-y-3">
          {rules.map((r) => (
            <div
              key={r.id}
              className={`rounded-xl border p-4 transition-colors ${
                r.status === "active"
                  ? "border-green/20 bg-green-light-7/50 dark:border-green/20 dark:bg-green/5"
                  : "border-stroke dark:border-dark-3"
              }`}
            >
              <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                <div className="flex items-start gap-3">
                  <div
                    className={`mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg ${
                      r.status === "active"
                        ? "bg-green/10 text-green"
                        : "bg-gray-200 text-dark-5 dark:bg-dark-3"
                    }`}
                  >
                    <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
                    </svg>
                  </div>
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-dark dark:text-white">
                        {r.name}
                      </span>
                      <span
                        className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                          r.status === "active"
                            ? "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3"
                            : "bg-gray-200 text-dark-4 dark:bg-dark-3 dark:text-dark-6"
                        }`}
                      >
                        {r.status === "active" ? "Aktif" : "Duraklatıldı"}
                      </span>
                    </div>
                    <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-dark-5 dark:text-dark-6">
                      <span>Periyot: {fmtInterval(r.intervalMinutes)}</span>
                      <span>Bağlantı: {r.connectionIds.length}</span>
                      <span>Sonraki: {fmtDate(r.nextRunAtUtc)}</span>
                    </div>
                  </div>
                </div>

                <div className="flex gap-2">
                  {r.status === "active" ? (
                    <button
                      onClick={() => toggle(r.id, "pause")}
                      className="rounded-lg border border-red-200 px-4 py-2 text-xs font-medium text-red transition-colors hover:bg-red-50 dark:border-red/20 dark:hover:bg-red/10"
                    >
                      Duraklat
                    </button>
                  ) : (
                    <button
                      onClick={() => toggle(r.id, "activate")}
                      className="rounded-lg border border-green-200 px-4 py-2 text-xs font-medium text-green transition-colors hover:bg-green-50 dark:border-green/20 dark:hover:bg-green/10"
                    >
                      Aktifleştir
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))}

          {rules.length === 0 && !loading && (
            <p className="py-8 text-center text-sm text-dark-5 dark:text-dark-6">
              Henüz rule tanımlı değil.
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
