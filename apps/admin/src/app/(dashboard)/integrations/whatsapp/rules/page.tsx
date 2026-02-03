"use client";

import React, { useEffect, useMemo, useState } from "react";

type RuleMode = 1 | 2; // 1=Daily, 2=OrderEvent

type RuleDto = {
  id: string;
  tenantId: string;
  name: string;
  mode: RuleMode;
  dailyLimit: number;
  timezone: string;
  dailyTime1?: string | null;
  dailyTime2?: string | null;
  dailyDelay2Minutes?: number | null;
  orderDelay1Minutes?: number | null;
  orderDelay2Minutes?: number | null;
  isActive: boolean;
  updatedAtUtc: string;
};

function timeOnlyOrNull(v: string): string | null {
  const s = v.trim();
  if (!s) return null;
  return s.length === 5 ? `${s}:00` : s; // backend TimeOnly parse edebilir (HH:mm:ss)
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
    setEditId(null);
    setName("Daily Promo");
    setMode(1);
    setDailyLimit(2);
    setTimezone("Europe/Istanbul");
    setDailyTime1("10:00");
    setDailyTime2("");
    setDailyDelay2Minutes(90);
    setOrderDelay1Minutes(10);
    setOrderDelay2Minutes(120);
    setIsActive(true);
  };

  const load = async () => {
    setErr(null);
    const res = await fetch("/api/whatsapp/rules", { cache: "no-store" });
    const payload = await res.json().catch(() => null);
    if (!res.ok) {
      setErr(payload?.message || "Failed to load rules.");
      setItems([]);
      return;
    }
    setItems(payload?.items || []);
  };

  useEffect(() => {
    load();
  }, []);

  const save = async () => {
    const dto: any = {
      id: editId || "00000000-0000-0000-0000-000000000000",
      tenantId: "00000000-0000-0000-0000-000000000000",
      name: name.trim(),
      mode,
      dailyLimit: dailyLimit === 2 ? 2 : 1,
      timezone: timezone.trim() || "Europe/Istanbul",
      dailyTime1: mode === 1 ? timeOnlyOrNull(dailyTime1) : null,
      dailyTime2: mode === 1 ? timeOnlyOrNull(dailyTime2) : null,
      dailyDelay2Minutes: mode === 1 && dailyLimit === 2 ? Number(dailyDelay2Minutes || 0) : null,
      orderDelay1Minutes: mode === 2 ? Number(orderDelay1Minutes || 0) : null,
      orderDelay2Minutes: mode === 2 && dailyLimit === 2 ? Number(orderDelay2Minutes || 0) : null,
      isActive,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
    };

    const res = await fetch("/api/whatsapp/rules", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(dto),
    });

    const payload = await res.json().catch(() => null);
    if (!res.ok) {
      alert(payload?.message || "Save failed");
      return;
    }

    resetForm();
    await load();
  };

  const edit = (r: RuleDto) => {
    setEditId(r.id);
    setName(r.name);
    setMode(r.mode);
    setDailyLimit(r.dailyLimit);
    setTimezone(r.timezone || "Europe/Istanbul");
    setDailyTime1((r.dailyTime1 || "10:00").slice(0, 5));
    setDailyTime2((r.dailyTime2 || "").slice(0, 5));
    setDailyDelay2Minutes(r.dailyDelay2Minutes ?? 90);
    setOrderDelay1Minutes(r.orderDelay1Minutes ?? 10);
    setOrderDelay2Minutes(r.orderDelay2Minutes ?? 120);
    setIsActive(Boolean(r.isActive));
  };

  const del = async (id: string) => {
    if (!confirm("Delete rule?")) return;
    const res = await fetch(`/api/whatsapp/rules/${id}`, { method: "DELETE" });
    if (!res.ok) {
      const p = await res.json().catch(() => null);
      alert(p?.message || "Delete failed");
      return;
    }
    await load();
  };

  const modeLabel = useMemo(() => (mode === 1 ? "Daily" : "OrderEvent"), [mode]);

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4">
        <h2 className="text-xl font-semibold text-dark dark:text-white">WhatsApp Rules</h2>
        <p className="text-sm text-body-color dark:text-dark-6">
          Daily schedule veya Order event trigger. Limit müşteri başına günlük 1 ya da 2.
        </p>
      </div>

      {err && (
        <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-600 dark:text-red-400">
          {err}
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-3 flex items-center justify-between">
            <h3 className="text-lg font-semibold text-dark dark:text-white">Editor</h3>
            <span className="text-xs text-body-color dark:text-dark-6">{editId ? "Editing" : "New"}</span>
          </div>

          <div className="grid gap-3">
            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Name</label>
              <input className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={name} onChange={(e) => setName(e.target.value)} />
            </div>

            <div className="grid gap-3 md:grid-cols-2">
              <div>
                <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Mode</label>
                <select className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                  value={mode} onChange={(e) => setMode(Number(e.target.value) as RuleMode)}>
                  <option value={1}>Daily</option>
                  <option value={2}>OrderEvent</option>
                </select>
              </div>

              <div>
                <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Daily Limit</label>
                <select className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                  value={dailyLimit} onChange={(e) => setDailyLimit(Number(e.target.value))}>
                  <option value={1}>1 message/day</option>
                  <option value={2}>2 messages/day</option>
                </select>
              </div>
            </div>

            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Timezone</label>
              <input className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={timezone} onChange={(e) => setTimezone(e.target.value)} />
              <div className="mt-1 text-xs text-body-color dark:text-dark-6">Default: Europe/Istanbul</div>
            </div>

            {mode === 1 ? (
              <div className="rounded border border-stroke p-3 dark:border-dark-3">
                <div className="text-sm font-semibold text-dark dark:text-white mb-2">Daily</div>

                <div className="grid gap-3 md:grid-cols-2">
                  <div>
                    <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Time 1 (HH:mm)</label>
                    <input className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                      value={dailyTime1} onChange={(e) => setDailyTime1(e.target.value)} placeholder="10:00" />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Time 2 (optional)</label>
                    <input className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                      value={dailyTime2} onChange={(e) => setDailyTime2(e.target.value)} placeholder="14:00" />
                  </div>
                </div>

                {dailyLimit === 2 ? (
                  <div className="mt-3">
                    <label className="mb-1 block text-xs text-body-color dark:text-dark-6">
                      Delay2 minutes (Time2 boşsa Time1 + delay)
                    </label>
                    <input type="number"
                      className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                      value={dailyDelay2Minutes} onChange={(e) => setDailyDelay2Minutes(Number(e.target.value))} />
                  </div>
                ) : null}
              </div>
            ) : (
              <div className="rounded border border-stroke p-3 dark:border-dark-3">
                <div className="text-sm font-semibold text-dark dark:text-white mb-2">Order Event</div>

                <div className="grid gap-3 md:grid-cols-2">
                  <div>
                    <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Delay1 minutes</label>
                    <input type="number"
                      className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                      value={orderDelay1Minutes} onChange={(e) => setOrderDelay1Minutes(Number(e.target.value))} />
                  </div>
                  {dailyLimit === 2 ? (
                    <div>
                      <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Delay2 minutes</label>
                      <input type="number"
                        className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                        value={orderDelay2Minutes} onChange={(e) => setOrderDelay2Minutes(Number(e.target.value))} />
                    </div>
                  ) : null}
                </div>
              </div>
            )}

            <label className="flex items-center gap-2 text-sm text-body-color dark:text-dark-6">
              <input type="checkbox" checked={isActive} onChange={(e) => setIsActive(e.target.checked)} />
              Rule active
            </label>

            <div className="flex gap-2">
              <button onClick={save}
                className="rounded bg-primary px-4 py-2 text-sm font-semibold text-white hover:opacity-90">
                Save {modeLabel}
              </button>
              <button onClick={resetForm}
                className="rounded bg-dark px-4 py-2 text-sm font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark">
                Reset
              </button>
            </div>
          </div>
        </div>

        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-3 flex items-center justify-between">
            <h3 className="text-lg font-semibold text-dark dark:text-white">Rules</h3>
            <button onClick={load}
              className="rounded bg-dark px-3 py-2 text-sm font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark">
              Refresh
            </button>
          </div>

          {items.length === 0 ? (
            <div className="text-sm text-body-color dark:text-dark-6">No rules.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full table-auto">
                <thead>
                  <tr className="text-left text-xs text-body-color dark:text-dark-6">
                    <th className="px-2 py-2">Name</th>
                    <th className="px-2 py-2">Mode</th>
                    <th className="px-2 py-2">Limit</th>
                    <th className="px-2 py-2">Active</th>
                    <th className="px-2 py-2">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((r) => (
                    <tr key={r.id} className="border-t border-stroke dark:border-dark-3 text-sm">
                      <td className="px-2 py-2">{r.name}</td>
                      <td className="px-2 py-2">{r.mode === 1 ? "Daily" : "OrderEvent"}</td>
                      <td className="px-2 py-2">{r.dailyLimit}</td>
                      <td className="px-2 py-2">{r.isActive ? "Yes" : "No"}</td>
                      <td className="px-2 py-2 flex gap-2">
                        <button onClick={() => edit(r)}
                          className="rounded bg-dark px-3 py-1 text-xs font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark">
                          Edit
                        </button>
                        <button onClick={() => del(r.id)}
                          className="rounded bg-red-500/20 px-3 py-1 text-xs text-red-700 dark:text-red-400">
                          Delete
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
