"use client";

import React, { useEffect, useMemo, useState } from "react";

type RuleDto = {
  id: string;
  name: string;
  mode: number; // 1=Daily,2=OrderEvent
  dailyLimit: number; // 1|2
  isActive: boolean;
};

type TemplateDto = { id: string; name: string; status: string };

type JobDto = {
  id: string;
  name: string;
  ruleId: string;
  template1Id: string;
  template2Id?: string | null;
  targetsJson: string;
  isActive: boolean;
  updatedAtUtc: string;
};

type TargetItem = {
  customerId: string; // canonical customer id
  toE164: string; // açık telefon
  fullName: string; // ad soyad
};

type TargetSearchItem = {
  customerId: string;
  fullName: string;
  phoneE164: string | null;
  lastSeenAtUtc: string;
};

type TargetSearchResponse = {
  page: number;
  pageSize: number;
  total: number;
  items: TargetSearchItem[];
};

function safeJsonParse<T>(text: string, fallback: T): T {
  try {
    return JSON.parse(text) as T;
  } catch {
    return fallback;
  }
}

function normalizeE164(v: string): string {
  const s = (v ?? "").trim();
  if (!s) return "";
  return s.replace(/\s+/g, "");
}

async function readJsonOrText(res: Response): Promise<{ json: any | null; text: string }> {
  const text = await res.text();
  try {
    return { json: text ? JSON.parse(text) : null, text };
  } catch {
    return { json: null, text };
  }
}

export default function WhatsappJobsPage() {
  const [rules, setRules] = useState<RuleDto[]>([]);
  const [templates, setTemplates] = useState<TemplateDto[]>([]);
  const [jobs, setJobs] = useState<JobDto[]>([]);
  const [err, setErr] = useState<string | null>(null);

  const [editId, setEditId] = useState<string | null>(null);

  const [name, setName] = useState("Promo Job");
  const [ruleId, setRuleId] = useState<string>("");
  const [template1Id, setTemplate1Id] = useState<string>("");
  const [template2Id, setTemplate2Id] = useState<string>("");

  const [targets, setTargets] = useState<TargetItem[]>([]);

  // Search
  const [searchQ, setSearchQ] = useState("");
  const [searchPage, setSearchPage] = useState(1);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchErr, setSearchErr] = useState<string | null>(null);
  const [searchData, setSearchData] = useState<TargetSearchResponse | null>(null);

  const selectedRule = useMemo(() => rules.find((r) => r.id === ruleId) || null, [rules, ruleId]);

  const targetsJson = useMemo(
    () => JSON.stringify(targets.map((x) => ({ customerId: x.customerId, toE164: x.toE164, fullName: x.fullName })), null, 2),
    [targets]
  );

  const reset = () => {
    setEditId(null);
    setName("Promo Job");
    setRuleId(rules[0]?.id || "");
    setTemplate1Id(templates[0]?.id || "");
    setTemplate2Id("");
    setTargets([]);
    setSearchQ("");
    setSearchPage(1);
    setSearchData(null);
    setSearchErr(null);
  };

  const loadAll = async () => {
    setErr(null);

    const [r1, r2, r3] = await Promise.all([
      fetch("/api/whatsapp/rules", { cache: "no-store" }),
      fetch("/api/whatsapp/templates", { cache: "no-store" }),
      fetch("/api/whatsapp/jobs", { cache: "no-store" }),
    ]);

    const p1 = await r1.json().catch(() => null);
    const p2 = await r2.json().catch(() => null);
    const p3 = await r3.json().catch(() => null);

    if (!r1.ok || !r2.ok || !r3.ok) {
      setErr(p1?.message || p2?.message || p3?.message || "Load failed");
      return;
    }

    const rulesItems: RuleDto[] = p1?.items || [];
    const templateItems: TemplateDto[] = p2?.items || [];
    const jobItems: JobDto[] = p3?.items || [];

    setRules(rulesItems);
    setTemplates(templateItems);
    setJobs(jobItems);

    if (!editId) {
      setRuleId(rulesItems[0]?.id || "");
      setTemplate1Id(templateItems[0]?.id || "");
    }
  };

  useEffect(() => {
    loadAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const searchLoad = async (pageOverride?: number) => {
    const page = pageOverride ?? searchPage;

    setSearchLoading(true);
    setSearchErr(null);

    const qs = new URLSearchParams({
      q: searchQ.trim(),
      page: String(page),
      pageSize: "20",
    });

    const res = await fetch(`/api/whatsapp/targets?${qs.toString()}`, { cache: "no-store" });
    const { json, text } = await readJsonOrText(res);

    if (!res.ok) {
      setSearchErr(json?.message || `Search failed (HTTP ${res.status})`);
      setSearchData(null);
      setSearchLoading(false);
      return;
    }

    setSearchData((json as TargetSearchResponse) || null);
    setSearchLoading(false);
  };

  const addTarget = (c: TargetSearchItem) => {
    const phone = c.phoneE164 ? normalizeE164(c.phoneE164) : "";
    if (!phone) {
      alert("Bu müşteride phone identity yok (E164 boş). Hedefe eklenemez.");
      return;
    }

    setTargets((prev) => {
      if (prev.some((x) => x.customerId === c.customerId)) return prev;
      return [...prev, { customerId: c.customerId, toE164: phone, fullName: c.fullName }];
    });
  };

  const removeTarget = (customerId: string) => {
    setTargets((prev) => prev.filter((x) => x.customerId !== customerId));
  };

  const save = async () => {
    if (!ruleId) return alert("Rule seç.");
    if (!template1Id) return alert("Template1 seç.");
    if (!name.trim()) return alert("Name zorunlu.");
    if (targets.length === 0) return alert("En az 1 target seçmelisin.");

    if (selectedRule?.dailyLimit === 2 && !template2Id) {
      const ok = confirm("Rule limit 2 ama Template2 seçilmemiş. 2. mesaj enqueue edilmeyecek. Devam?");
      if (!ok) return;
    }

    const dto: any = {
      id: editId || "00000000-0000-0000-0000-000000000000",
      tenantId: "00000000-0000-0000-0000-000000000000",
      name: name.trim(),
      ruleId,
      template1Id,
      template2Id: template2Id.trim() ? template2Id.trim() : null,
      targetsJson,
      isActive: false,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
    };

    const res = await fetch("/api/whatsapp/jobs", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(dto),
    });

    const { json } = await readJsonOrText(res);

    if (!res.ok) return alert(json?.message || `Save failed (HTTP ${res.status})`);

    reset();
    await loadAll();
  };

  const edit = (j: JobDto) => {
    setEditId(j.id);
    setName(j.name);
    setRuleId(j.ruleId);
    setTemplate1Id(j.template1Id);
    setTemplate2Id(j.template2Id || "");

    const list = safeJsonParse<any[]>(j.targetsJson || "[]", []);
    if (Array.isArray(list)) {
      const hydrated: TargetItem[] = list
        .map((x) => ({
          customerId: String(x.customerId || ""),
          toE164: normalizeE164(String(x.toE164 || "")),
          fullName: String(x.fullName || "Customer"),
        }))
        .filter((x) => x.customerId && x.toE164);

      setTargets(hydrated);
    } else {
      setTargets([]);
    }
  };

  const del = async (id: string) => {
    if (!confirm("Delete job?")) return;

    const res = await fetch(`/api/whatsapp/jobs/${id}`, { method: "DELETE" });
    const { json } = await readJsonOrText(res);

    if (!res.ok) {
      alert(json?.message || `Delete failed (HTTP ${res.status})`);
      return;
    }

    await loadAll();
  };

  const setActive = async (id: string, isActive: boolean) => {
    const res = await fetch(`/api/whatsapp/jobs/${id}/active`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ isActive }),
    });

    const { json, text } = await readJsonOrText(res);

    if (!res.ok) {
      alert(json?.message || `Active failed (HTTP ${res.status})`);
      return;
    }

    await loadAll();
  };

  const runNow = async (id: string) => {
    const res = await fetch(`/api/whatsapp/jobs/${id}/run-now`, { method: "POST" });
    const { json } = await readJsonOrText(res);

    if (!res.ok) {
      alert(json?.message || `RunNow failed (HTTP ${res.status})`);
      return;
    }

    alert(`RunNow queued: ${json?.enqueued ?? 0}`);
  };

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4">
        <h2 className="text-xl font-semibold text-dark dark:text-white">WhatsApp Jobs</h2>
        <p className="text-sm text-body-color dark:text-dark-6">
          Job = Rule + Template1(+Template2) + Targets. Aktifleştirince scheduler otomatik enqueue eder, RunNow ile anında enqueue.
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
            <button
              onClick={reset}
              className="rounded bg-dark px-3 py-2 text-sm font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark"
            >
              Reset
            </button>
          </div>

          <div className="grid gap-3">
            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Name</label>
              <input
                className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Rule</label>
              <select
                className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={ruleId}
                onChange={(e) => setRuleId(e.target.value)}
              >
                {rules.map((r) => (
                  <option key={r.id} value={r.id}>
                    {r.name} ({r.mode === 1 ? "Daily" : "OrderEvent"} / {r.dailyLimit})
                  </option>
                ))}
              </select>
            </div>

            <div className="grid gap-3 md:grid-cols-2">
              <div>
                <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Template 1</label>
                <select
                  className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                  value={template1Id}
                  onChange={(e) => setTemplate1Id(e.target.value)}
                >
                  {templates.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Template 2 (optional)</label>
                <select
                  className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                  value={template2Id}
                  onChange={(e) => setTemplate2Id(e.target.value)}
                >
                  <option value="">(none)</option>
                  {templates.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>

                {selectedRule?.dailyLimit === 2 && !template2Id ? (
                  <div className="mt-1 rounded bg-yellow-500/20 px-2 py-1 text-xs text-yellow-700">
                    Rule limit 2 ama template2 yoksa 2. mesaj enqueue edilmez.
                  </div>
                ) : null}
              </div>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="rounded border border-stroke p-3 dark:border-dark-3">
                <div className="mb-2 text-sm font-semibold text-dark dark:text-white">Customer Search</div>

                <div className="flex gap-2">
                  <input
                    className="flex-1 rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                    value={searchQ}
                    onChange={(e) => setSearchQ(e.target.value)}
                    placeholder="Ad / Soyad ara"
                  />
                  <button
                    onClick={() => {
                      setSearchPage(1);
                      searchLoad(1);
                    }}
                    className="rounded bg-primary px-3 py-2 text-sm font-semibold text-white hover:opacity-90"
                    disabled={searchLoading}
                  >
                    Search
                  </button>
                </div>

                {searchErr && <div className="mt-2 text-xs text-red-500">{searchErr}</div>}

                <div className="mt-3 overflow-x-auto">
                  <table className="w-full table-auto">
                    <thead>
                      <tr className="text-left text-xs text-body-color dark:text-dark-6">
                        <th className="px-2 py-2">Name</th>
                        <th className="px-2 py-2">Phone</th>
                        <th className="px-2 py-2">Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {searchLoading ? (
                        <tr>
                          <td className="px-2 py-2 text-sm" colSpan={3}>
                            Loading...
                          </td>
                        </tr>
                      ) : searchData?.items?.length ? (
                        searchData.items.map((c) => (
                          <tr key={c.customerId} className="border-t border-stroke dark:border-dark-3 text-sm">
                            <td className="px-2 py-2">{c.fullName}</td>
                            <td className="px-2 py-2 font-mono text-xs">{c.phoneE164 ?? "-"}</td>
                            <td className="px-2 py-2">
                              <button
                                onClick={() => addTarget(c)}
                                className="rounded bg-dark px-3 py-1 text-xs font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark"
                              >
                                Add
                              </button>
                            </td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td className="px-2 py-2 text-sm" colSpan={3}>
                            No results.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>

                <div className="mt-3 flex items-center justify-between">
                  <button
                    className="rounded border border-stroke px-3 py-2 text-sm dark:border-dark-3"
                    onClick={() => setSearchPage((p) => Math.max(1, p - 1))}
                    disabled={searchLoading || searchPage <= 1}
                  >
                    Prev
                  </button>

                  <div className="text-xs text-body-color dark:text-dark-6">Page {searchPage}</div>

                  <button
                    className="rounded border border-stroke px-3 py-2 text-sm dark:border-dark-3"
                    onClick={() => setSearchPage((p) => p + 1)}
                    disabled={searchLoading}
                  >
                    Next
                  </button>
                </div>
              </div>

              <div className="rounded border border-stroke p-3 dark:border-dark-3">
                <div className="mb-2 text-sm font-semibold text-dark dark:text-white">Selected Targets</div>

                {targets.length === 0 ? (
                  <div className="text-sm text-body-color dark:text-dark-6">No targets selected.</div>
                ) : (
                  <div className="grid gap-2">
                    {targets.map((t) => (
                      <div
                        key={t.customerId}
                        className="flex items-center justify-between rounded border border-stroke px-3 py-2 dark:border-dark-3"
                      >
                        <div>
                          <div className="text-sm text-dark dark:text-white">{t.fullName}</div>
                          <div className="text-xs font-mono text-body-color dark:text-dark-6">{t.toE164}</div>
                        </div>
                        <button
                          onClick={() => removeTarget(t.customerId)}
                          className="rounded bg-red-500/20 px-3 py-1 text-xs text-red-700 dark:text-red-400"
                        >
                          Remove
                        </button>
                      </div>
                    ))}
                  </div>
                )}

                <div className="mt-3">
                  <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Targets JSON (saved)</label>
                  <textarea
                    readOnly
                    className="h-40 w-full rounded border border-stroke bg-transparent px-3 py-2 font-mono text-xs dark:border-dark-3 dark:text-white"
                    value={targetsJson}
                  />
                </div>
              </div>
            </div>

            <button
              onClick={save}
              className="w-fit rounded bg-primary px-4 py-2 text-sm font-semibold text-white hover:opacity-90"
            >
              Save Job
            </button>
          </div>
        </div>

        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-3 flex items-center justify-between">
            <h3 className="text-lg font-semibold text-dark dark:text-white">Jobs</h3>
            <button
              onClick={loadAll}
              className="rounded bg-dark px-3 py-2 text-sm font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark"
            >
              Refresh
            </button>
          </div>

          {jobs.length === 0 ? (
            <div className="text-sm text-body-color dark:text-dark-6">No jobs.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full table-auto">
                <thead>
                  <tr className="text-left text-xs text-body-color dark:text-dark-6">
                    <th className="px-2 py-2">Name</th>
                    <th className="px-2 py-2">Active</th>
                    <th className="px-2 py-2">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {jobs.map((j) => (
                    <tr key={j.id} className="border-t border-stroke dark:border-dark-3 text-sm">
                      <td className="px-2 py-2">{j.name}</td>
                      <td className="px-2 py-2">{j.isActive ? "Yes" : "No"}</td>
                      <td className="px-2 py-2 flex gap-2">
                        <button
                          onClick={() => edit(j)}
                          className="rounded bg-dark px-3 py-1 text-xs font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark"
                        >
                          Edit
                        </button>
                        <button
                          onClick={() => setActive(j.id, !j.isActive)}
                          className={`rounded px-3 py-1 text-xs font-semibold ${
                            j.isActive ? "bg-yellow-500/20 text-yellow-700" : "bg-green-500/20 text-green-700"
                          }`}
                        >
                          {j.isActive ? "Deactivate" : "Activate"}
                        </button>
                        <button
                          onClick={() => runNow(j.id)}
                          className="rounded bg-primary px-3 py-1 text-xs font-semibold text-white hover:opacity-90"
                        >
                          RunNow
                        </button>
                        <button
                          onClick={() => del(j.id)}
                          className="rounded bg-red-500/20 px-3 py-1 text-xs text-red-700 dark:text-red-400"
                        >
                          Delete
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <div className="mt-3 text-xs text-body-color dark:text-dark-6">
            Activate edilince scheduler günlük veya order event’e göre dispatch üretir. Worker dummy sent yapar.
          </div>
        </div>
      </div>
    </div>
  );
}
