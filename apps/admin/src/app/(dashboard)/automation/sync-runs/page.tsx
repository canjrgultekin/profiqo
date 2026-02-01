"use client";

import Breadcrumb from "@/components/Breadcrumbs/Breadcrumb";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
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

export default function SyncRunsPage() {
  const [runs, setRuns] = useState<RunRow[]>([]);
  const [detail, setDetail] = useState<RunDetail | null>(null);
  const [err, setErr] = useState<string | null>(null);

  async function load() {
    setErr(null);
    const res = await fetch("/api/automation/sync/runs?take=50", { cache: "no-store" });
    if (!res.ok) { setErr(await res.text()); return; }
    const json = await res.json();
    setRuns(json.items ?? []);
  }

  async function open(batchId: string) {
    setErr(null);
    const res = await fetch(`/api/automation/sync/runs/${batchId}`, { cache: "no-store" });
    if (!res.ok) { setErr(await res.text()); return; }
    const json = await res.json();
    setDetail(json);
  }

  useEffect(() => { void load(); }, []);

  return (
    <div className="space-y-6">
      <Breadcrumb pageName="Automation / Sync Runs" />

      {err && <Card className="p-4 text-red-600">{err}</Card>}

      <Card className="p-6">
        <div className="flex items-center justify-between">
          <div className="text-lg font-semibold">Run List</div>
          <Button onClick={load}>Refresh</Button>
        </div>

        <div className="mt-4 space-y-3">
          {runs.map((r) => (
            <div key={r.batchId} className="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
              <div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
                <div>
                  <div className="font-semibold">Batch: {r.batchId}</div>
                  <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">
                    {new Date(r.scheduledAtUtc).toLocaleString()} | Status: {r.status} | Jobs: {r.totalJobs}
                  </div>
                  {r.lastError && (
                    <div className="mt-2 text-sm text-red-600">{r.lastError}</div>
                  )}
                </div>
                <Button onClick={() => open(r.batchId)}>Detay</Button>
              </div>
            </div>
          ))}
          {runs.length === 0 && <div className="text-sm text-gray-600 dark:text-gray-300">Henüz run yok.</div>}
        </div>
      </Card>

      {detail && (
        <Card className="p-6">
          <div className="text-lg font-semibold">Run Detail</div>
          <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">
            Batch: {detail.batchId} | {new Date(detail.scheduledAtUtc).toLocaleString()}
          </div>

          <div className="mt-4 overflow-x-auto">
            <table className="w-full table-auto text-sm">
              <thead>
                <tr className="text-left text-xs text-gray-500">
                  <th className="px-2 py-2">Kind</th>
                  <th className="px-2 py-2">Status</th>
                  <th className="px-2 py-2">Connection</th>
                  <th className="px-2 py-2">Processed</th>
                  <th className="px-2 py-2">Error</th>
                </tr>
              </thead>
              <tbody>
                {detail.jobs.map((j) => (
                  <tr key={j.jobId} className="border-t border-gray-200 dark:border-gray-700">
                    <td className="px-2 py-2">{j.kind}</td>
                    <td className="px-2 py-2">{j.status}</td>
                    <td className="px-2 py-2">{j.connectionId}</td>
                    <td className="px-2 py-2">{j.processedItems}</td>
                    <td className="px-2 py-2 text-red-600">{j.lastError ?? ""}</td>
                  </tr>
                ))}
                {detail.jobs.length === 0 && (
                  <tr className="border-t border-gray-200 dark:border-gray-700">
                    <td className="px-2 py-3" colSpan={5}>Job yok</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </Card>
      )}
    </div>
  );
}
