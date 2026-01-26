"use client";

import React, { useEffect, useMemo, useRef, useState } from "react";

type SyncScope = "customers" | "orders" | "both";

type StartSyncResponse = {
  batchId: string;
  jobs: { jobId: string; kind: string }[];
};

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

type BatchResponse = {
  batchId: string;
  jobs: IntegrationJobDto[];
};

function normalizeStartSync(payload: any): StartSyncResponse {
  // Backend PascalCase olabilir: BatchId, Jobs, JobId, Kind
  const batchId = payload?.batchId ?? payload?.BatchId ?? "";
  const jobsRaw = payload?.jobs ?? payload?.Jobs ?? [];
  const jobs = Array.isArray(jobsRaw)
    ? jobsRaw.map((x: any) => ({
        jobId: x?.jobId ?? x?.JobId ?? "",
        kind: x?.kind ?? x?.Kind ?? "",
      }))
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

export default function IkasIntegrationPage() {
  const [storeLabel, setStoreLabel] = useState("");
  const [storeDomain, setStoreDomain] = useState("");
  const [accessToken, setAccessToken] = useState("");

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

  const anyRunning = useMemo(() => {
    const jobs = batch?.jobs || [];
    return jobs.some((j) => j.status === "Queued" || j.status === "Running");
  }, [batch]);

  const connect = async () => {
    setLog("");
    setBatchId(null);
    setBatch(null);

    const res = await fetch("/api/integrations/ikas/connect", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ storeLabel, storeDomain, accessToken }),
    });

    const payload = await res.json().catch(() => null);

    if (!res.ok) {
      append(`CONNECT ERROR: ${payload?.message || JSON.stringify(payload)}`);
      return;
    }

    setConnectionId(payload.connectionId ?? payload.ConnectionId ?? null);
    append(`Connected. connectionId=${payload.connectionId ?? payload.ConnectionId}`);
  };

  const test = async () => {
    if (!connectionId) return;

    const res = await fetch("/api/integrations/ikas/test", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ connectionId }),
    });

    const payload = await res.json().catch(() => null);

    if (!res.ok) {
      append(`TEST ERROR: ${payload?.message || JSON.stringify(payload)}`);
      return;
    }

    append(`Test OK. meId=${payload.meId ?? payload.MeId}`);
  };

  const startSync = async () => {
    if (!connectionId) return;

    setBatchId(null);
    setBatch(null);

    const res = await fetch("/api/integrations/ikas/sync/start", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        connectionId,
        scope,
        pageSize,
        maxPages,
      }),
    });

    const payload = await res.json().catch(() => null);

    if (!res.ok) {
      append(`START SYNC ERROR: ${payload?.message || JSON.stringify(payload)}`);
      return;
    }

    const r = normalizeStartSync(payload);

    if (!r.batchId) {
      append(`START SYNC ERROR: batchId missing. payload=${JSON.stringify(payload)}`);
      return;
    }

    setBatchId(r.batchId);
    append(`Sync started. batchId=${r.batchId}`);

    if (r.jobs?.length) {
      append(`Jobs: ${r.jobs.map((j) => `${j.kind}:${j.jobId}`).join(", ")}`);
    } else {
      append("Jobs: (empty)");
    }

    setPolling(true);
  };

  const fetchBatch = async (id: string) => {
    const res = await fetch(`/api/integrations/ikas/jobs/batch/${id}`, {
      method: "GET",
      cache: "no-store",
    });

    const payload = await res.json().catch(() => null);

    if (!res.ok) {
      append(`POLL ERROR: ${payload?.message || JSON.stringify(payload)}`);
      return null;
    }

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

    pollTimer.current = window.setInterval(() => {
      tick();
    }, 2000);

    return () => {
      cancelled = true;
      if (pollTimer.current) {
        window.clearInterval(pollTimer.current);
        pollTimer.current = null;
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [polling, batchId]);

  return (
    <div className="p-4 sm:p-6">
      <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h2 className="mb-4 text-lg font-semibold text-dark dark:text-white">Ikas Integration</h2>

        <div className="grid gap-3 md:grid-cols-2">
          <div>
            <label className="text-sm text-body-color dark:text-dark-6">Store Label</label>
            <input
              className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={storeLabel}
              onChange={(e) => setStoreLabel(e.target.value)}
              placeholder="Örn: profiqo"
            />
          </div>

          <div>
            <label className="text-sm text-body-color dark:text-dark-6">Store Domain (opsiyonel)</label>
            <input
              className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={storeDomain}
              onChange={(e) => setStoreDomain(e.target.value)}
              placeholder="Örn: https://www.example.com"
            />
          </div>

          <div className="md:col-span-2">
            <label className="text-sm text-body-color dark:text-dark-6">Access Token</label>
            <textarea
              className="min-h-[120px] w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
              value={accessToken}
              onChange={(e) => setAccessToken(e.target.value)}
              placeholder="Bearer token"
            />
          </div>
        </div>

        <div className="mt-4 flex flex-wrap gap-2">
          <button className="rounded-lg bg-primary px-4 py-2 font-medium text-white hover:bg-opacity-90" onClick={connect}>
            Connect
          </button>

          <button
            className="rounded-lg border border-stroke px-4 py-2 text-dark dark:border-dark-3 dark:text-white"
            onClick={test}
            disabled={!connectionId}
          >
            Test Connection
          </button>

          <div className="ml-auto flex items-center gap-2">
            <label className="text-sm text-body-color dark:text-dark-6">Scope</label>
            <select
              value={scope}
              onChange={(e) => setScope(e.target.value as SyncScope)}
              className="rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm text-dark outline-none dark:border-dark-3 dark:text-white"
            >
              <option value="both">Both</option>
              <option value="customers">Customers</option>
              <option value="orders">Orders</option>
            </select>

            <label className="text-sm text-body-color dark:text-dark-6">PageSize</label>
            <input
              type="number"
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value || 50))}
              className="w-[90px] rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm text-dark outline-none dark:border-dark-3 dark:text-white"
            />

            <label className="text-sm text-body-color dark:text-dark-6">MaxPages</label>
            <input
              type="number"
              value={maxPages}
              onChange={(e) => setMaxPages(Number(e.target.value || 20))}
              className="w-[90px] rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm text-dark outline-none dark:border-dark-3 dark:text-white"
            />

            <button
              className="rounded-lg border border-stroke px-4 py-2 text-dark dark:border-dark-3 dark:text-white"
              onClick={startSync}
              disabled={!connectionId || polling || anyRunning}
            >
              {polling || anyRunning ? "Sync Running..." : "Start Sync"}
            </button>
          </div>
        </div>

        {batchId && (
          <div className="mt-4 rounded-lg border border-stroke p-3 dark:border-dark-3">
            <div className="mb-2 flex items-center justify-between">
              <div className="text-sm text-dark dark:text-white">
                Batch: <span className="font-mono text-xs">{batchId}</span>
              </div>
              <div className="text-sm text-body-color dark:text-dark-6">{polling ? "Polling..." : "Idle"}</div>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full table-auto">
                <thead>
                  <tr className="text-left text-xs text-body-color dark:text-dark-6">
                    <th className="px-2 py-2">Kind</th>
                    <th className="px-2 py-2">Status</th>
                    <th className="px-2 py-2">Processed</th>
                    <th className="px-2 py-2">Started</th>
                    <th className="px-2 py-2">Finished</th>
                    <th className="px-2 py-2">Error</th>
                  </tr>
                </thead>
                <tbody>
                  {(batch?.jobs || []).map((j) => (
                    <tr key={j.jobId} className="border-t border-stroke dark:border-dark-3 text-xs">
                      <td className="px-2 py-2">{j.kind}</td>
                      <td className="px-2 py-2">{j.status}</td>
                      <td className="px-2 py-2">{j.processedItems}</td>
                      <td className="px-2 py-2">{j.startedAtUtc ? new Date(j.startedAtUtc).toLocaleString() : "-"}</td>
                      <td className="px-2 py-2">{j.finishedAtUtc ? new Date(j.finishedAtUtc).toLocaleString() : "-"}</td>
                      <td className="px-2 py-2">{j.lastError || "-"}</td>
                    </tr>
                  ))}
                  {!batch?.jobs?.length && (
                    <tr className="border-t border-stroke dark:border-dark-3 text-xs">
                      <td className="px-2 py-2" colSpan={6}>Jobs not loaded yet...</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        <pre className="mt-4 whitespace-pre-wrap rounded-lg border border-stroke bg-transparent p-3 text-xs text-dark dark:border-dark-3 dark:text-white">
          {log || "Logs..."}
        </pre>
      </div>
    </div>
  );
}
