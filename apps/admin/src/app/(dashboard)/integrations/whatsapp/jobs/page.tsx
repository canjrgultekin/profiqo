"use client";

import React, { useEffect, useMemo, useState } from "react";

type RuleDto = { id: string; name: string; mode: number; dailyLimit: number; isActive: boolean };
type TemplateDto = { id: string; name: string; status: string };
type JobDto = { id: string; name: string; ruleId: string; template1Id: string; template2Id?: string | null; targetsJson: string; isActive: boolean; updatedAtUtc: string };
type TargetItem = { customerId: string; toE164: string; fullName: string };
type TargetSearchItem = { customerId: string; fullName: string; phoneE164: string | null; lastSeenAtUtc: string };
type TargetSearchResponse = { page: number; pageSize: number; total: number; items: TargetSearchItem[] };

function safeJsonParse<T>(text: string, fallback: T): T { try { return JSON.parse(text) as T; } catch { return fallback; } }
function normalizeE164(v: string): string { const s = (v ?? "").trim(); if (!s) return ""; return s.replace(/\s+/g, ""); }
async function readJsonOrText(res: Response): Promise<{ json: any | null; text: string }> { const text = await res.text(); try { return { json: text ? JSON.parse(text) : null, text }; } catch { return { json: null, text }; } }

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
  const [searchQ, setSearchQ] = useState("");
  const [searchPage, setSearchPage] = useState(1);
  const [searchLoading, setSearchLoading] = useState(false);
  const [searchErr, setSearchErr] = useState<string | null>(null);
  const [searchData, setSearchData] = useState<TargetSearchResponse | null>(null);

  const selectedRule = useMemo(() => rules.find((r) => r.id === ruleId) || null, [rules, ruleId]);
  const targetsJson = useMemo(() => JSON.stringify(targets.map((x) => ({ customerId: x.customerId, toE164: x.toE164, fullName: x.fullName })), null, 2), [targets]);

  const reset = () => { setEditId(null); setName("Promo Job"); setRuleId(rules[0]?.id || ""); setTemplate1Id(templates[0]?.id || ""); setTemplate2Id(""); setTargets([]); setSearchQ(""); setSearchPage(1); setSearchData(null); setSearchErr(null); };

  const loadAll = async () => {
    setErr(null);
    const [r1, r2, r3] = await Promise.all([fetch("/api/whatsapp/rules", { cache: "no-store" }), fetch("/api/whatsapp/templates", { cache: "no-store" }), fetch("/api/whatsapp/jobs", { cache: "no-store" })]);
    const p1 = await r1.json().catch(() => null); const p2 = await r2.json().catch(() => null); const p3 = await r3.json().catch(() => null);
    if (!r1.ok || !r2.ok || !r3.ok) { setErr(p1?.message || p2?.message || p3?.message || "Yükleme başarısız"); return; }
    const rulesItems: RuleDto[] = p1?.items || []; const templateItems: TemplateDto[] = p2?.items || []; const jobItems: JobDto[] = p3?.items || [];
    setRules(rulesItems); setTemplates(templateItems); setJobs(jobItems);
    if (!editId) { setRuleId(rulesItems[0]?.id || ""); setTemplate1Id(templateItems[0]?.id || ""); }
  };

  useEffect(() => { loadAll(); }, []);

  const searchLoad = async (pageOverride?: number) => {
    const page = pageOverride ?? searchPage; setSearchLoading(true); setSearchErr(null);
    const qs = new URLSearchParams({ q: searchQ.trim(), page: String(page), pageSize: "20" });
    const res = await fetch(`/api/whatsapp/targets?${qs.toString()}`, { cache: "no-store" });
    const { json } = await readJsonOrText(res);
    if (!res.ok) { setSearchErr(json?.message || `Arama başarısız (HTTP ${res.status})`); setSearchData(null); setSearchLoading(false); return; }
    setSearchData((json as TargetSearchResponse) || null); setSearchLoading(false);
  };

  const addTarget = (c: TargetSearchItem) => {
    const phone = c.phoneE164 ? normalizeE164(c.phoneE164) : "";
    if (!phone) { alert("Bu müşteride telefon identity yok. Hedefe eklenemez."); return; }
    setTargets((prev) => { if (prev.some((x) => x.customerId === c.customerId)) return prev; return [...prev, { customerId: c.customerId, toE164: phone, fullName: c.fullName }]; });
  };

  const removeTarget = (customerId: string) => { setTargets((prev) => prev.filter((x) => x.customerId !== customerId)); };

  const save = async () => {
    if (!ruleId) return alert("Kural seçin."); if (!template1Id) return alert("Template 1 seçin."); if (!name.trim()) return alert("İsim zorunlu."); if (targets.length === 0) return alert("En az 1 hedef seçin.");
    if (selectedRule?.dailyLimit === 2 && !template2Id) { const ok = confirm("Kural limiti 2 ama Template 2 seçilmemiş. Devam?"); if (!ok) return; }
    const dto: any = { id: editId || "00000000-0000-0000-0000-000000000000", tenantId: "00000000-0000-0000-0000-000000000000", name: name.trim(), ruleId, template1Id, template2Id: template2Id.trim() ? template2Id.trim() : null, targetsJson, isActive: false, createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString() };
    const res = await fetch("/api/whatsapp/jobs", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(dto) });
    const { json } = await readJsonOrText(res);
    if (!res.ok) return alert(json?.message || `Kayıt başarısız (HTTP ${res.status})`);
    reset(); await loadAll();
  };

  const edit = (j: JobDto) => {
    setEditId(j.id); setName(j.name); setRuleId(j.ruleId); setTemplate1Id(j.template1Id); setTemplate2Id(j.template2Id || "");
    const list = safeJsonParse<any[]>(j.targetsJson || "[]", []);
    if (Array.isArray(list)) { setTargets(list.map((x) => ({ customerId: String(x.customerId || ""), toE164: normalizeE164(String(x.toE164 || "")), fullName: String(x.fullName || "Customer") })).filter((x) => x.customerId && x.toE164)); } else { setTargets([]); }
  };

  const del = async (id: string) => { if (!confirm("Bu job silinsin mi?")) return; const res = await fetch(`/api/whatsapp/jobs/${id}`, { method: "DELETE" }); const { json } = await readJsonOrText(res); if (!res.ok) { alert(json?.message || `Silme başarısız`); return; } await loadAll(); };
  const setActive = async (id: string, isActive: boolean) => { const res = await fetch(`/api/whatsapp/jobs/${id}/active`, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ isActive }) }); const { json } = await readJsonOrText(res); if (!res.ok) { alert(json?.message || `İşlem başarısız`); return; } await loadAll(); };
  const runNow = async (id: string) => { const res = await fetch(`/api/whatsapp/jobs/${id}/run-now`, { method: "POST" }); const { json } = await readJsonOrText(res); if (!res.ok) { alert(json?.message || `RunNow başarısız`); return; } alert(`Kuyruğa eklendi: ${json?.enqueued ?? 0}`); };

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-6 flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10">
          <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M21 13.255A23.931 23.931 0 0112 15c-3.183 0-6.22-.62-9-1.745M16 6V4a2 2 0 00-2-2h-4a2 2 0 00-2 2v2m4 6h.01M5 20h14a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" /></svg>
        </div>
        <div>
          <h2 className="text-xl font-bold text-dark dark:text-white">WhatsApp Jobs</h2>
          <p className="text-sm text-body-color dark:text-dark-6">Job = Kural + Şablon + Hedefler. Aktifleştirince otomatik çalışır, RunNow ile anında tetiklenir.</p>
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
            <h3 className="text-lg font-semibold text-dark dark:text-white">{editId ? "✏️ Job Düzenle" : "➕ Yeni Job"}</h3>
            <button onClick={reset} className="rounded-lg border border-stroke px-3 py-1.5 text-xs font-medium text-dark hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2">Sıfırla</button>
          </div>
          <div className="grid gap-4">
            <div>
              <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Job Adı</label>
              <input className="w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white" value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div>
              <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Kural</label>
              <select className="w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white" value={ruleId} onChange={(e) => setRuleId(e.target.value)}>
                {rules.map((r) => (<option key={r.id} value={r.id}>{r.name} ({r.mode === 1 ? "Daily" : "OrderEvent"} / {r.dailyLimit})</option>))}
              </select>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Şablon 1</label>
                <select className="w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white" value={template1Id} onChange={(e) => setTemplate1Id(e.target.value)}>
                  {templates.map((t) => (<option key={t.id} value={t.id}>{t.name}</option>))}
                </select>
              </div>
              <div>
                <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Şablon 2 (opsiyonel)</label>
                <select className="w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white" value={template2Id} onChange={(e) => setTemplate2Id(e.target.value)}>
                  <option value="">(yok)</option>
                  {templates.map((t) => (<option key={t.id} value={t.id}>{t.name}</option>))}
                </select>
                {selectedRule?.dailyLimit === 2 && !template2Id && (
                  <div className="mt-1.5 rounded-md bg-yellow-50 px-2.5 py-1.5 text-xs text-yellow-700 dark:bg-yellow-900/10 dark:text-yellow-400">
                    ⚠️ Kural limiti 2 ama şablon 2 yok — 2. mesaj gönderilmeyecek.
                  </div>
                )}
              </div>
            </div>

            {/* Customer Search + Targets */}
            <div className="grid gap-4 md:grid-cols-2">
              <div className="rounded-lg border border-stroke p-4 dark:border-dark-3">
                <div className="mb-3 text-sm font-semibold text-dark dark:text-white">🔍 Müşteri Ara</div>
                <div className="flex gap-2">
                  <input className="flex-1 rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white" value={searchQ} onChange={(e) => setSearchQ(e.target.value)} placeholder="Ad / Soyad" onKeyDown={(e) => e.key === "Enter" && (setSearchPage(1), searchLoad(1))} />
                  <button onClick={() => { setSearchPage(1); searchLoad(1); }} className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white hover:opacity-90" disabled={searchLoading}>Ara</button>
                </div>
                {searchErr && <div className="mt-2 text-xs text-red-500">{searchErr}</div>}
                <div className="mt-3 max-h-48 overflow-y-auto">
                  {searchLoading ? (
                    <div className="flex items-center gap-2 py-4 text-sm text-body-color"><div className="h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" /> Aranıyor...</div>
                  ) : searchData?.items?.length ? (
                    <div className="space-y-1.5">
                      {searchData.items.map((c) => (
                        <div key={c.customerId} className="flex items-center justify-between rounded-md border border-stroke px-3 py-2 text-sm dark:border-dark-3">
                          <div>
                            <div className="font-medium text-dark dark:text-white">{c.fullName}</div>
                            <div className="font-mono text-[10px] text-body-color dark:text-dark-6">{c.phoneE164 ?? "telefon yok"}</div>
                          </div>
                          <button onClick={() => addTarget(c)} className="rounded-md bg-primary/10 px-2.5 py-1 text-xs font-medium text-primary hover:bg-primary/20">+ Ekle</button>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div className="py-4 text-center text-xs text-body-color dark:text-dark-6">Sonuç yok</div>
                  )}
                </div>
                <div className="mt-3 flex items-center justify-between">
                  <button className="rounded-md border border-stroke px-2.5 py-1 text-xs dark:border-dark-3" onClick={() => { const p = Math.max(1, searchPage - 1); setSearchPage(p); searchLoad(p); }} disabled={searchLoading || searchPage <= 1}>← Önceki</button>
                  <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-semibold text-primary">Sayfa {searchPage}</span>
                  <button className="rounded-md border border-stroke px-2.5 py-1 text-xs dark:border-dark-3" onClick={() => { const p = searchPage + 1; setSearchPage(p); searchLoad(p); }} disabled={searchLoading}>Sonraki →</button>
                </div>
              </div>

              <div className="rounded-lg border border-stroke p-4 dark:border-dark-3">
                <div className="mb-3 flex items-center justify-between">
                  <span className="text-sm font-semibold text-dark dark:text-white">🎯 Seçili Hedefler</span>
                  <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-semibold text-primary">{targets.length}</span>
                </div>
                {targets.length === 0 ? (
                  <div className="py-4 text-center text-xs text-body-color dark:text-dark-6">Hedef seçilmedi</div>
                ) : (
                  <div className="max-h-40 space-y-1.5 overflow-y-auto">
                    {targets.map((t) => (
                      <div key={t.customerId} className="flex items-center justify-between rounded-md bg-green-50/50 px-3 py-2 dark:bg-green-900/5">
                        <div>
                          <div className="text-sm font-medium text-dark dark:text-white">{t.fullName}</div>
                          <div className="font-mono text-[10px] text-body-color dark:text-dark-6">{t.toE164}</div>
                        </div>
                        <button onClick={() => removeTarget(t.customerId)} className="rounded-md bg-red-50 px-2 py-1 text-[10px] font-medium text-red-500 hover:bg-red-100 dark:bg-red-900/10 dark:hover:bg-red-900/20">✕</button>
                      </div>
                    ))}
                  </div>
                )}
                <div className="mt-3">
                  <label className="mb-1 block text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Targets JSON</label>
                  <textarea readOnly className="h-24 w-full rounded-lg border border-stroke bg-gray-1/50 px-3 py-2 font-mono text-[10px] dark:border-dark-3 dark:bg-dark-2/30 dark:text-white" value={targetsJson} />
                </div>
              </div>
            </div>

            <button onClick={save} className="w-fit rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:opacity-90">💾 Job Kaydet</button>
          </div>
        </div>

        {/* Jobs List */}
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="text-lg font-semibold text-dark dark:text-white">Jobs</h3>
            <button onClick={loadAll} className="flex items-center gap-1.5 rounded-lg border border-stroke px-3 py-2 text-xs font-medium text-dark hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2">
              <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" /></svg>
              Yenile
            </button>
          </div>

          {jobs.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12 text-center">
              <svg className="mb-3 h-12 w-12 text-body-color/30 dark:text-dark-6/30" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}><path strokeLinecap="round" strokeLinejoin="round" d="M21 13.255A23.931 23.931 0 0112 15c-3.183 0-6.22-.62-9-1.745M16 6V4a2 2 0 00-2-2h-4a2 2 0 00-2 2v2m4 6h.01M5 20h14a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" /></svg>
              <p className="text-sm font-medium text-body-color dark:text-dark-6">Henüz job yok</p>
            </div>
          ) : (
            <div className="space-y-3">
              {jobs.map((j) => (
                <div key={j.id} className={`rounded-lg border p-4 ${j.isActive ? "border-green-200 bg-green-50/50 dark:border-green-900/20 dark:bg-green-900/5" : "border-stroke dark:border-dark-3"}`}>
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold text-dark dark:text-white">{j.name}</span>
                      <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${j.isActive ? "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400" : "bg-gray-100 text-gray-500 dark:bg-dark-3 dark:text-dark-6"}`}>
                        {j.isActive ? "● Aktif" : "○ Pasif"}
                      </span>
                    </div>
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    <button onClick={() => edit(j)} className="rounded-md border border-primary/30 px-3 py-1.5 text-xs font-medium text-primary hover:bg-primary/10">Düzenle</button>
                    <button onClick={() => setActive(j.id, !j.isActive)} className={`rounded-md px-3 py-1.5 text-xs font-medium ${j.isActive ? "border border-yellow-300 text-yellow-700 hover:bg-yellow-50 dark:border-yellow-900/30 dark:text-yellow-400" : "border border-green-300 text-green-700 hover:bg-green-50 dark:border-green-900/30 dark:text-green-400"}`}>
                      {j.isActive ? "Durdur" : "Aktifleştir"}
                    </button>
                    <button onClick={() => runNow(j.id)} className="rounded-md bg-primary px-3 py-1.5 text-xs font-semibold text-white hover:opacity-90">▶ Şimdi Çalıştır</button>
                    <button onClick={() => del(j.id)} className="rounded-md border border-red-200 px-3 py-1.5 text-xs font-medium text-red-500 hover:bg-red-50 dark:border-red-900/30 dark:hover:bg-red-900/10">Sil</button>
                  </div>
                </div>
              ))}
            </div>
          )}

          <div className="mt-4 rounded-lg bg-gray-1/50 px-3 py-2 text-xs text-body-color dark:bg-dark-2/30 dark:text-dark-6">
            💡 Aktifleştirilen job'lar scheduler tarafından otomatik çalıştırılır. Worker test modunda dummy mesaj gönderir.
          </div>
        </div>
      </div>
    </div>
  );
}
