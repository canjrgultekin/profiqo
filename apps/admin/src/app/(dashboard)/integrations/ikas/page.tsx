// Path: apps/admin/src/app/(dashboard)/integrations/ikas/page.tsx
// Path: apps/admin/src/app/(dashboard)/integrations/ikas/page.tsx
"use client";

import React, { useEffect, useMemo, useRef, useState, useCallback } from "react";

// ── Types ───────────────────────────────────────────────────────

type SyncScope = "customers" | "orders" | "abandoned" | "both";
type StartSyncResponse = { batchId: string; jobs: { jobId: string; kind: string }[] };
type JobStatus = "Queued" | "Running" | "Succeeded" | "Failed" | "Cancelled" | string;
type IntegrationJobDto = { jobId: string; batchId: string; tenantId: string; connectionId: string; kind: string; status: JobStatus; pageSize: number; maxPages: number; processedItems: number; createdAtUtc: string; startedAtUtc?: string | null; finishedAtUtc?: string | null; lastError?: string | null };
type BatchResponse = { batchId: string; jobs: IntegrationJobDto[] };

type StorefrontRouting = {
  id: string;
  domain: string | null;
  locale: string | null;
  path: string | null;
  currencyCode: string | null;
  countryCodes: string[];
};

type Storefront = {
  id: string;
  name: string;
  salesChannelId: string | null;
  routings: StorefrontRouting[];
};

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
  Queued: { icon: "◷", color: "bg-gray-2 text-dark-5 dark:bg-dark-3 dark:text-dark-6" },
  Running: { icon: "⟳", color: "bg-blue-light-5 text-blue-dark dark:bg-blue/10 dark:text-blue-light" },
  Succeeded: { icon: "✓", color: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light" },
  Failed: { icon: "✕", color: "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" },
  Cancelled: { icon: "⊘", color: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" },
};

function fmtDate(s: string | null | undefined) { if (!s) return "-"; try { return new Date(s).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit", second: "2-digit" }); } catch { return s; } }

// ── Shared Styles ───────────────────────────────────────────────

const inputCls = "w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white";
const cardCls = "rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card";
const labelCls = "mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6";
const btnPrimary = "rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:opacity-90 disabled:opacity-50";
const btnOutline = "rounded-lg border border-stroke px-5 py-2.5 text-sm font-semibold text-dark hover:bg-gray-1 disabled:opacity-50 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2";

// ═══════════════════════════════════════════════════════════════
// Storefront Install Modal
// ═══════════════════════════════════════════════════════════════

function StorefrontInstallModal({
  open,
  onClose,
  scriptTag,
  onLog,
}: {
  open: boolean;
  onClose: () => void;
  scriptTag: string;
  onLog: (msg: string) => void;
}) {
  const [storefronts, setStorefronts] = useState<Storefront[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [installing, setInstalling] = useState(false);
  const [result, setResult] = useState<{ success: boolean; message: string } | null>(null);

  // Load storefronts when modal opens
  useEffect(() => {
    if (!open) return;
    setResult(null);
    setSelectedId(null);
    setLoading(true);
    (async () => {
      try {
        const res = await fetch("/api/integrations/ikas/storefronts", { cache: "no-store" });
        const data = await res.json().catch(() => null);
        if (!res.ok) {
          onLog(`HATA: Storefront listesi alınamadı — ${data?.message || res.status}`);
          setStorefronts([]);
          return;
        }
        const list: Storefront[] = (data?.storefronts || []).map((sf: any) => ({
          id: sf.id || "",
          name: sf.name || "",
          salesChannelId: sf.salesChannelId || null,
          routings: (sf.routings || []).map((r: any) => ({
            id: r.id || "",
            domain: r.domain || null,
            locale: r.locale || null,
            path: r.path || null,
            currencyCode: r.currencyCode || null,
            countryCodes: r.countryCodes || [],
          })),
        }));
        setStorefronts(list);
        if (list.length === 1) setSelectedId(list[0].id);
        onLog(`${list.length} storefront bulundu.`);
      } catch (e: any) {
        onLog(`HATA: ${e.message}`);
      } finally {
        setLoading(false);
      }
    })();
  }, [open]);

  const handleInstall = async () => {
    if (!selectedId || !scriptTag) return;
    setInstalling(true);
    setResult(null);
    try {
      const res = await fetch("/api/integrations/ikas/install-script", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          storefrontId: selectedId,
          scriptContent: scriptTag,
          scriptName: "ProfiqoPixel",
          isHighPriority: true,
        }),
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        const msg = data?.message || `HTTP ${res.status}`;
        setResult({ success: false, message: msg });
        onLog(`HATA: Script yüklenemedi — ${msg}`);
        return;
      }
      const sfName = storefronts.find((s) => s.id === selectedId)?.name || selectedId;
      setResult({ success: true, message: `Script "${sfName}" mağazasına başarıyla yüklendi!` });
      onLog(`Script yüklendi ✓ storefront=${sfName} scriptId=${data?.scriptId || "?"}`);
    } catch (e: any) {
      setResult({ success: false, message: e.message });
      onLog(`HATA: ${e.message}`);
    } finally {
      setInstalling(false);
    }
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/50 backdrop-blur-sm" onClick={onClose}>
      <div
        className="relative mx-4 w-full max-w-lg rounded-2xl border border-stroke bg-white p-6 shadow-2xl dark:border-dark-3 dark:bg-gray-dark"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="mb-5 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary/10 text-base">🚀</div>
            <div>
              <h3 className="text-base font-bold text-dark dark:text-white">Script'i Mağazaya Yükle</h3>
              <p className="text-xs text-body-color dark:text-dark-6">Pixel script'ini ikas mağazanıza otomatik entegre edin.</p>
            </div>
          </div>
          <button onClick={onClose} className="rounded-lg p-1.5 text-body-color transition-colors hover:bg-gray-1 hover:text-dark dark:text-dark-6 dark:hover:bg-dark-2 dark:hover:text-white">
            <svg width="18" height="18" viewBox="0 0 18 18" fill="none"><path d="M13.5 4.5L4.5 13.5M4.5 4.5L13.5 13.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" /></svg>
          </button>
        </div>

        {/* Content */}
        {loading ? (
          <div className="flex items-center justify-center py-10">
            <div className="h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
            <span className="ml-3 text-sm text-body-color dark:text-dark-6">Mağazalar yükleniyor...</span>
          </div>
        ) : storefronts.length === 0 ? (
          <div className="rounded-lg border border-yellow-200 bg-yellow-50 px-4 py-6 text-center dark:border-yellow-900/30 dark:bg-yellow-900/10">
            <p className="text-sm text-yellow-dark-2 dark:text-yellow-light">Storefront bulunamadı. ikas API bağlantınızı kontrol edin.</p>
          </div>
        ) : result?.success ? (
          /* Success State */
          <div className="py-4">
            <div className="rounded-lg border border-green-200 bg-green-50 px-4 py-5 text-center dark:border-green-900/30 dark:bg-green-900/10">
              <div className="mb-2 text-3xl">✅</div>
              <p className="text-sm font-medium text-green-dark dark:text-green-300">{result.message}</p>
              <p className="mt-2 text-xs text-green-600 dark:text-green-light">Script otomatik olarak aktif edildi. Mağazanızda test edebilirsiniz.</p>
            </div>
            <button onClick={onClose} className={`mt-4 w-full ${btnPrimary}`}>Kapat</button>
          </div>
        ) : (
          <>
            {/* Storefront List */}
            <div className="mb-4">
              <label className={labelCls}>Online Mağaza Seçin</label>
              <div className="max-h-60 space-y-2 overflow-y-auto">
                {storefronts.map((sf) => {
                  const isSelected = selectedId === sf.id;
                  const routing = sf.routings[0];
                  const domain = routing?.domain;
                  const locale = routing?.locale;
                  const countries = routing?.countryCodes?.join(", ");

                  return (
                    <button
                      key={sf.id}
                      onClick={() => setSelectedId(sf.id)}
                      className={`w-full rounded-lg border px-4 py-3 text-left transition-all ${
                        isSelected
                          ? "border-primary bg-primary/5 ring-1 ring-primary/20 dark:border-primary dark:bg-primary/10"
                          : "border-stroke hover:border-primary/40 hover:bg-gray-1/50 dark:border-dark-3 dark:hover:border-primary/30 dark:hover:bg-dark-2/50"
                      }`}
                    >
                      <div className="flex items-center gap-3">
                        <div className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-full border-2 ${
                          isSelected ? "border-primary" : "border-stroke dark:border-dark-3"
                        }`}>
                          {isSelected && <div className="h-2.5 w-2.5 rounded-full bg-primary" />}
                        </div>
                        <div className="min-w-0 flex-1">
                          <div className="flex items-center gap-2">
                            <span className="text-sm font-semibold text-dark dark:text-white">{sf.name}</span>
                            {locale && (
                              <span className="rounded bg-gray-2 px-1.5 py-0.5 text-[10px] font-medium uppercase text-dark-5 dark:bg-dark-3 dark:text-dark-6">
                                {locale}
                              </span>
                            )}
                            {countries && (
                              <span className="rounded bg-blue-50 px-1.5 py-0.5 text-[10px] font-medium text-blue-600 dark:bg-blue/10 dark:text-blue-light">
                                {countries}
                              </span>
                            )}
                          </div>
                          {domain && <div className="mt-0.5 truncate text-xs text-body-color dark:text-dark-6">{domain}</div>}
                          <div className="mt-0.5 font-mono text-[10px] text-body-color/60 dark:text-dark-6/60">{sf.id}</div>
                        </div>
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Error display */}
            {result && !result.success && (
              <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 dark:border-red-900/30 dark:bg-red-900/10">
                <p className="text-xs text-red-600 dark:text-red-light">{result.message}</p>
              </div>
            )}

            {/* Info */}
            <div className="mb-5 rounded-lg border border-blue-100 bg-blue-50/50 px-3 py-2.5 dark:border-blue-900/20 dark:bg-blue-900/5">
              <p className="text-[11px] leading-relaxed text-blue-dark dark:text-blue-light">
                Script seçilen mağazaya <strong>high priority</strong> olarak yüklenir ve otomatik aktif edilir.
                Mağazanın <code className="rounded bg-blue-light-5 px-1 dark:bg-blue-900/30">&lt;head&gt;</code> bölümünde en üstte yer alır.
              </p>
            </div>

            {/* Actions */}
            <div className="flex items-center gap-3">
              <button onClick={onClose} className={`flex-1 ${btnOutline}`}>Vazgeç</button>
              <button
                onClick={handleInstall}
                disabled={!selectedId || installing}
                className={`flex-1 rounded-lg px-5 py-2.5 text-sm font-semibold text-white shadow-sm disabled:opacity-50 ${
                  installing ? "bg-yellow-500" : "bg-green-500 hover:opacity-90"
                }`}
              >
                {installing ? "Yükleniyor..." : "Entegre Et"}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════
// ikas API Integration Section
// ═══════════════════════════════════════════════════════════════

function IkasApiSection() {
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
    if (!res.ok) return null;
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

  return (
    <div className={cardCls}>
      <div className="grid gap-4 md:grid-cols-2">
        <div>
          <label className={labelCls}>Store Label</label>
          <input className={inputCls} value={storeLabel} onChange={(e) => setStoreLabel(e.target.value)} placeholder="Örn: profiqo-ikas" />
        </div>
        <div>
          <label className={labelCls}>Store Name</label>
          <input className={inputCls} value={storeName} onChange={(e) => setStoreName(e.target.value)} placeholder="Örn: profiqo" />
        </div>
        <div>
          <label className={labelCls}>Client ID {hasExisting ? "(opsiyonel)" : ""}</label>
          <input className={inputCls} value={clientId} onChange={(e) => setClientId(e.target.value)} placeholder={hasExisting ? "Boş bırakılırsa mevcut korunur" : "client_id"} />
        </div>
        <div>
          <label className={labelCls}>Client Secret {hasExisting ? "(opsiyonel)" : ""}</label>
          <input type="password" className={inputCls} value={clientSecret} onChange={(e) => setClientSecret(e.target.value)} placeholder={hasExisting ? "Boş bırakılırsa mevcut korunur" : "client_secret"} />
        </div>
      </div>

      <div className="mt-5 flex flex-wrap items-center gap-3">
        <button onClick={connectOrUpdate} className={btnPrimary}>{hasExisting ? "Güncelle" : "Bağlan"}</button>
        <button onClick={test} disabled={!connectionId} className={btnOutline}>Test</button>

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
            <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${polling ? "bg-blue-light-5 text-blue-dark dark:bg-blue/10 dark:text-blue-light" : "bg-gray-2 text-dark-5 dark:bg-dark-3 dark:text-dark-6"}`}>{polling ? "⟳ Polling" : "Idle"}</span>
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
        <h4 className="mb-2 text-sm font-semibold text-dark dark:text-white">Log</h4>
        <pre className="max-h-48 overflow-auto whitespace-pre-wrap rounded-lg border border-stroke bg-gray-1/30 p-3 text-xs text-dark dark:border-dark-3 dark:bg-dark-2/30 dark:text-white">{log || "Logs..."}</pre>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════
// Storefront Events Pixel Section
// ═══════════════════════════════════════════════════════════════

function StorefrontPixelSection() {
  const [loading, setLoading] = useState(true);
  const [hasConnection, setHasConnection] = useState(false);
  const [publicApiKey, setPublicApiKey] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [scriptTag, setScriptTag] = useState("");
  const [status, setStatus] = useState("");
  const [saving, setSaving] = useState(false);
  const [rotating, setRotating] = useState(false);
  const [copied, setCopied] = useState<string | null>(null);
  const [log, setLog] = useState("");
  const append = useCallback((s: string) => setLog((x) => (x ? x + "\n" + s : s)), []);
  const [showInstallModal, setShowInstallModal] = useState(false);

  // Load existing pixel connection
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch("/api/integrations/pixel/connection", { cache: "no-store" });
        const data = await res.json().catch(() => null);
        if (cancelled) return;
        if (!res.ok) { append(`HATA: ${data?.message || "Bağlantı yüklenemedi"}`); setLoading(false); return; }
        if (data?.hasConnection) {
          setHasConnection(true);
          setPublicApiKey(data.publicApiKey || "");
          setDisplayName(data.displayName || "");
          setStatus(data.status || "Active");
          append(`Pixel bağlantısı yüklendi. Key: ${(data.publicApiKey || "").slice(0, 16)}...`);
          // Script tag'i yükle
          const tagRes = await fetch("/api/integrations/pixel/script-tag", { cache: "no-store" });
          const tagData = await tagRes.json().catch(() => null);
          if (!cancelled && tagData?.scriptTag) setScriptTag(tagData.scriptTag);
        }
      } catch (e: any) {
        if (!cancelled) append(`HATA: ${e.message}`);
      }
      if (!cancelled) setLoading(false);
    })();
    return () => { cancelled = true; };
  }, []);

  const connectPixel = async () => {
    if (!displayName.trim()) { append("HATA: Mağaza adı zorunlu."); return; }
    setSaving(true);
    try {
      const res = await fetch("/api/integrations/pixel/connect", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ displayName: displayName.trim() }),
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) { append(`HATA: ${data?.message || "Bağlantı oluşturulamadı"}`); return; }
      setHasConnection(true);
      setPublicApiKey(data.publicApiKey || "");
      setStatus("Active");
      append(`${data.updated ? "Güncellendi" : "Oluşturuldu"}. Key: ${data.publicApiKey}`);
      // Script tag yükle
      const tagRes = await fetch("/api/integrations/pixel/script-tag", { cache: "no-store" });
      const tagData = await tagRes.json().catch(() => null);
      if (tagData?.scriptTag) setScriptTag(tagData.scriptTag);
    } catch (e: any) { append(`HATA: ${e.message}`); }
    finally { setSaving(false); }
  };

  const rotateKey = async () => {
    if (!confirm("API Key yenilenecek. Mevcut mağaza script'i çalışmayı durduracak. Emin misiniz?")) return;
    setRotating(true);
    try {
      const res = await fetch("/api/integrations/pixel/rotate-key", { method: "POST" });
      const data = await res.json().catch(() => null);
      if (!res.ok) { append(`HATA: ${data?.message || "Key yenilenemedi"}`); return; }
      setPublicApiKey(data.publicApiKey || "");
      append(`Key yenilendi: ${data.publicApiKey}`);
      // Script tag güncelle
      const tagRes = await fetch("/api/integrations/pixel/script-tag", { cache: "no-store" });
      const tagData = await tagRes.json().catch(() => null);
      if (tagData?.scriptTag) setScriptTag(tagData.scriptTag);
    } catch (e: any) { append(`HATA: ${e.message}`); }
    finally { setRotating(false); }
  };

  const copyToClipboard = (text: string, label: string) => {
    navigator.clipboard.writeText(text).then(() => {
      setCopied(label);
      setTimeout(() => setCopied(null), 2000);
    });
  };

  if (loading) {
    return (
      <div className={cardCls}>
        <div className="flex items-center justify-center py-12">
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        </div>
      </div>
    );
  }

  return (
    <div className={cardCls}>
      {/* Install Modal */}
      <StorefrontInstallModal
        open={showInstallModal}
        onClose={() => setShowInstallModal(false)}
        scriptTag={scriptTag}
        onLog={append}
      />

      {/* Connection Status */}
      {hasConnection && (
        <div className="mb-5 flex items-center gap-3 rounded-lg border border-green-200 bg-green-50 px-4 py-3 dark:border-green-900/40 dark:bg-green-900/10">
          <span className="relative flex h-2.5 w-2.5">
            <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-green-400 opacity-75" />
            <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-green-500" />
          </span>
          <span className="text-sm font-medium text-green-dark dark:text-green-300">Pixel aktif</span>
          <span className="ml-auto font-mono text-xs text-green-600 dark:text-green-light">{publicApiKey}</span>
        </div>
      )}

      {/* Setup Form */}
      <div className="grid gap-4 md:grid-cols-2">
        <div className="md:col-span-2">
          <label className={labelCls}>Mağaza Adı / Domain</label>
          <input
            className={inputCls}
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            placeholder="Örn: testprofiqo.myikas.com"
          />
          <p className="mt-1 text-[11px] text-body-color dark:text-dark-6">
            ikas mağaza domain'iniz. Bu bilgi yönetim panelinde görüntülenir.
          </p>
        </div>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-3">
        <button onClick={connectPixel} disabled={saving} className={btnPrimary}>
          {saving ? "Kaydediliyor..." : hasConnection ? "Güncelle" : "Pixel Bağlantısı Oluştur"}
        </button>
        {hasConnection && (
          <button onClick={rotateKey} disabled={rotating} className="rounded-lg border border-red-300 px-4 py-2.5 text-sm font-semibold text-red-600 hover:bg-red-50 disabled:opacity-50 dark:border-red-800 dark:text-red-light dark:hover:bg-red/10">
            {rotating ? "Yenileniyor..." : "Key Yenile"}
          </button>
        )}
      </div>

      {/* Script Tag & API Key — sadece bağlantı varsa göster */}
      {hasConnection && (
        <>
          <div className="mt-6 border-t border-stroke pt-5 dark:border-dark-3">
            <h4 className="mb-3 text-sm font-semibold text-dark dark:text-white">Script Tag</h4>
            <p className="mb-3 text-xs text-body-color dark:text-dark-6">
              Bu kodu ikas admin panelinden mağazanızın <strong>Custom Scripts</strong> bölümüne yapıştırın
              veya <strong>"Mağazaya Yükle"</strong> butonuyla otomatik entegre edin.
            </p>
            <div className="relative">
              <pre className="overflow-auto whitespace-pre-wrap rounded-lg border border-stroke bg-gray-1/50 p-4 pr-24 font-mono text-xs text-dark dark:border-dark-3 dark:bg-dark-2/50 dark:text-white">
                {scriptTag || "<script> henüz oluşturulmadı — önce bağlantı kurun </script>"}
              </pre>
              <button
                onClick={() => copyToClipboard(scriptTag, "script")}
                className="absolute right-3 top-3 rounded-md bg-primary/10 px-3 py-1.5 text-xs font-medium text-primary transition-colors hover:bg-primary/20"
              >
                {copied === "script" ? "Kopyalandı ✓" : "Kopyala"}
              </button>
            </div>

            {/* ── Mağazaya Yükle butonu ── */}
            <button
              onClick={() => setShowInstallModal(true)}
              disabled={!scriptTag}
              className="mt-3 inline-flex items-center gap-2 rounded-lg bg-green-500 px-5 py-2.5 text-sm font-semibold text-white shadow-sm transition-opacity hover:opacity-90 disabled:opacity-50"
            >
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><path d="M8 1v10m0 0L4.5 7.5M8 11l3.5-3.5M2 13h12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
              Mağazaya Yükle
            </button>
          </div>

          <div className="mt-5">
            <h4 className="mb-3 text-sm font-semibold text-dark dark:text-white">API Key</h4>
            <div className="flex items-center gap-3">
              <code className="flex-1 rounded-lg border border-stroke bg-gray-1/50 px-4 py-2.5 font-mono text-sm text-dark dark:border-dark-3 dark:bg-dark-2/50 dark:text-white">
                {publicApiKey}
              </code>
              <button
                onClick={() => copyToClipboard(publicApiKey, "key")}
                className="shrink-0 rounded-lg bg-primary/10 px-4 py-2.5 text-xs font-medium text-primary transition-colors hover:bg-primary/20"
              >
                {copied === "key" ? "Kopyalandı ✓" : "Kopyala"}
              </button>
            </div>
          </div>

          {/* Tracked Events Info */}
          <div className="mt-5">
            <h4 className="mb-3 text-sm font-semibold text-dark dark:text-white">Takip Edilen Event'ler</h4>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              {[
                { name: "ADD_TO_CART", icon: "🛒", label: "Sepete Ekleme" },
                { name: "REMOVE_FROM_CART", icon: "🗑️", label: "Sepetten Çıkarma" },
                { name: "COMPLETE_CHECKOUT", icon: "✅", label: "Sipariş Tamamlama" },
                { name: "ADD_TO_WISHLIST", icon: "❤️", label: "İstek Listesine Ekleme" },
                { name: "PAGE_VIEW", icon: "👁️", label: "Sayfa Görüntüleme" },
                { name: "PRODUCT_VIEW", icon: "📦", label: "Ürün Görüntüleme" },
                { name: "BEGIN_CHECKOUT", icon: "💳", label: "Ödeme Başlatma" },
                { name: "SEARCH", icon: "🔍", label: "Arama" },
              ].map((evt) => (
                <div key={evt.name} className="flex items-center gap-2 rounded-lg border border-stroke px-3 py-2 dark:border-dark-3">
                  <span className="text-base">{evt.icon}</span>
                  <div>
                    <div className="text-xs font-medium text-dark dark:text-white">{evt.label}</div>
                    <div className="font-mono text-[10px] text-body-color dark:text-dark-6">{evt.name}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Installation Guide */}
          <div className="mt-5 rounded-lg border border-blue-200 bg-blue-50/50 p-4 dark:border-blue-900/30 dark:bg-blue-900/10">
            <h4 className="mb-2 text-sm font-semibold text-blue-800 dark:text-blue-300">Kurulum Rehberi</h4>
            <ol className="space-y-1.5 text-xs text-blue-dark dark:text-blue-light">
              <li><strong>Otomatik:</strong> Yukarıdaki <strong>"Mağazaya Yükle"</strong> butonunu kullanarak script'i otomatik olarak ikas mağazanıza yükleyin.</li>
              <li><strong>Manuel 1:</strong> Script tag'i kopyalayın → ikas admin paneli → <strong>Online Mağaza</strong> → <strong>Tema Düzenle</strong> → Custom Scripts.</li>
              <li><strong>Manuel 2:</strong> Script'i <code className="rounded bg-blue-light-5 px-1 dark:bg-blue-900/30">&lt;head&gt;</code> bölümüne yapıştırın.</li>
              <li><strong>Test:</strong> Mağazanızda sepete ürün ekleyerek test edin. Event'ler otomatik olarak Profiqo'ya iletilir.</li>
            </ol>
          </div>
        </>
      )}

      {/* Log */}
      <div className="mt-5">
        <h4 className="mb-2 text-sm font-semibold text-dark dark:text-white">Log</h4>
        <pre className="max-h-36 overflow-auto whitespace-pre-wrap rounded-lg border border-stroke bg-gray-1/30 p-3 text-xs text-dark dark:border-dark-3 dark:bg-dark-2/30 dark:text-white">{log || "Logs..."}</pre>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════
// Main Page
// ═══════════════════════════════════════════════════════════════

export default function IkasIntegrationPage() {
  const [activeTab, setActiveTab] = useState<"api" | "pixel">("api");

  return (
    <div className="p-4 sm:p-6">
      {/* Header */}
      <div className="mb-6 flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-lg">🛒</div>
        <div>
          <h2 className="text-xl font-bold text-dark dark:text-white">ikas Entegrasyonu</h2>
          <p className="text-sm text-body-color dark:text-dark-6">API bağlantısı ve storefront event tracking yönetimi.</p>
        </div>
      </div>

      {/* Tab Navigation */}
      <div className="mb-5 flex gap-1 rounded-lg border border-stroke bg-gray-1/50 p-1 dark:border-dark-3 dark:bg-dark-2/50">
        <button
          onClick={() => setActiveTab("api")}
          className={`flex items-center gap-2 rounded-md px-4 py-2.5 text-sm font-medium transition-colors ${
            activeTab === "api"
              ? "bg-white text-primary shadow-sm dark:bg-gray-dark"
              : "text-body-color hover:text-dark dark:text-dark-6 dark:hover:text-white"
          }`}
        >
          <span>🔗</span>
          API Entegrasyonu
        </button>
        <button
          onClick={() => setActiveTab("pixel")}
          className={`flex items-center gap-2 rounded-md px-4 py-2.5 text-sm font-medium transition-colors ${
            activeTab === "pixel"
              ? "bg-white text-primary shadow-sm dark:bg-gray-dark"
              : "text-body-color hover:text-dark dark:text-dark-6 dark:hover:text-white"
          }`}
        >
          <span>📡</span>
          Storefront Events
        </button>
      </div>

      {/* Tab Content */}
      {activeTab === "api" && <IkasApiSection />}
      {activeTab === "pixel" && <StorefrontPixelSection />}
    </div>
  );
}