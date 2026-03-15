// Path: apps/admin/src/app/(dashboard)/integrations/shopify/page.tsx
"use client";
import React, { useEffect, useMemo, useRef, useState } from "react";

type StartSyncResponse = { batchId: string; jobs: { jobId: string; kind: string }[] };
type JobStatus = "Queued" | "Running" | "Succeeded" | "Failed" | "Cancelled" | string;
type IntegrationJobDto = { jobId: string; batchId: string; tenantId: string; connectionId: string; kind: string; status: JobStatus; pageSize: number; maxPages: number; processedItems: number; createdAtUtc: string; startedAtUtc?: string | null; finishedAtUtc?: string | null; lastError?: string | null };
type BatchResponse = { batchId: string; jobs: IntegrationJobDto[] };

function normalizeStartSync(p: any): StartSyncResponse { const b = p?.batchId ?? p?.BatchId ?? ""; const jr = p?.jobs ?? p?.Jobs ?? []; return { batchId: b, jobs: Array.isArray(jr) ? jr.map((x: any) => ({ jobId: x?.jobId ?? x?.JobId ?? "", kind: x?.kind ?? x?.Kind ?? "" })) : [] }; }
function normalizeBatch(p: any): BatchResponse { const b = p?.batchId ?? p?.BatchId ?? ""; const jr = p?.jobs ?? p?.Jobs ?? []; return { batchId: b, jobs: Array.isArray(jr) ? jr.map((j: any) => ({ jobId: j?.jobId ?? j?.JobId ?? "", batchId: j?.batchId ?? j?.BatchId ?? b, tenantId: j?.tenantId ?? j?.TenantId ?? "", connectionId: j?.connectionId ?? j?.ConnectionId ?? "", kind: String(j?.kind ?? j?.Kind ?? ""), status: String(j?.status ?? j?.Status ?? ""), pageSize: Number(j?.pageSize ?? j?.PageSize ?? 0), maxPages: Number(j?.maxPages ?? j?.MaxPages ?? 0), processedItems: Number(j?.processedItems ?? j?.ProcessedItems ?? 0), createdAtUtc: String(j?.createdAtUtc ?? j?.CreatedAtUtc ?? ""), startedAtUtc: j?.startedAtUtc ?? j?.StartedAtUtc ?? null, finishedAtUtc: j?.finishedAtUtc ?? j?.FinishedAtUtc ?? null, lastError: j?.lastError ?? j?.LastError ?? null })) : [] }; }

const statusBadge: Record<string, { icon: string; color: string }> = { Queued: { icon: "◷", color: "bg-gray-2 text-dark-5 dark:bg-dark-3 dark:text-dark-6" }, Running: { icon: "⟳", color: "bg-blue-light-5 text-blue-dark dark:bg-blue/10 dark:text-blue-light" }, Succeeded: { icon: "✓", color: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light" }, Failed: { icon: "✕", color: "bg-red-light-5 text-red-dark dark:bg-red/10 dark:text-red-light" }, Cancelled: { icon: "⊘", color: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" } };
function fmtDate(s: string | null | undefined) { if (!s) return "-"; try { return new Date(s).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit", second: "2-digit" }); } catch { return s; } }

export default function ShopifyIntegrationPage() {
  const [displayName, setDisplayName] = useState("Shopify");
  const [shopName, setShopName] = useState("");
  const [clientId, setClientId] = useState("");
  const [clientSecret, setClientSecret] = useState("");
  const [hasExisting, setHasExisting] = useState(false);
  const [connectionId, setConnectionId] = useState<string | null>(null);
  const [log, setLog] = useState("");
  const append = (s: string) => setLog((x) => (x ? x + "\n" + s : s));
  const [batchId, setBatchId] = useState<string | null>(null);
  const [batch, setBatch] = useState<BatchResponse | null>(null);
  const [polling, setPolling] = useState(false);
  const pollTimer = useRef<number | null>(null);
  const [pageSize, setPageSize] = useState(50);
  const [maxPages, setMaxPages] = useState(100);
  const [scope, setScope] = useState("all");
  const [showGuide, setShowGuide] = useState(false);
  const anyRunning = useMemo(() => (batch?.jobs || []).some((j) => j.status === "Queued" || j.status === "Running"), [batch]);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      const res = await fetch("/api/integrations/shopify/connection", { method: "GET", cache: "no-store" });
      const payload = await res.json().catch(() => null);
      if (cancelled) return;
      if (!res.ok) return append(`HATA: ${payload?.message || JSON.stringify(payload)}`);
      const has = Boolean(payload?.hasConnection ?? payload?.HasConnection);
      if (!has) { setHasExisting(false); setConnectionId(null); return; }
      setHasExisting(true);
      const cid = payload?.connectionId ?? payload?.ConnectionId;
      setConnectionId(cid);
      setDisplayName(payload?.displayName ?? payload?.DisplayName ?? "Shopify");
      setShopName(payload?.shopName ?? payload?.ShopName ?? payload?.externalAccountId ?? "");
      append(`Mevcut Shopify bağlantısı yüklendi. connectionId=${cid}`);
    };
    load();
    return () => { cancelled = true; };
  }, []);

  const connectOrUpdate = async () => {
    setBatchId(null); setBatch(null);
    if (!displayName.trim()) return append("HATA: Display Name zorunlu.");
    if (!shopName.trim()) return append("HATA: Shop Name zorunlu.");
    if (!clientId.trim()) return append("HATA: Client ID zorunlu. Dev Dashboard > Ayarlar kısmından alınır.");
    if (!clientSecret.trim()) return append("HATA: Client Secret zorunlu. Dev Dashboard > Ayarlar kısmından alınır.");
    append("Token alınıyor...");
    const res = await fetch("/api/integrations/shopify/connect", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ displayName, shopName, clientId, clientSecret }) });
    const payload = await res.json().catch(() => null);
    if (!res.ok) return append(`HATA: ${payload?.message || JSON.stringify(payload)}`);
    const cid = payload?.connectionId ?? payload?.ConnectionId;
    setConnectionId(cid); setHasExisting(true);
    append(`${hasExisting ? "Güncellendi" : "Bağlandı"}. Token otomatik alındı ve şifrelenmiş olarak kaydedildi. connectionId=${cid}`);
  };

  const test = async () => {
    if (!connectionId) return;
    const res = await fetch("/api/integrations/shopify/test", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ connectionId }) });
    const payload = await res.json().catch(() => null);
    if (!res.ok) return append(`TEST HATA: ${payload?.message || JSON.stringify(payload)}`);
    append("Test başarılı ✓");
  };

  const startSync = async () => {
    if (!connectionId) return;
    setBatchId(null); setBatch(null);
    const res = await fetch("/api/integrations/shopify/sync/start", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ connectionId, scope, pageSize, maxPages }) });
    const payload = await res.json().catch(() => null);
    if (!res.ok) return append(`SYNC HATA: ${payload?.message || JSON.stringify(payload)}`);
    const r = normalizeStartSync(payload);
    setBatchId(r.batchId); append(`Sync başlatıldı. batchId=${r.batchId}`);
    r.jobs.forEach((j) => append(`  Job: ${j.kind} → ${j.jobId}`));
    setPolling(true);
  };

  const fetchBatch = async (id: string) => { const res = await fetch(`/api/integrations/ikas/jobs/batch/${id}`, { method: "GET", cache: "no-store" }); const p = await res.json().catch(() => null); if (!res.ok) return null; return normalizeBatch(p); };

  useEffect(() => {
    if (!polling || !batchId) return;
    let cancelled = false;
    const tick = async () => { if (cancelled) return; const b = await fetchBatch(batchId); if (b) { setBatch(b); if (!(b.jobs || []).some((j) => j.status === "Queued" || j.status === "Running")) { setPolling(false); append(`Batch tamamlandı. batchId=${batchId}`); } } };
    tick(); pollTimer.current = window.setInterval(() => tick(), 2000);
    return () => { cancelled = true; if (pollTimer.current) { window.clearInterval(pollTimer.current); pollTimer.current = null; } };
  }, [polling, batchId]);

  const inputCls = "w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white";
  const selectCls = "rounded-lg border border-stroke bg-transparent px-2 py-2 text-xs outline-none dark:border-dark-3 dark:text-white";

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-6 flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-green-100 text-lg dark:bg-green-900/20">🟢</div>
        <div>
          <h2 className="text-xl font-bold text-dark dark:text-white">Shopify Entegrasyonu</h2>
          <p className="text-sm text-body-color dark:text-dark-6">Admin API bağlantısı, müşteri, sipariş ve ürün senkronizasyonu.</p>
        </div>
      </div>

      {/* Setup Guide */}
      <div className="mb-5 rounded-xl border border-green-200 bg-green-50/50 dark:border-green-900/40 dark:bg-green-900/10">
        <button onClick={() => setShowGuide(!showGuide)} className="flex w-full items-center justify-between px-4 py-3 text-left">
          <div className="flex items-center gap-2">
            <span className="text-base">📋</span>
            <span className="text-sm font-medium text-dark dark:text-white">Shopify App Kurulum Rehberi</span>
          </div>
          <span className="text-xs text-body-color dark:text-dark-6">{showGuide ? "▲ Kapat" : "▼ Aç"}</span>
        </button>
        {showGuide && (
          <div className="space-y-2 border-t border-green-200 px-4 py-3 text-xs text-body-color dark:border-green-900/40 dark:text-dark-6">
            <p><strong className="text-dark dark:text-white">1.</strong> <a href="https://dev.shopify.com" target="_blank" rel="noopener" className="text-primary underline">dev.shopify.com</a> adresine gidin ve "Uygulama oluştur" butonuna tıklayın.</p>
            <p><strong className="text-dark dark:text-white">2.</strong> App adını "Profiqo" olarak girin, Uygulama URL'sini <code className="rounded bg-gray-2 px-1 dark:bg-dark-3">https://example.com</code> bırakın.</p>
            <p><strong className="text-dark dark:text-white">3.</strong> "Sürümler" sekmesinde yeni sürüm oluşturun. Access scopes bölümünde şunları ekleyin: <code className="rounded bg-gray-2 px-1 dark:bg-dark-3">read_orders</code>, <code className="rounded bg-gray-2 px-1 dark:bg-dark-3">read_customers</code>, <code className="rounded bg-gray-2 px-1 dark:bg-dark-3">read_products</code></p>
            <p><strong className="text-dark dark:text-white">4.</strong> Sürümü yayınlayın (Release).</p>
            <p><strong className="text-dark dark:text-white">5.</strong> App'i mağazanıza yükleyin (Install).</p>
            <p><strong className="text-dark dark:text-white">6.</strong> Sol menüden "Ayarlar" sekmesine gidin. <strong>İstemci Kimliği</strong> (Client ID) ve <strong>Gizli Anahtar</strong> (Client Secret) değerlerini kopyalayın.</p>
            <p><strong className="text-dark dark:text-white">7.</strong> Bu bilgileri aşağıdaki forma yapıştırın. Token otomatik alınır ve 24 saatte bir yenilenir.</p>
          </div>
        )}
      </div>

      <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="grid gap-4 md:grid-cols-2">
          <div>
            <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Display Name</label>
            <input className={inputCls} value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
          </div>
          <div>
            <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Shop Name</label>
            <input className={inputCls} value={shopName} onChange={(e) => setShopName(e.target.value)} placeholder="mystore (veya mystore.myshopify.com)" />
          </div>
          <div>
            <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Client ID <span className="normal-case text-body-color dark:text-dark-6">(İstemci Kimliği)</span></label>
            <input className={inputCls} value={clientId} onChange={(e) => setClientId(e.target.value)} placeholder="Dev Dashboard > Ayarlar > İstemci Kimliği" />
          </div>
          <div>
            <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Client Secret <span className="normal-case text-body-color dark:text-dark-6">(Gizli Anahtar)</span></label>
            <input className={inputCls} type="password" value={clientSecret} onChange={(e) => setClientSecret(e.target.value)} placeholder="shpss_xxxxxxxxxxxxxxxx" />
          </div>
        </div>

        <div className="mt-5 flex flex-wrap items-center gap-3">
          <button onClick={connectOrUpdate} className="rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:opacity-90">{hasExisting ? "Güncelle" : "Bağlan"}</button>
          <button onClick={test} disabled={!connectionId} className="rounded-lg border border-stroke px-5 py-2.5 text-sm font-semibold text-dark hover:bg-gray-1 disabled:opacity-50 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2">Test</button>
          <div className="ml-auto flex flex-wrap items-center gap-2">
            <select value={scope} onChange={(e) => setScope(e.target.value)} className={selectCls}>
              <option value="all">Tümü</option>
              <option value="customers">Müşteriler</option>
              <option value="orders">Siparişler</option>
              <option value="products">Ürünler</option>
              <option value="both">Müşteri + Sipariş</option>
            </select>
            <div className="flex items-center gap-1"><span className="text-[10px] text-body-color dark:text-dark-6">Page</span><input type="number" value={pageSize} onChange={(e) => setPageSize(Number(e.target.value || 50))} className="w-16 rounded-lg border border-stroke bg-transparent px-2 py-2 text-xs outline-none dark:border-dark-3 dark:text-white" /></div>
            <div className="flex items-center gap-1"><span className="text-[10px] text-body-color dark:text-dark-6">Max</span><input type="number" value={maxPages} onChange={(e) => setMaxPages(Number(e.target.value || 100))} className="w-16 rounded-lg border border-stroke bg-transparent px-2 py-2 text-xs outline-none dark:border-dark-3 dark:text-white" /></div>
            <button onClick={startSync} disabled={!connectionId || polling || anyRunning} className={`rounded-lg px-5 py-2.5 text-sm font-semibold text-white shadow-sm ${polling || anyRunning ? "bg-yellow-500" : "bg-green-500 hover:opacity-90"} disabled:opacity-50`}>{polling || anyRunning ? "⟳ Sync Çalışıyor..." : "▶ Sync Başlat"}</button>
          </div>
        </div>

        {batchId && (
          <div className="mt-5 rounded-lg border border-stroke p-4 dark:border-dark-3">
            <div className="mb-3 flex items-center justify-between">
              <div className="text-sm text-dark dark:text-white">Batch: <span className="font-mono text-xs text-body-color dark:text-dark-6">{batchId.slice(0, 16)}…</span></div>
              <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${polling ? "bg-blue-light-5 text-blue-dark dark:bg-blue/10 dark:text-blue-light" : "bg-gray-2 text-dark-5 dark:bg-dark-3 dark:text-dark-6"}`}>{polling ? "⟳ Polling" : "Idle"}</span>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full table-auto"><thead><tr className="border-b border-stroke dark:border-dark-3">
                <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Kind</th>
                <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Status</th>
                <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">İşlenen</th>
                <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Başlangıç</th>
                <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Bitiş</th>
                <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Hata</th>
              </tr></thead><tbody className="divide-y divide-stroke dark:divide-dark-3">
                {(batch?.jobs || []).map((j) => { const sb = statusBadge[j.status] || statusBadge.Queued; return (
                  <tr key={j.jobId} className="text-xs hover:bg-gray-1 dark:hover:bg-dark-2">
                    <td className="px-3 py-2 font-medium text-dark dark:text-white">{j.kind}</td>
                    <td className="px-3 py-2"><span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold ${sb.color}`}>{sb.icon} {j.status}</span></td>
                    <td className="px-3 py-2 text-dark dark:text-white">{j.processedItems}</td>
                    <td className="px-3 py-2 text-body-color dark:text-dark-6">{fmtDate(j.startedAtUtc)}</td>
                    <td className="px-3 py-2 text-body-color dark:text-dark-6">{fmtDate(j.finishedAtUtc)}</td>
                    <td className="max-w-[150px] truncate px-3 py-2 text-red-500">{j.lastError || "-"}</td>
                  </tr>); })}
              </tbody></table>
            </div>
          </div>
        )}

        <div className="mt-5">
          <h4 className="mb-2 text-sm font-semibold text-dark dark:text-white">📋 Log</h4>
          <pre className="max-h-48 overflow-auto whitespace-pre-wrap rounded-lg border border-stroke bg-gray-1/30 p-3 text-xs text-dark dark:border-dark-3 dark:bg-dark-2/30 dark:text-white">{log || "Logs..."}</pre>
        </div>
      </div>
    </div>
  );
}