// Path: apps/admin/src/app/integrations/trendyol/page.tsx
"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";

type StartSyncResponse = { batchId: string; jobs: { jobId: string; kind: string }[] };
type JobStatus = "Queued" | "Running" | "Succeeded" | "Failed" | "Cancelled" | string;

type IntegrationJobDto = {
  jobId: string;
  batchId: string;
  tenantId: string;
  connectionId: string;
  kind: string;
  status: JobStatus;
  pageSize: number;
  maxPages: number;
  processedItems: number;
  createdAtUtc: string;
  startedAtUtc?: string | null;
  finishedAtUtc?: string | null;
  lastError?: string | null;
};

type BatchResponse = { batchId: string; jobs: IntegrationJobDto[] };

function normalizeStartSync(payload: any): StartSyncResponse {
  const batchId = payload?.batchId ?? payload?.BatchId ?? "";
  const jobsRaw = payload?.jobs ?? payload?.Jobs ?? [];
  const jobs = Array.isArray(jobsRaw)
    ? jobsRaw.map((x: any) => ({ jobId: x?.jobId ?? x?.JobId ?? "", kind: x?.kind ?? x?.Kind ?? "" }))
    : [];
  return { batchId, jobs };
}

function normalizeBatch(payload: any): BatchResponse {
  const batchId = payload?.batchId ?? payload?.BatchId ?? "";
  const jobsRaw = payload?.jobs ?? payload?.Jobs ?? [];
  const jobs: IntegrationJobDto[] = Array.isArray(jobsRaw)
    ? jobsRaw.map((j: any) => ({
        jobId: j?.jobId ?? j?.JobId ?? "",
        batchId: j?.batchId ?? j?.BatchId ?? batchId,
        tenantId: j?.tenantId ?? j?.TenantId ?? "",
        connectionId: j?.connectionId ?? j?.ConnectionId ?? "",
        kind: String(j?.kind ?? j?.Kind ?? ""),
        status: String(j?.status ?? j?.Status ?? ""),
        pageSize: Number(j?.pageSize ?? j?.PageSize ?? 0),
        maxPages: Number(j?.maxPages ?? j?.MaxPages ?? 0),
        processedItems: Number(j?.processedItems ?? j?.ProcessedItems ?? 0),
        createdAtUtc: String(j?.createdAtUtc ?? j?.CreatedAtUtc ?? ""),
        startedAtUtc: j?.startedAtUtc ?? j?.StartedAtUtc ?? null,
        finishedAtUtc: j?.finishedAtUtc ?? j?.FinishedAtUtc ?? null,
        lastError: j?.lastError ?? j?.LastError ?? null,
      }))
    : [];
  return { batchId, jobs };
}

export default function TrendyolIntegrationPage() {
  const [displayName, setDisplayName] = useState("Trendyol");
  const [sellerId, setSellerId] = useState("");
  const [apiKey, setApiKey] = useState("");
  const [apiSecret, setApiSecret] = useState("");
  const [userAgent, setUserAgent] = useState("");

  const [hasExisting, setHasExisting] = useState(false);
  const [connectionId, setConnectionId] = useState<string | null>(null);

  const [log, setLog] = useState("");
  const append = (s: string) => setLog((x) => (x ? x + "\n" + s : s));

  const [batchId, setBatchId] = useState<string | null>(null);
  const [batch, setBatch] = useState<BatchResponse | null>(null);
  const [polling, setPolling] = useState(false);
  const pollTimer = useRef<number | null>(null);

  const [pageSize, setPageSize] = useState(200);
  const [maxPages, setMaxPages] = useState(20);

  const anyRunning = useMemo(() => (batch?.jobs || []).some((j) => j.status === "Queued" || j.status === "Running"), [batch]);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      const res = await fetch("/api/integrations/trendyol/connection", { method: "GET", cache: "no-store" });
      const payload = await res.json().catch(() => null);

      if (cancelled) return;
      if (!res.ok) return append(`LOAD CONNECTION ERROR: ${payload?.message || JSON.stringify(payload)}`);

      const hasConnection = Boolean(payload?.hasConnection ?? payload?.HasConnection);
      if (!hasConnection) {
        setHasExisting(false);
        setConnectionId(null);
        return;
      }

      setHasExisting(true);
      const cid = payload?.connectionId ?? payload?.ConnectionId;
      setConnectionId(cid);

      setDisplayName(payload?.displayName ?? payload?.DisplayName ?? "Trendyol");
      setSellerId(payload?.sellerId ?? payload?.SellerId ?? payload?.externalAccountId ?? "");

      setApiKey("");
      setApiSecret("");
      setUserAgent("");

      append(`Existing Trendyol connection loaded. connectionId=${cid}`);
    };

    load();
    return () => {
      cancelled = true;
    };
  }, []);

  const connectOrUpdate = async () => {
    setBatchId(null);
    setBatch(null);

    if (!displayName.trim()) return append("CONNECT ERROR: DisplayName zorunlu.");
    if (!sellerId.trim()) return append("CONNECT ERROR: SellerId zorunlu.");

    if (!hasExisting && (!apiKey.trim() || !apiSecret.trim()))
      return append("CONNECT ERROR: ApiKey/ApiSecret zorunlu.");

    if (hasExisting && (!apiKey.trim() || !apiSecret.trim()))
      return append("UPDATE ERROR: Mevcut connection var. ApiKey/ApiSecret girerek güncelle.");

    const res = await fetch("/api/integrations/trendyol/connect", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ displayName, sellerId, apiKey, apiSecret, userAgent }),
    });

    const payload = await res.json().catch(() => null);
    if (!res.ok) return append(`CONNECT/UPDATE ERROR: ${payload?.message || JSON.stringify(payload)}`);

    const cid = payload?.connectionId ?? payload?.ConnectionId;
    setConnectionId(cid);
    setHasExisting(true);

    setApiKey("");
    setApiSecret("");
    setUserAgent("");

    append(`${hasExisting ? "Updated" : "Connected"}. connectionId=${cid}`);
  };

  const test = async () => {
    if (!connectionId) return;

    const res = await fetch("/api/integrations/trendyol/test", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ connectionId }),
    });

    const payload = await res.json().catch(() => null);
    if (!res.ok) return append(`TEST ERROR: ${payload?.message || JSON.stringify(payload)}`);

    append("Test OK.");
  };

  const startSync = async () => {
    if (!connectionId) return;

    setBatchId(null);
    setBatch(null);

    const res = await fetch("/api/integrations/trendyol/sync/start", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ connectionId, pageSize, maxPages }),
    });

    const payload = await res.json().catch(() => null);
    if (!res.ok) return append(`START SYNC ERROR: ${payload?.message || JSON.stringify(payload)}`);

    const r = normalizeStartSync(payload);
    setBatchId(r.batchId);
    append(`Sync started. batchId=${r.batchId}`);
    append(`Jobs: ${r.jobs.map((j) => `${j.kind}:${j.jobId}`).join(", ")}`);
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

    const tick = async () => {
      if (cancelled) return;
      const b = await fetchBatch(batchId);
      if (b) {
        setBatch(b);
        const running = (b.jobs || []).some((j) => j.status === "Queued" || j.status === "Running");
        if (!running) {
          setPolling(false);
          append(`Batch finished. batchId=${batchId}`);
        }
      }
    };

    tick();
    pollTimer.current = window.setInterval(() => tick(), 2000);

    return () => {
      cancelled = true;
      if (pollTimer.current) {
        window.clearInterval(pollTimer.current);
        pollTimer.current = null;
      }
    };
  }, [polling, batchId]);

  return (
    <div className="p-4 sm:p-6">
      <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h2 className="mb-4 text-lg font-semibold text-dark dark:text-white">Trendyol Integration (PROD)</h2>

        <div className="grid gap-3 md:grid-cols-2">
          <div>
            <label className="text-sm text-body-color dark:text-dark-6">Display Name</label>
            <input className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
          </div>

          <div>
            <label className="text-sm text-body-color dark:text-dark-6">SellerId</label>
            <input className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={sellerId} onChange={(e) => setSellerId(e.target.value)} />
          </div>

          <div>
            <label className="text-sm text-body-color dark:text-dark-6">API Key {hasExisting ? "(update)" : ""}</label>
            <input className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={apiKey} onChange={(e) => setApiKey(e.target.value)} />
          </div>

          <div>
            <label className="text-sm text-body-color dark:text-dark-6">API Secret {hasExisting ? "(update)" : ""}</label>
            <input className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={apiSecret} onChange={(e) => setApiSecret(e.target.value)} />
          </div>

          <div className="md:col-span-2">
            <label className="text-sm text-body-color dark:text-dark-6">User-Agent (opsiyonel)</label>
            <input className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={userAgent} onChange={(e) => setUserAgent(e.target.value)} placeholder="Boş bırakırsan ProfiQo/{sellerId}" />
          </div>
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          <button className="rounded-lg bg-primary px-4 py-2 font-medium text-white hover:bg-opacity-90" onClick={connectOrUpdate}>
            {hasExisting ? "Update Credentials" : "Connect"}
          </button>

          <button className="rounded-lg border border-stroke px-4 py-2 text-dark dark:border-dark-3 dark:text-white" onClick={test} disabled={!connectionId}>
            Test Connection
          </button>

          <div className="ml-auto flex items-center gap-2">
            <label className="text-sm text-body-color dark:text-dark-6">PageSize</label>
            <input type="number" value={pageSize} onChange={(e) => setPageSize(Number(e.target.value || 200))}
              className="w-[90px] rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm text-dark outline-none dark:border-dark-3 dark:text-white" />

            <label className="text-sm text-body-color dark:text-dark-6">MaxPages</label>
            <input type="number" value={maxPages} onChange={(e) => setMaxPages(Number(e.target.value || 20))}
              className="w-[90px] rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm text-dark outline-none dark:border-dark-3 dark:text-white" />

            <button className="rounded-lg border border-stroke px-4 py-2 text-dark dark:border-dark-3 dark:text-white"
              onClick={startSync} disabled={!connectionId || polling || anyRunning}>
              {polling || anyRunning ? "Sync Running..." : "Start Sync"}
            </button>
          </div>
        </div>

        <pre className="mt-4 whitespace-pre-wrap rounded-lg border border-stroke bg-transparent p-3 text-xs text-dark dark:border-dark-3 dark:text-white">
          {log || "Logs..."}
        </pre>
      </div>
    </div>
  );
}
