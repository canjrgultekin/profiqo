"use client";

import React, { useEffect, useState } from "react";

type DispatchDto = {
  id: string;
  jobId: string;
  ruleId: string;
  customerId: string;
  toE164: string;
  messageNo: number;
  templateId: string;
  plannedAtUtc: string;
  localDate: string;
  status: string;
  attemptCount: number;
  nextAttemptAtUtc: string;
  sentAtUtc?: string | null;
  lastError?: string | null;
};

export default function WhatsappDispatchPage() {
  const [items, setItems] = useState<DispatchDto[]>([]);
  const [err, setErr] = useState<string | null>(null);

  const [take, setTake] = useState(100);

  const load = async () => {
    setErr(null);
    const res = await fetch(`/api/whatsapp/dispatch/recent?take=${take}`, { cache: "no-store" });
    const payload = await res.json().catch(() => null);
    if (!res.ok) {
      setErr(payload?.message || "Failed to load dispatch.");
      setItems([]);
      return;
    }
    setItems(payload?.items || []);
  };

  useEffect(() => {
    load();
  }, []);

  // Manual enqueue (test)
  const [jobId, setJobId] = useState("");
  const [ruleId, setRuleId] = useState("");
  const [customerId, setCustomerId] = useState("");
  const [toE164, setToE164] = useState("+905xxxxxxxxx");
  const [messageNo, setMessageNo] = useState(1);
  const [templateId, setTemplateId] = useState("");
  const [plannedAtUtc, setPlannedAtUtc] = useState(() => new Date().toISOString());
  const [payloadJson, setPayloadJson] = useState(`{"kind":"manual"}`);

  const manualEnqueue = async () => {
    const res = await fetch(`/api/whatsapp/dispatch/manual-enqueue`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        jobId,
        ruleId,
        customerId,
        toE164,
        messageNo,
        templateId,
        plannedAtUtc,
        payloadJson,
      }),
    });
    const p = await res.json().catch(() => null);
    if (!res.ok) return alert(p?.message || "enqueue failed");
    alert(`enqueued: ${p?.id}`);
    await load();
  };

  // Order event simulate
  const [orderId, setOrderId] = useState("ORD-TEST-1");
  const [oeCustomerId, setOeCustomerId] = useState("");
  const [oeToE164, setOeToE164] = useState("+905xxxxxxxxx");

  const simulateOrder = async () => {
    const res = await fetch(`/api/whatsapp/order-events`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        orderId,
        customerId: oeCustomerId,
        toE164: oeToE164,
      }),
    });
    const p = await res.json().catch(() => null);
    if (!res.ok) return alert(p?.message || "order event failed");
    alert(`order event created: ${p?.id}`);
  };

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4 flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold text-dark dark:text-white">WhatsApp Dispatch</h2>
          <p className="text-sm text-body-color dark:text-dark-6">Queue ve dummy send status burada görünür.</p>
        </div>
        <button onClick={load}
          className="rounded bg-dark px-4 py-2 text-sm font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark">
          Refresh
        </button>
      </div>

      {err && (
        <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-600 dark:text-red-400">
          {err}
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-3 text-lg font-semibold text-dark dark:text-white">Manual Enqueue (test)</h3>

          <div className="grid gap-3">
            <input className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
              value={jobId} onChange={(e) => setJobId(e.target.value)} placeholder="jobId (uuid)" />
            <input className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
              value={ruleId} onChange={(e) => setRuleId(e.target.value)} placeholder="ruleId (uuid)" />
            <input className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
              value={customerId} onChange={(e) => setCustomerId(e.target.value)} placeholder="customerId (uuid)" />
            <input className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
              value={toE164} onChange={(e) => setToE164(e.target.value)} placeholder="+90..." />

            <div className="grid gap-3 md:grid-cols-2">
              <input type="number"
                className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={messageNo} onChange={(e) => setMessageNo(Number(e.target.value))} placeholder="messageNo" />
              <input className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={templateId} onChange={(e) => setTemplateId(e.target.value)} placeholder="templateId (uuid)" />
            </div>

            <input className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
              value={plannedAtUtc} onChange={(e) => setPlannedAtUtc(e.target.value)} placeholder="plannedAtUtc ISO" />

            <textarea className="h-28 rounded border border-stroke bg-transparent px-3 py-2 font-mono text-xs dark:border-dark-3 dark:text-white"
              value={payloadJson} onChange={(e) => setPayloadJson(e.target.value)} />

            <button onClick={manualEnqueue}
              className="w-fit rounded bg-primary px-4 py-2 text-sm font-semibold text-white hover:opacity-90">
              Enqueue
            </button>
          </div>
        </div>

        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-3 text-lg font-semibold text-dark dark:text-white">Order Event Simulator</h3>

          <div className="grid gap-3">
            <input className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
              value={orderId} onChange={(e) => setOrderId(e.target.value)} placeholder="orderId" />
            <input className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
              value={oeCustomerId} onChange={(e) => setOeCustomerId(e.target.value)} placeholder="customerId (uuid)" />
            <input className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
              value={oeToE164} onChange={(e) => setOeToE164(e.target.value)} placeholder="+90..." />

            <button onClick={simulateOrder}
              className="w-fit rounded bg-primary px-4 py-2 text-sm font-semibold text-white hover:opacity-90">
              Create Order Event
            </button>

            <div className="text-xs text-body-color dark:text-dark-6">
              Worker order-event rules için otomatik dispatch üretir.
            </div>
          </div>
        </div>
      </div>

      <div className="mt-4 rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <div className="mb-3 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-dark dark:text-white">Recent Dispatch</h3>
          <div className="flex items-center gap-2">
            <input type="number"
              className="w-20 rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
              value={take} onChange={(e) => setTake(Number(e.target.value))} />
            <button onClick={load}
              className="rounded bg-dark px-3 py-2 text-sm font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark">
              Load
            </button>
          </div>
        </div>

        {items.length === 0 ? (
          <div className="text-sm text-body-color dark:text-dark-6">No dispatch items.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full table-auto">
              <thead>
                <tr className="text-left text-xs text-body-color dark:text-dark-6">
                  <th className="px-2 py-2">Status</th>
                  <th className="px-2 py-2">To</th>
                  <th className="px-2 py-2">MsgNo</th>
                  <th className="px-2 py-2">Planned</th>
                  <th className="px-2 py-2">Sent</th>
                  <th className="px-2 py-2">Err</th>
                </tr>
              </thead>
              <tbody>
                {items.map((x) => (
                  <tr key={x.id} className="border-t border-stroke dark:border-dark-3 text-sm">
                    <td className="px-2 py-2">{x.status}</td>
                    <td className="px-2 py-2">{x.toE164}</td>
                    <td className="px-2 py-2">{x.messageNo}</td>
                    <td className="px-2 py-2 text-xs">{x.plannedAtUtc}</td>
                    <td className="px-2 py-2 text-xs">{x.sentAtUtc || "-"}</td>
                    <td className="px-2 py-2 text-xs text-red-500">{x.lastError || "-"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
