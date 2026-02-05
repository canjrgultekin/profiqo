"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";

type SyncScope = "customers" | "orders" | "abandoned" | "both";
type StartSyncResponse = { batchId: string; jobs: { jobId: string; kind: string }[] };
type JobStatus = "Queued" | "Running" | "Succeeded" | "Failed" | "Cancelled" | string;
type IntegrationJobDto = { jobId: string; batchId: string; tenantId: string; connectionId: string; kind: string; status: JobStatus; pageSize: number; maxPages: number; processedItems: number; createdAtUtc: string; startedAtUtc?: string | null; finishedAtUtc?: string | null; lastError?: string | null };
type BatchResponse = { batchId: string; jobs: IntegrationJobDto[] };

function normalizeStartSync(payload: any): StartSyncResponse {
  const batchId = payload?.batchId ?? payload?.BatchId ?? "";
  const jobsRaw = payload?.jobs ?? payload?.Jobs ?? [];
  const jobs = Array.isArray(jobsRaw) ? jobsRaw.map((x: any) => ({ jobId: x?.jobId ?? x?.JobId ?? "", kind: x?.kind ?? x?.Kind ?? "" })) : [];
  return { batchId, jobs };
}
function normalizeBatch(payload: any): BatchResponse {
  const batchId = payload?.batchId ?? payload?.BatchId ?? "";
  const jobsRaw = payload?.jobs ?? payload?.Jobs ?? [];
  const jobs: IntegrationJobDto[] = Array.isArray(jobsRaw) ? jobsRaw.map((j: any) => ({ jobId: j?.jobId ?? j?.JobId ?? "", batchId: j?.batchId ?? j?.BatchId ?? batchId, tenantId: j?.tenantId ?? j?.TenantId ?? "", connectionId: j?.connectionId ?? j?.ConnectionId ?? "", kind: String(j?.kind ?? j?.Kind ?? ""), status: String(j?.status ?? j?.Status ?? ""), pageSize: Number(j?.pageSize ?? j?.PageSize ?? 0), maxPages: Number(j?.maxPages ?? j?.MaxPages ?? 0), processedItems: Number(j?.processedItems ?? j?.ProcessedItems ?? 0), createdAtUtc: String(j?.createdAtUtc ?? j?.CreatedAtUtc ?? ""), startedAtUtc: j?.startedAtUtc ?? j?.StartedAtUtc ?? null, finishedAtUtc: j?.finishedAtUtc ?? j?.FinishedAtUtc ?? null, lastError: j?.lastError ?? j?.LastError ?? null })) : [];
  return { batchId, jobs };
}

const statusBadge: Record<string, { icon: string; color: string }> = {
  Queued: { icon: "◷", color: "bg-gray-100 text-gray-600 dark:bg-dark-3 dark:text-dark-6" },
  Running: { icon: "⟳", color: "bg-blue-100 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400" },
  Succeeded: { icon: "✓", color: "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400" },
  Failed: { icon: "✕", color: "bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400" },
  Cancelled: { icon: "⊘", color: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/20 dark:text-yellow-400" },
};

function fmtDate(s: string | null | undefined) { if (!s) return "-"; try { return new Date(s).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit", second: "2-digit" }); } catch { return s; } }

export default function IkasIntegrationPage() {
  const [storeLabel, setStoreLabel] = useState("");
  const [storeName, setStoreName] = useState("");
  const [clientId, setClientId] = useState("");
  const [clientSecret, setClientSecret] = useState("");
  const [hasExisting, setHasExisting] = useState(false);
  const [connectionId, setConnectionId] = useState<string | null>(null);
  const [log, setLog] = useState<string>("");
  const append = (s: string) => setLog((x) => (x ? x + "\n" + s : s));
  const [batchId, setBatchId] = useState<string | null>(null);
  const [batch, setBatch] = useState<BatchResponse | null>(null);
  const [polling, setPolling] = useState(false);
  const pollTimer = useRef<number | null>(null);
  const [pageSize, setPageSize] = useState(50);
  const [maxPages, setMaxPages] = useState(20);
  const [scope, setScope] = useState<SyncScope>("both");

  const anyRunning = useMemo(() => (batch?.jobs || []).some((j) => j.status === "Queued" || j.status === "Running"), [batch]);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      const res = await fetch("/api/integrations/ikas/connection", { method: "GET", cache: "no-store" });
      const payload = await res.json().catch(() => null);
      if (cancelled) return;
      if (!res.ok) { append(`HATA: ${payload?.message || JSON.stringify(payload)}`); return; }
      const hasConnection = Boolean(payload?.hasConnection ?? payload?.HasConnection);
      if (!hasConnection) { setHasExisting(false); setConnectionId(null); return; }
      setHasExisting(true);
      const cid = payload?.connectionId ?? payload?.ConnectionId;
      setConnectionId(cid);
      setStoreLabel(payload?.displayName ?? payload?.DisplayName ?? "");
      setStoreName(payload?.externalAccountId ?? payload?.ExternalAccountId ?? "");
      setClientId(""); setClientSecret("");
      append(`Mevcut ikas bağlantısı yüklendi. connectionId=${cid}`);
    };
    load();
    return () => { cancelled = true; };
  }, []);

  const connectOrUpdate = async () => {
    setBatchId(null); setBatch(null);
    if (!storeLabel.trim()) { append("HATA: Store Label zorunlu."); return; }
    if (!storeName.trim()) { append("HATA: Store Name zorunlu."); return; }
    if (!hasExisting && (!clientId.trim() || !clientSecret.trim())) { append("HATA: İlk bağlantıda Client ID ve Secret zorunlu."); return; }
    const res = await fetch("/api/integrations/ikas/connect", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ storeLabel: storeLabel.trim(), storeName: storeName.trim(), clientId: clientId.trim(), clientSecret: clientSecret.trim() }) });
    const payload = await res.json().catch(() => null);
    if (!res.ok) { append(`HATA: ${payload?.message || JSON.stringify(payload)}`); return; }
    const newId = payload?.connectionId ?? payload?.ConnectionId;
    setConnectionId(newId); setHasExisting(true); setClientSecret("");
    append(`${hasExisting ? "Güncellendi" : "Bağlandı"}. connectionId=${newId}`);
  };

  const test = async () => {
    if (!connectionId) return;
    const res = await fetch("/api/integrations/ikas/test", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ connectionId }) });
    const payload = await res.json().catch(() => null);
    if (!res.ok) { append(`TEST HATA: ${payload?.message || JSON.stringify(payload)}`); return; }
    append("Test başarılı ✓");
  };

  const startSync = async () => {
    if (!connectionId) return;
    setBatchId(null); setBatch(null);
    const res = await fetch("/api/integrations/ikas/sync/start", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ connectionId, scope, pageSize, maxPages }) });
    const payload = await res.json().catch(() => null);
    if (!res.ok) { append(`SYNC HATA: ${payload?.message || JSON.stringify(payload)}`); return; }
    const r = normalizeStartSync(payload);
    setBatchId(r.batchId);
    append(`Sync başlatıldı. batchId=${r.batchId}`);
    if (r.jobs.length > 0) r.jobs.forEach((j) => append(`  Job: ${j.kind} → ${j.jobId}`));
    else append("Jobs: (boş)");
    setPolling(true);
  };

  const fetchBatch = async (id: string) => {
    const res = await fetch(`/api/integrations/ikas/jobs/batch/${id}`, { method: "GET", cache: "no-store" });
    const payload = await res.json().catch(() => null);
    if (!res.ok) { append(`POLL HATA: ${payload?.message || JSON.stringify(payload)}`); return null; }
    return normalizeBatch(payload);
  };

  useEffect(() => {
    if (!polling || !batchId) return;
    let cancelled = false;
    const tick = async () => { if (cancelled) return; const b = await fetchBatch(batchId); if (b) { setBatch(b); const running = (b.jobs || []).some((j) => j.status === "Queued" || j.status === "Running"); if (!running) { setPolling(false); append(`Batch tamamlandı. batchId=${batchId}`); } } };
    tick();
    pollTimer.current = window.setInterval(() => tick(), 2000);
    return () => { cancelled = true; if (pollTimer.current) { window.clearInterval(pollTimer.current); pollTimer.current = null; } };
  }, [polling, batchId]);

  const inputCls = "w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white";

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-6 flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-lg">🛒</div>
        <div>
          <h2 className="text-xl font-bold text-dark dark:text-white">ikas Entegrasyonu</h2>
          <p className="text-sm text-body-color dark:text-dark-6">Token yapıştırma yok. StoreName + ClientID + ClientSecret ile otomatik token alınır.</p>
        </div>
      </div>

      <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="grid gap-4 md:grid-cols-2">
          <div>
            <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Store Label</label>
            <input className={inputCls} value={storeLabel} onChange={(e) => setStoreLabel(e.target.value)} placeholder="Örn: profiqo-ikas" />
          </div>
          <div>
            <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Store Name</label>
            <input className={inputCls} value={storeName} onChange={(e) => setStoreName(e.target.value)} placeholder="Örn: profiqo" />
          </div>
          <div>
            <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Client ID {hasExisting ? "(opsiyonel)" : ""}</label>
            <input className={inputCls} value={clientId} onChange={(e) => setClientId(e.target.value)} placeholder={hasExisting ? "Boş bırakılırsa mevcut korunur" : "client_id"} />
          </div>
          <div>
            <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Client Secret {hasExisting ? "(opsiyonel)" : ""}</label>
            <input type="password" className={inputCls} value={clientSecret} onChange={(e) => setClientSecret(e.target.value)} placeholder={hasExisting ? "Boş bırakılırsa mevcut korunur" : "client_secret"} />
          </div>
        </div>

        <div className="mt-5 flex flex-wrap items-center gap-3">
          <button onClick={connectOrUpdate} className="rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:opacity-90">{hasExisting ? "Güncelle" : "Bağlan"}</button>
          <button onClick={test} disabled={!connectionId} className="rounded-lg border border-stroke px-5 py-2.5 text-sm font-semibold text-dark hover:bg-gray-1 disabled:opacity-50 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2">Test</button>

          <div className="ml-auto flex flex-wrap items-center gap-2">
            <select value={scope} onChange={(e) => setScope(e.target.value as SyncScope)} className="rounded-lg border border-stroke bg-transparent px-3 py-2 text-xs outline-none dark:border-dark-3 dark:text-white">
              <option value="both">Tümü</option>
              <option value="customers">Customers</option>
              <option value="orders">Orders</option>
              <option value="abandoned">Abandoned</option>
            </select>
            <div className="flex items-center gap-1">
              <span className="text-[10px] text-body-color dark:text-dark-6">Page</span>
              <input type="number" value={pageSize} onChange={(e) => setPageSize(Number(e.target.value || 50))} className="w-16 rounded-lg border border-stroke bg-transparent px-2 py-2 text-xs outline-none dark:border-dark-3 dark:text-white" />
            </div>
            <div className="flex items-center gap-1">
              <span className="text-[10px] text-body-color dark:text-dark-6">Max</span>
              <input type="number" value={maxPages} onChange={(e) => setMaxPages(Number(e.target.value || 20))} className="w-16 rounded-lg border border-stroke bg-transparent px-2 py-2 text-xs outline-none dark:border-dark-3 dark:text-white" />
            </div>
            <button onClick={startSync} disabled={!connectionId || polling || anyRunning} className={`rounded-lg px-5 py-2.5 text-sm font-semibold text-white shadow-sm ${polling || anyRunning ? "bg-yellow-500" : "bg-green-500 hover:opacity-90"} disabled:opacity-50`}>
              {polling || anyRunning ? "⟳ Sync Çalışıyor..." : "▶ Sync Başlat"}
            </button>
          </div>
        </div>

        {/* Batch Jobs */}
        {batchId && (
          <div className="mt-5 rounded-lg border border-stroke p-4 dark:border-dark-3">
            <div className="mb-3 flex items-center justify-between">
              <div className="text-sm text-dark dark:text-white">Batch: <span className="font-mono text-xs text-body-color dark:text-dark-6">{batchId.slice(0, 16)}…</span></div>
              <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${polling ? "bg-blue-100 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400" : "bg-gray-100 text-gray-600 dark:bg-dark-3 dark:text-dark-6"}`}>{polling ? "⟳ Polling" : "Idle"}</span>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full table-auto">
                <thead><tr className="border-b border-stroke dark:border-dark-3">
                  <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Kind</th>
                  <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Status</th>
                  <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">İşlenen</th>
                  <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Başlangıç</th>
                  <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Bitiş</th>
                  <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Hata</th>
                </tr></thead>
                <tbody className="divide-y divide-stroke dark:divide-dark-3">
                  {(batch?.jobs || []).map((j) => {
                    const sb = statusBadge[j.status] || statusBadge.Queued;
                    return (
                      <tr key={j.jobId} className="text-xs hover:bg-gray-1 dark:hover:bg-dark-2">
                        <td className="px-3 py-2 font-medium text-dark dark:text-white">{j.kind}</td>
                        <td className="px-3 py-2"><span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold ${sb.color}`}>{sb.icon} {j.status}</span></td>
                        <td className="px-3 py-2 text-dark dark:text-white">{j.processedItems}</td>
                        <td className="px-3 py-2 text-body-color dark:text-dark-6">{fmtDate(j.startedAtUtc)}</td>
                        <td className="px-3 py-2 text-body-color dark:text-dark-6">{fmtDate(j.finishedAtUtc)}</td>
                        <td className="max-w-[150px] truncate px-3 py-2 text-red-500">{j.lastError || "-"}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Log */}
        <div className="mt-5">
          <h4 className="mb-2 text-sm font-semibold text-dark dark:text-white">📋 Log</h4>
          <pre className="max-h-48 overflow-auto whitespace-pre-wrap rounded-lg border border-stroke bg-gray-1/30 p-3 text-xs text-dark dark:border-dark-3 dark:bg-dark-2/30 dark:text-white">{log || "Logs..."}</pre>
        </div>
      </div>
    </div>
  );
}
