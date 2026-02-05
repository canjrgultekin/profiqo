"use client";

import React, { useEffect, useMemo, useState } from "react";

type RuleMode = 1 | 2;
type RuleDto = {
  id: string; tenantId: string; name: string; mode: RuleMode; dailyLimit: number; timezone: string;
  dailyTime1?: string | null; dailyTime2?: string | null; dailyDelay2Minutes?: number | null;
  orderDelay1Minutes?: number | null; orderDelay2Minutes?: number | null; isActive: boolean; updatedAtUtc: string;
};

function timeOnlyOrNull(v: string): string | null {
  const s = v.trim();
  if (!s) return null;
  return s.length === 5 ? `${s}:00` : s;
}

export default function WhatsappRulesPage() {
  const [items, setItems] = useState<RuleDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [editId, setEditId] = useState<string | null>(null);
  const [name, setName] = useState("Daily Promo");
  const [mode, setMode] = useState<RuleMode>(1);
  const [dailyLimit, setDailyLimit] = useState(2);
  const [timezone, setTimezone] = useState("Europe/Istanbul");
  const [dailyTime1, setDailyTime1] = useState("10:00");
  const [dailyTime2, setDailyTime2] = useState("");
  const [dailyDelay2Minutes, setDailyDelay2Minutes] = useState<number>(90);
  const [orderDelay1Minutes, setOrderDelay1Minutes] = useState<number>(10);
  const [orderDelay2Minutes, setOrderDelay2Minutes] = useState<number>(120);
  const [isActive, setIsActive] = useState(true);

  const resetForm = () => {
    setEditId(null); setName("Daily Promo"); setMode(1); setDailyLimit(2);
    setTimezone("Europe/Istanbul"); setDailyTime1("10:00"); setDailyTime2("");
    setDailyDelay2Minutes(90); setOrderDelay1Minutes(10); setOrderDelay2Minutes(120); setIsActive(true);
  };

  const load = async () => {
    setErr(null);
    const res = await fetch("/api/whatsapp/rules", { cache: "no-store" });
    const payload = await res.json().catch(() => null);
    if (!res.ok) { setErr(payload?.message || "Kurallar yüklenemedi."); setItems([]); return; }
    setItems(payload?.items || []);
  };

  useEffect(() => { load(); }, []);

  const save = async () => {
    const dto: any = {
      id: editId || "00000000-0000-0000-0000-000000000000",
      tenantId: "00000000-0000-0000-0000-000000000000",
      name: name.trim(), mode, dailyLimit: dailyLimit === 2 ? 2 : 1,
      timezone: timezone.trim() || "Europe/Istanbul",
      dailyTime1: mode === 1 ? timeOnlyOrNull(dailyTime1) : null,
      dailyTime2: mode === 1 ? timeOnlyOrNull(dailyTime2) : null,
      dailyDelay2Minutes: mode === 1 && dailyLimit === 2 ? Number(dailyDelay2Minutes || 0) : null,
      orderDelay1Minutes: mode === 2 ? Number(orderDelay1Minutes || 0) : null,
      orderDelay2Minutes: mode === 2 && dailyLimit === 2 ? Number(orderDelay2Minutes || 0) : null,
      isActive, createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString(),
    };
    const res = await fetch("/api/whatsapp/rules", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(dto) });
    const payload = await res.json().catch(() => null);
    if (!res.ok) { alert(payload?.message || "Kayıt başarısız"); return; }
    resetForm(); await load();
  };

  const edit = (r: RuleDto) => {
    setEditId(r.id); setName(r.name); setMode(r.mode); setDailyLimit(r.dailyLimit);
    setTimezone(r.timezone || "Europe/Istanbul"); setDailyTime1((r.dailyTime1 || "10:00").slice(0, 5));
    setDailyTime2((r.dailyTime2 || "").slice(0, 5)); setDailyDelay2Minutes(r.dailyDelay2Minutes ?? 90);
    setOrderDelay1Minutes(r.orderDelay1Minutes ?? 10); setOrderDelay2Minutes(r.orderDelay2Minutes ?? 120);
    setIsActive(Boolean(r.isActive));
  };

  const del = async (id: string) => {
    if (!confirm("Bu kural silinsin mi?")) return;
    const res = await fetch(`/api/whatsapp/rules/${id}`, { method: "DELETE" });
    if (!res.ok) { const p = await res.json().catch(() => null); alert(p?.message || "Silme başarısız"); return; }
    await load();
  };

  const modeLabel = useMemo(() => (mode === 1 ? "Daily" : "OrderEvent"), [mode]);

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-6 flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10">
          <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 6v6l4 2m6-2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
        </div>
        <div>
          <h2 className="text-xl font-bold text-dark dark:text-white">WhatsApp Rules</h2>
          <p className="text-sm text-body-color dark:text-dark-6">Zamanlama kuralları: Daily schedule veya Order event trigger.</p>
        </div>
      </div>

      {err && (
        <div className="mb-4 flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 dark:border-red-900/30 dark:bg-red-900/10">
          <svg className="h-4 w-4 flex-shrink-0 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
          <span className="text-sm text-red-600 dark:text-red-400">{err}</span>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Editor */}
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="text-lg font-semibold text-dark dark:text-white">{editId ? "✏️ Düzenle" : "➕ Yeni Kural"}</h3>
            <span className={`rounded-full px-2.5 py-0.5 text-xs font-medium ${editId ? "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/20 dark:text-yellow-400" : "bg-primary/10 text-primary"}`}>
              {editId ? "Editing" : "New"}
            </span>
          </div>
          <div className="grid gap-4">
            <div>
              <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Kural Adı</label>
              <input className="w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white" value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Mod</label>
                <select className="w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white" value={mode} onChange={(e) => setMode(Number(e.target.value) as RuleMode)}>
                  <option value={1}>⏰ Daily</option>
                  <option value={2}>📦 OrderEvent</option>
                </select>
              </div>
              <div>
                <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Günlük Limit</label>
                <select className="w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white" value={dailyLimit} onChange={(e) => setDailyLimit(Number(e.target.value))}>
                  <option value={1}>1 mesaj/gün</option>
                  <option value={2}>2 mesaj/gün</option>
                </select>
              </div>
            </div>
            <div>
              <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Timezone</label>
              <input className="w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white" value={timezone} onChange={(e) => setTimezone(e.target.value)} />
            </div>

            {mode === 1 ? (
              <div className="rounded-lg border border-primary/20 bg-primary/5 p-4 dark:border-primary/10">
                <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-dark dark:text-white">⏰ Daily Zamanlama</div>
                <div className="grid gap-3 md:grid-cols-2">
                  <div>
                    <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Zaman 1 (HH:mm)</label>
                    <input className="w-full rounded-lg border border-stroke bg-white px-3 py-2 text-sm dark:border-dark-3 dark:bg-gray-dark dark:text-white" value={dailyTime1} onChange={(e) => setDailyTime1(e.target.value)} placeholder="10:00" />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Zaman 2 (opsiyonel)</label>
                    <input className="w-full rounded-lg border border-stroke bg-white px-3 py-2 text-sm dark:border-dark-3 dark:bg-gray-dark dark:text-white" value={dailyTime2} onChange={(e) => setDailyTime2(e.target.value)} placeholder="14:00" />
                  </div>
                </div>
                {dailyLimit === 2 && (
                  <div className="mt-3">
                    <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Delay2 (dk)</label>
                    <input type="number" className="w-full rounded-lg border border-stroke bg-white px-3 py-2 text-sm dark:border-dark-3 dark:bg-gray-dark dark:text-white" value={dailyDelay2Minutes} onChange={(e) => setDailyDelay2Minutes(Number(e.target.value))} />
                  </div>
                )}
              </div>
            ) : (
              <div className="rounded-lg border border-blue-200/50 bg-blue-50/50 p-4 dark:border-blue-900/20 dark:bg-blue-900/10">
                <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-dark dark:text-white">📦 Order Event Gecikmeleri</div>
                <div className="grid gap-3 md:grid-cols-2">
                  <div>
                    <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Delay 1 (dk)</label>
                    <input type="number" className="w-full rounded-lg border border-stroke bg-white px-3 py-2 text-sm dark:border-dark-3 dark:bg-gray-dark dark:text-white" value={orderDelay1Minutes} onChange={(e) => setOrderDelay1Minutes(Number(e.target.value))} />
                  </div>
                  {dailyLimit === 2 && (
                    <div>
                      <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Delay 2 (dk)</label>
                      <input type="number" className="w-full rounded-lg border border-stroke bg-white px-3 py-2 text-sm dark:border-dark-3 dark:bg-gray-dark dark:text-white" value={orderDelay2Minutes} onChange={(e) => setOrderDelay2Minutes(Number(e.target.value))} />
                    </div>
                  )}
                </div>
              </div>
            )}

            <label className="flex cursor-pointer items-center gap-2.5" onClick={() => setIsActive(!isActive)}>
              <div className={`relative h-5 w-9 rounded-full transition-colors ${isActive ? "bg-green-500" : "bg-gray-300 dark:bg-dark-3"}`}>
                <div className={`absolute top-0.5 h-4 w-4 rounded-full bg-white shadow transition-transform ${isActive ? "translate-x-4" : "translate-x-0.5"}`} />
              </div>
              <span className="text-sm text-dark dark:text-white">{isActive ? "Aktif" : "Pasif"}</span>
            </label>

            <div className="flex gap-2 pt-1">
              <button onClick={save} className="rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:opacity-90">Kaydet ({modeLabel})</button>
              <button onClick={resetForm} className="rounded-lg border border-stroke px-5 py-2.5 text-sm font-medium text-dark hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2">Sıfırla</button>
            </div>
          </div>
        </div>

        {/* Rules List */}
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="text-lg font-semibold text-dark dark:text-white">Kurallar</h3>
            <button onClick={load} className="flex items-center gap-1.5 rounded-lg border border-stroke px-3 py-2 text-xs font-medium text-dark hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2">
              <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" /></svg>
              Yenile
            </button>
          </div>

          {items.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12 text-center">
              <svg className="mb-3 h-12 w-12 text-body-color/30 dark:text-dark-6/30" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}><path strokeLinecap="round" strokeLinejoin="round" d="M12 6v6l4 2m6-2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
              <p className="text-sm font-medium text-body-color dark:text-dark-6">Henüz kural yok</p>
            </div>
          ) : (
            <div className="space-y-3">
              {items.map((r) => (
                <div key={r.id} className={`rounded-lg border p-4 transition-colors ${r.isActive ? "border-green-200 bg-green-50/50 dark:border-green-900/20 dark:bg-green-900/5" : "border-stroke bg-gray-1/50 dark:border-dark-3 dark:bg-dark-2/30"}`}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-semibold text-dark dark:text-white">{r.name}</span>
                        <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${r.mode === 1 ? "bg-primary/10 text-primary" : "bg-blue-100 text-blue-600 dark:bg-blue-900/20 dark:text-blue-400"}`}>
                          {r.mode === 1 ? "⏰ Daily" : "📦 OrderEvent"}
                        </span>
                      </div>
                      <div className="mt-1.5 flex flex-wrap gap-x-4 gap-y-1 text-xs text-body-color dark:text-dark-6">
                        <span>Limit: <strong className="text-dark dark:text-white">{r.dailyLimit}/gün</strong></span>
                        {r.mode === 1 && r.dailyTime1 && <span>Saat: <strong className="text-dark dark:text-white">{r.dailyTime1.slice(0,5)}</strong>{r.dailyTime2 ? ` / ${r.dailyTime2.slice(0,5)}` : ""}</span>}
                        {r.mode === 2 && r.orderDelay1Minutes != null && <span>Delay: <strong className="text-dark dark:text-white">{r.orderDelay1Minutes}dk</strong>{r.orderDelay2Minutes ? ` / ${r.orderDelay2Minutes}dk` : ""}</span>}
                      </div>
                    </div>
                    <span className={`flex-shrink-0 rounded-full px-2 py-0.5 text-[10px] font-semibold ${r.isActive ? "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400" : "bg-gray-100 text-gray-500 dark:bg-dark-3 dark:text-dark-6"}`}>
                      {r.isActive ? "● Aktif" : "○ Pasif"}
                    </span>
                  </div>
                  <div className="mt-3 flex gap-2">
                    <button onClick={() => edit(r)} className="rounded-md border border-primary/30 px-3 py-1.5 text-xs font-medium text-primary hover:bg-primary/10">Düzenle</button>
                    <button onClick={() => del(r.id)} className="rounded-md border border-red-200 px-3 py-1.5 text-xs font-medium text-red-500 hover:bg-red-50 dark:border-red-900/30 dark:hover:bg-red-900/10">Sil</button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
