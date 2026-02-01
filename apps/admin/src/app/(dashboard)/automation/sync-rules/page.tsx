"use client";

import Breadcrumb from "@/components/Breadcrumbs/Breadcrumb";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
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
  { label: "3 saatte 1", value: 180 },
  { label: "6 saatte 1", value: 360 },
  { label: "12 saatte 1", value: 720 },
  { label: "24 saatte 1 (günlük)", value: 1440 },
  { label: "Haftada 1", value: 10080 },
];

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
      (cJson.items ?? []).forEach((x: Connection) => (init[x.connectionId] = true));
      setSelected(init);
    } catch (e: any) {
      setErr(e?.message ?? "Load failed");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void load(); }, []);

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
      const res = await fetch(`/api/automation/sync/rules/${ruleId}/${action}`, { method: "POST" });
      if (!res.ok) throw new Error(await res.text());
      await load();
    } catch (e: any) {
      setErr(e?.message ?? "Update failed");
    }
  }

  return (
    <div className="space-y-6">
      <Breadcrumb pageName="Automation / Sync Rules" />

      <Card className="p-6">
        <div className="text-lg font-semibold">Yeni Sync Rule</div>

        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <label className="text-sm">
            Rule adı
            <input
              className="mt-1 w-full rounded-md border border-gray-200 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </label>

          <label className="text-sm">
            Periyot
            <select
              className="mt-1 w-full rounded-md border border-gray-200 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
              value={intervalMinutes}
              onChange={(e) => setIntervalMinutes(Number(e.target.value))}
            >
              {intervalOptions.map((o) => (
                <option key={o.value} value={o.value}>{o.label}</option>
              ))}
            </select>
          </label>

          <label className="text-sm">
            PageSize
            <input
              type="number"
              min={10}
              max={500}
              className="mt-1 w-full rounded-md border border-gray-200 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value))}
            />
          </label>

          <label className="text-sm">
            MaxPages
            <input
              type="number"
              min={1}
              max={500}
              className="mt-1 w-full rounded-md border border-gray-200 bg-white px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900"
              value={maxPages}
              onChange={(e) => setMaxPages(Number(e.target.value))}
            />
          </label>
        </div>

        <div className="mt-4 text-sm font-medium">Çalışacak bağlantılar</div>
        <div className="mt-2 grid gap-2 md:grid-cols-2">
          {connections.map((c) => (
            <label key={c.connectionId} className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={!!selected[c.connectionId]}
                onChange={(e) => setSelected((p) => ({ ...p, [c.connectionId]: e.target.checked }))}
              />
              <span className="font-medium">{c.displayName}</span>
              <span className="text-gray-500 dark:text-gray-400">({c.providerType}, {c.status})</span>
            </label>
          ))}
        </div>

        <div className="mt-4">
          <Button onClick={createRule} disabled={selectedIds.length === 0}>Rule Oluştur</Button>
        </div>

        {err && <div className="mt-3 text-sm text-red-600">{err}</div>}
        {info && <div className="mt-3 text-sm text-green-600">{info}</div>}
      </Card>

      <Card className="p-6">
        <div className="flex items-baseline justify-between">
          <div className="text-lg font-semibold">Mevcut Rule’lar</div>
          {loading && <div className="text-sm text-gray-500">Yükleniyor…</div>}
        </div>

        <div className="mt-4 space-y-3">
          {rules.map((r) => (
            <div key={r.id} className="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
              <div className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
                <div>
                  <div className="font-semibold">{r.name}</div>
                  <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">
                    Status: {r.status}, Interval: {r.intervalMinutes} dk, Next: {new Date(r.nextRunAtUtc).toLocaleString()}
                  </div>
                  <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">
                    Connections: {r.connectionIds.length}
                  </div>
                </div>

                <div className="flex gap-2">
                  {r.status === "active" ? (
                    <Button variant="destructive" onClick={() => toggle(r.id, "pause")}>Pause</Button>
                  ) : (
                    <Button onClick={() => toggle(r.id, "activate")}>Activate</Button>
                  )}
                </div>
              </div>
            </div>
          ))}

          {rules.length === 0 && (
            <div className="text-sm text-gray-600 dark:text-gray-300">Henüz rule yok.</div>
          )}
        </div>
      </Card>
    </div>
  );
}
