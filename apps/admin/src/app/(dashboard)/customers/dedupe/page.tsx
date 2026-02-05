"use client";

import Breadcrumb from "@/components/Breadcrumbs/Breadcrumb";
import { useCallback, useEffect, useMemo, useState } from "react";

type PendingCustomer = { customerId: string; firstName: string | null; lastName: string | null; providers: string[] };
type PendingGroup = { groupKey: string; normalizedName: string; count: number; customers: PendingCustomer[] };
type Address = { fullName?: string | null; addressLine1?: string | null; addressLine2?: string | null; district?: string | null; city?: string | null; postalCode?: string | null; country?: string | null };
type ChannelRow = { channel: string; ordersCount: number; totalAmount: number; currency: string };
type SuggestionCandidate = { customerId: string; firstName: string | null; lastName: string | null; providers: string[]; channels: ChannelRow[]; shippingAddress: Address | null; billingAddress: Address | null };
type SuggestionGroup = { groupKey: string; confidence: number; normalizedName: string; rationale: string; candidates: SuggestionCandidate[] };

function fullName(first?: string | null, last?: string | null) { return `${(first ?? "").trim()} ${(last ?? "").trim()}`.trim() || "(İsimsiz)"; }
function fmtProviders(p?: string[]) { if (!p || p.length === 0) return "-"; return p.join(", "); }
function fmtAddress(a: Address | null) {
  if (!a) return "(adres yok)";
  const parts = [a.addressLine1, a.addressLine2, a.district, a.city, a.postalCode, a.country].map((x) => (x ?? "").trim()).filter(Boolean);
  return parts.length ? parts.join(", ") : "(adres yok)";
}
function fmtMoney(a: number, c: string) { return `${a.toLocaleString("tr-TR", { minimumFractionDigits: 2 })} ${c}`; }

function ConfidenceBadge({ value }: { value: number }) {
  const pct = Math.round(value * 100);
  const color = pct >= 90 ? "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400" : pct >= 70 ? "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/20 dark:text-yellow-400" : "bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400";
  return <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${color}`}>%{pct} güven</span>;
}

export default function CustomerDedupePage() {
  const [threshold, setThreshold] = useState<number>(0.78);
  const [pending, setPending] = useState<PendingGroup[]>([]);
  const [suggestions, setSuggestions] = useState<SuggestionGroup[]>([]);
  const [loading, setLoading] = useState(true);
  const [analyzing, setAnalyzing] = useState(false);
  const [deciding, setDeciding] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);

  const analyzeDisabled = useMemo(() => analyzing || deciding || suggestions.length > 0, [analyzing, deciding, suggestions.length]);

  const loadAll = useCallback(async () => {
    setLoading(true); setError(null);
    try {
      const [pendingRes, suggestionsRes] = await Promise.all([fetch("/api/customers/dedupe/pending", { cache: "no-store" }), fetch("/api/customers/dedupe/suggestions/details", { cache: "no-store" })]);
      if (!pendingRes.ok) throw new Error((await pendingRes.text()) || "Pending alınamadı");
      if (!suggestionsRes.ok) throw new Error((await suggestionsRes.text()) || "Suggestions alınamadı");
      const pendingJson = await pendingRes.json(); const suggestionsJson = await suggestionsRes.json();
      setPending((pendingJson.items ?? []) as PendingGroup[]); setSuggestions((suggestionsJson.items ?? []) as SuggestionGroup[]);
    } catch (e: any) { setError(e?.message ?? "Beklenmeyen hata"); } finally { setLoading(false); }
  }, []);

  useEffect(() => { void loadAll(); }, [loadAll]);

  const runAnalyze = useCallback(async () => {
    setAnalyzing(true); setError(null); setInfo(null);
    try {
      const res = await fetch("/api/customers/dedupe/analyze", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ threshold }) });
      if (!res.ok) throw new Error((await res.text()) || "Analiz başarısız");
      await loadAll(); setInfo("Analiz tamamlandı. Grupları onaylayın veya reddedin.");
    } catch (e: any) { setError(e?.message ?? "Beklenmeyen hata"); } finally { setAnalyzing(false); }
  }, [threshold, loadAll]);

  const decide = useCallback(async (groupKey: string, action: "approve" | "reject") => {
    setDeciding(true); setError(null); setInfo(null);
    try {
      const res = await fetch(`/api/customers/dedupe/suggestions/${encodeURIComponent(groupKey)}/${action}`, { method: "POST" });
      if (!res.ok) throw new Error((await res.text()) || "İşlem başarısız");
      await loadAll(); setInfo(action === "approve" ? "Onaylandı. Tüm ekranlarda tek müşteri görünecek." : "Reddedildi.");
    } catch (e: any) { setError(e?.message ?? "Beklenmeyen hata"); } finally { setDeciding(false); }
  }, [loadAll]);

  const decideAll = useCallback(async (action: "approve" | "reject") => {
    if (suggestions.length === 0) return;
    setDeciding(true); setError(null); setInfo(null);
    try {
      for (const g of suggestions) { const res = await fetch(`/api/customers/dedupe/suggestions/${encodeURIComponent(g.groupKey)}/${action}`, { method: "POST" }); if (!res.ok) throw new Error((await res.text()) || "İşlem başarısız"); }
      await loadAll(); setInfo(action === "approve" ? "Tüm gruplar onaylandı." : "Tüm gruplar reddedildi.");
    } catch (e: any) { setError(e?.message ?? "Beklenmeyen hata"); } finally { setDeciding(false); }
  }, [suggestions, loadAll]);

  return (
    <div className="space-y-6">
      <Breadcrumb pageName="Müşteri Tekilleştirme (Dedupe)" />

      {/* Analyzing Modal */}
      {analyzing && (
        <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl dark:bg-gray-dark">
            <div className="flex items-center gap-3">
              <div className="h-8 w-8 animate-spin rounded-full border-3 border-primary border-t-transparent" />
              <div>
                <div className="text-lg font-semibold text-dark dark:text-white">Analiz ediliyor…</div>
                <div className="mt-1 text-sm text-body-color dark:text-dark-6">Fuzzy benzeştirme çalışıyor.</div>
              </div>
            </div>
            <div className="mt-4 h-2 w-full overflow-hidden rounded-full bg-gray-200 dark:bg-dark-3">
              <div className="h-full w-2/3 animate-pulse rounded-full bg-primary" />
            </div>
          </div>
        </div>
      )}

      {/* Analyze Card */}
      <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/10 text-sm">🔍</div>
              <h3 className="text-lg font-semibold text-dark dark:text-white">Analiz</h3>
            </div>
            <p className="mt-1.5 text-sm text-body-color dark:text-dark-6">Aynı ad soyad gruplarını adres benzerliğine göre öneriye çevirir. Öneriler karar verilene kadar analiz butonu pasif.</p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div>
              <label className="mb-1 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Threshold</label>
              <input type="number" min={0} max={1} step={0.01} value={threshold} onChange={(e) => setThreshold(Number(e.target.value))}
                className="w-28 rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm outline-none focus:border-primary dark:border-dark-3 dark:text-white" />
            </div>
            <div className="flex gap-2">
              <button onClick={runAnalyze} disabled={analyzeDisabled} className="rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:opacity-90 disabled:opacity-50">Analiz Et</button>
              {suggestions.length > 0 && (
                <>
                  <button onClick={() => void decideAll("approve")} disabled={deciding} className="rounded-lg bg-green-500 px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50">Tümünü Onayla</button>
                  <button onClick={() => void decideAll("reject")} disabled={deciding} className="rounded-lg border border-red-200 px-4 py-2.5 text-sm font-semibold text-red-500 hover:bg-red-50 disabled:opacity-50 dark:border-red-900/30 dark:hover:bg-red-900/10">Tümünü Reddet</button>
                </>
              )}
            </div>
          </div>
        </div>

        {error && (
          <div className="mt-4 flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 dark:border-red-900/30 dark:bg-red-900/10">
            <svg className="h-4 w-4 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>
            <span className="text-sm text-red-600 dark:text-red-400">{error}</span>
          </div>
        )}
        {info && (
          <div className="mt-4 flex items-center gap-2 rounded-lg border border-green-200 bg-green-50 px-4 py-3 dark:border-green-900/30 dark:bg-green-900/10">
            <svg className="h-4 w-4 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" /></svg>
            <span className="text-sm text-green-600 dark:text-green-400">{info}</span>
          </div>
        )}
      </div>

      {/* Content */}
      {loading ? (
        <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark">
          <div className="flex items-center gap-3"><div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" /><span className="text-sm text-body-color dark:text-dark-6">Yükleniyor…</span></div>
        </div>
      ) : suggestions.length > 0 ? (
        <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-4 flex items-baseline justify-between">
            <div>
              <h3 className="text-lg font-semibold text-dark dark:text-white">Analiz Sonuçları</h3>
              <p className="mt-1 text-sm text-body-color dark:text-dark-6">Onaylanan gruplar tüm ekranlarda tek müşteri olarak görünecek.</p>
            </div>
            <span className="rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-semibold text-primary">{suggestions.length} grup</span>
          </div>

          <div className="space-y-4">
            {suggestions.map((g) => (
              <div key={g.groupKey} className="rounded-xl border border-stroke p-5 dark:border-dark-3">
                <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="text-base font-bold text-dark dark:text-white">{g.normalizedName}</span>
                      <ConfidenceBadge value={g.confidence} />
                    </div>
                    <p className="mt-1.5 text-sm text-body-color dark:text-dark-6">{g.rationale}</p>
                  </div>
                  <div className="flex gap-2">
                    <button onClick={() => void decide(g.groupKey, "approve")} disabled={deciding} className="rounded-lg bg-green-500 px-4 py-2 text-xs font-semibold text-white hover:opacity-90 disabled:opacity-50">✓ Onayla</button>
                    <button onClick={() => void decide(g.groupKey, "reject")} disabled={deciding} className="rounded-lg border border-red-200 px-4 py-2 text-xs font-semibold text-red-500 hover:bg-red-50 disabled:opacity-50 dark:border-red-900/30">✕ Reddet</button>
                  </div>
                </div>

                <div className="mt-4 grid gap-3 md:grid-cols-2">
                  {g.candidates.map((c) => (
                    <div key={c.customerId} className="rounded-lg border border-stroke bg-gray-1/30 p-4 dark:border-dark-3 dark:bg-dark-2/20">
                      <div className="flex items-center gap-2">
                        <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
                          {(c.firstName || "?").charAt(0).toUpperCase()}{(c.lastName || "").charAt(0).toUpperCase()}
                        </div>
                        <div>
                          <div className="text-sm font-semibold text-dark dark:text-white">{fullName(c.firstName, c.lastName)}</div>
                          <div className="text-[10px] text-body-color dark:text-dark-6">{fmtProviders(c.providers)}</div>
                        </div>
                      </div>

                      {c.channels?.length > 0 && (
                        <div className="mt-3 space-y-1">
                          <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Kanallar</div>
                          {c.channels.map((ch) => (
                            <div key={`${c.customerId}-${ch.channel}`} className="flex items-center justify-between text-xs">
                              <span className="text-dark dark:text-white">{ch.channel}</span>
                              <span className="text-body-color dark:text-dark-6">{ch.ordersCount} sipariş · {fmtMoney(ch.totalAmount, ch.currency)}</span>
                            </div>
                          ))}
                        </div>
                      )}

                      <div className="mt-3 grid gap-2 sm:grid-cols-2">
                        <div>
                          <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">📦 Teslimat</div>
                          <div className="mt-0.5 text-xs text-dark dark:text-white">{fmtAddress(c.shippingAddress)}</div>
                        </div>
                        <div>
                          <div className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">🧾 Fatura</div>
                          <div className="mt-0.5 text-xs text-dark dark:text-white">{fmtAddress(c.billingAddress)}</div>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      ) : (
        <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-4 flex items-baseline justify-between">
            <div>
              <h3 className="text-lg font-semibold text-dark dark:text-white">Analiz Öncesi Gruplar</h3>
              <p className="mt-1 text-sm text-body-color dark:text-dark-6">Aynı ad soyadlı müşteri grupları (provider bilgisiyle).</p>
            </div>
            <span className="rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-semibold text-gray-600 dark:bg-dark-3 dark:text-dark-6">{pending.length} grup</span>
          </div>

          {pending.length === 0 ? (
            <div className="flex flex-col items-center py-12 text-center">
              <svg className="mb-3 h-12 w-12 text-body-color/30 dark:text-dark-6/30" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}><path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" /></svg>
              <p className="text-sm font-medium text-body-color dark:text-dark-6">Aynı ad soyadlı birden fazla müşteri bulunamadı.</p>
            </div>
          ) : (
            <div className="space-y-3">
              {pending.map((g) => (
                <div key={g.groupKey} className="rounded-lg border border-stroke p-4 dark:border-dark-3">
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-dark dark:text-white">{g.normalizedName}</span>
                    <span className="rounded-full bg-gray-100 px-2 py-0.5 text-[10px] font-semibold text-gray-600 dark:bg-dark-3 dark:text-dark-6">{g.count} kayıt</span>
                  </div>
                  <div className="mt-2 space-y-1">
                    {g.customers.map((c) => (
                      <div key={c.customerId} className="flex items-center justify-between text-sm">
                        <span className="text-dark dark:text-white">{fullName(c.firstName, c.lastName)}</span>
                        <span className="text-xs text-body-color dark:text-dark-6">{fmtProviders(c.providers)}</span>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
