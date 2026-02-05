"use client";

import React, { useEffect, useState } from "react";

type DispatchDto = {
  id: string; jobId: string; ruleId: string; customerId: string; toE164: string; messageNo: number; templateId: string;
  plannedAtUtc: string; localDate: string; status: string; attemptCount: number; nextAttemptAtUtc: string;
  sentAtUtc?: string | null; lastError?: string | null;
};

const statusConfig: Record<string, { label: string; color: string }> = {
  "1": { label: "Kuyrukta", color: "bg-blue-100 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400" },
  "2": { label: "Çalışıyor", color: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/20 dark:text-yellow-400" },
  "3": { label: "Gönderildi", color: "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400" },
  "4": { label: "Engellendi", color: "bg-orange-100 text-orange-700 dark:bg-orange-900/20 dark:text-orange-400" },
  "5": { label: "Başarısız", color: "bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400" },
};

function fmtDate(s: string | null | undefined): string {
  if (!s) return "-";
  try { return new Date(s).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit", second: "2-digit" }); } catch { return s; }
}

function StatusBadge({ status }: { status: string }) {
  const cfg = statusConfig[status] || { label: status, color: "bg-gray-100 text-gray-600 dark:bg-dark-3 dark:text-dark-6" };
  return <span className={`inline-block rounded-full px-2 py-0.5 text-[10px] font-semibold ${cfg.color}`}>{cfg.label}</span>;
}

export default function WhatsappDispatchPage() {
  const [items, setItems] = useState<DispatchDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [take, setTake] = useState(100);

  const load = async () => {
    setErr(null);
    const res = await fetch(`/api/whatsapp/dispatch/recent?take=${take}`, { cache: "no-store" });
    const payload = await res.json().catch(() => null);
    if (!res.ok) { setErr(payload?.message || "Dispatch yüklenemedi."); setItems([]); return; }
    setItems(payload?.items || []);
  };

  useEffect(() => { load(); }, []);

  // Manual enqueue
  const [jobId, setJobId] = useState("");
  const [ruleId, setRuleId] = useState("");
  const [customerId, setCustomerId] = useState("");
  const [toE164, setToE164] = useState("+905xxxxxxxxx");
  const [messageNo, setMessageNo] = useState(1);
  const [templateId, setTemplateId] = useState("");
  const [plannedAtUtc, setPlannedAtUtc] = useState(() => new Date().toISOString());
  const [payloadJson, setPayloadJson] = useState(`{"kind":"manual"}`);

  const manualEnqueue = async () => {
    const res = await fetch(`/api/whatsapp/dispatch/manual-enqueue`, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ jobId, ruleId, customerId, toE164, messageNo, templateId, plannedAtUtc, payloadJson }) });
    const p = await res.json().catch(() => null);
    if (!res.ok) return alert(p?.message || "Enqueue başarısız");
    alert(`Eklendi: ${p?.id}`); await load();
  };

  // Order event
  const [orderId, setOrderId] = useState("ORD-TEST-1");
  const [oeCustomerId, setOeCustomerId] = useState("");
  const [oeToE164, setOeToE164] = useState("+905xxxxxxxxx");

  const simulateOrder = async () => {
    const res = await fetch(`/api/whatsapp/order-events`, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ orderId, customerId: oeCustomerId, toE164: oeToE164 }) });
    const p = await res.json().catch(() => null);
    if (!res.ok) return alert(p?.message || "Event oluşturulamadı");
    alert(`Order event: ${p?.id}`);
  };

  const inputCls = "w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white";

  return (
    <div className="p-4 sm:p-6">
      {/* Header */}
      <div className="mb-6 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10">
            <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" /></svg>
          </div>
          <div>
            <h2 className="text-xl font-bold text-dark dark:text-white">WhatsApp Dispatch</h2>
            <p className="text-sm text-body-color dark:text-dark-6">Kuyruk durumu, gönderim logları ve test araçları.</p>
          </div>
        </div>
        <button onClick={load} className="flex items-center gap-1.5 rounded-lg border border-stroke px-4 py-2.5 text-sm font-medium text-dark hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2">
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" /></svg>
          Yenile
        </button>
      </div>

      {err && (
        <div className="mb-4 flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 dark:border-red-900/30 dark:bg-red-900/10">
          <svg className="h-4 w-4 flex-shrink-0 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          <span className="text-sm text-red-600 dark:text-red-400">{err}</span>
        </div>
      )}

      {/* Test Tools */}
      <div className="mb-6 grid gap-6 lg:grid-cols-2">
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-4 flex items-center gap-2 text-base font-semibold text-dark dark:text-white">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-primary/10 text-xs">🧪</span>
            Manuel Enqueue
          </h3>
          <div className="grid gap-3">
            <input className={inputCls} value={jobId} onChange={(e) => setJobId(e.target.value)} placeholder="jobId (uuid)" />
            <input className={inputCls} value={ruleId} onChange={(e) => setRuleId(e.target.value)} placeholder="ruleId (uuid)" />
            <input className={inputCls} value={customerId} onChange={(e) => setCustomerId(e.target.value)} placeholder="customerId (uuid)" />
            <input className={inputCls} value={toE164} onChange={(e) => setToE164(e.target.value)} placeholder="+905xxxxxxxxx" />
            <div className="grid gap-3 md:grid-cols-2">
              <input type="number" className={inputCls} value={messageNo} onChange={(e) => setMessageNo(Number(e.target.value))} placeholder="messageNo" />
              <input className={inputCls} value={templateId} onChange={(e) => setTemplateId(e.target.value)} placeholder="templateId (uuid)" />
            </div>
            <input className={inputCls} value={plannedAtUtc} onChange={(e) => setPlannedAtUtc(e.target.value)} placeholder="plannedAtUtc ISO" />
            <textarea className="h-20 w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 font-mono text-xs outline-none focus:border-primary dark:border-dark-3 dark:text-white" value={payloadJson} onChange={(e) => setPayloadJson(e.target.value)} />
            <button onClick={manualEnqueue} className="w-fit rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:opacity-90">Kuyruğa Ekle</button>
          </div>
        </div>

        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-4 flex items-center gap-2 text-base font-semibold text-dark dark:text-white">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-blue-50 text-xs dark:bg-blue-900/20">📦</span>
            Order Event Simülatör
          </h3>
          <div className="grid gap-3">
            <input className={inputCls} value={orderId} onChange={(e) => setOrderId(e.target.value)} placeholder="orderId" />
            <input className={inputCls} value={oeCustomerId} onChange={(e) => setOeCustomerId(e.target.value)} placeholder="customerId (uuid)" />
            <input className={inputCls} value={oeToE164} onChange={(e) => setOeToE164(e.target.value)} placeholder="+905xxxxxxxxx" />
            <button onClick={simulateOrder} className="w-fit rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:opacity-90">Order Event Oluştur</button>
            <div className="rounded-lg bg-gray-1/50 px-3 py-2 text-xs text-body-color dark:bg-dark-2/30 dark:text-dark-6">
              💡 Worker, order-event kuralları için otomatik dispatch üretir.
            </div>
          </div>
        </div>
      </div>

      {/* Recent Dispatch Table */}
      <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-dark dark:text-white">Son Gönderimler</h3>
          <div className="flex items-center gap-2">
            <input type="number" className="w-20 rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white" value={take} onChange={(e) => setTake(Number(e.target.value))} />
            <button onClick={load} className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white hover:opacity-90">Yükle</button>
          </div>
        </div>

        {items.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-12 text-center">
            <svg className="mb-3 h-12 w-12 text-body-color/30 dark:text-dark-6/30" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}><path strokeLinecap="round" strokeLinejoin="round" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" /></svg>
            <p className="text-sm font-medium text-body-color dark:text-dark-6">Dispatch kaydı yok</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full table-auto">
              <thead>
                <tr className="border-b border-stroke dark:border-dark-3">
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Durum</th>
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Telefon</th>
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Msg#</th>
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Planlanan</th>
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Gönderildi</th>
                  <th className="px-3 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Hata</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-stroke dark:divide-dark-3">
                {items.map((x) => (
                  <tr key={x.id} className="text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2">
                    <td className="whitespace-nowrap px-3 py-2.5"><StatusBadge status={x.status} /></td>
                    <td className="whitespace-nowrap px-3 py-2.5 font-mono text-xs text-dark dark:text-white">{x.toE164}</td>
                    <td className="px-3 py-2.5 text-dark dark:text-white">{x.messageNo}</td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-xs text-body-color dark:text-dark-6">{fmtDate(x.plannedAtUtc)}</td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-xs text-body-color dark:text-dark-6">{fmtDate(x.sentAtUtc)}</td>
                    <td className="max-w-[200px] truncate px-3 py-2.5 text-xs text-red-500" title={x.lastError || undefined}>{x.lastError || "-"}</td>
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
