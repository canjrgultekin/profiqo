"use client";

import Breadcrumb from "@/components/Breadcrumbs/Breadcrumb";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { useCallback, useEffect, useMemo, useState } from "react";

type PendingCustomer = {
  customerId: string;
  firstName: string | null;
  lastName: string | null;
  providers: string[];
};

type PendingGroup = {
  groupKey: string;
  normalizedName: string;
  count: number;
  customers: PendingCustomer[];
};

type Address = {
  fullName?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  district?: string | null;
  city?: string | null;
  postalCode?: string | null;
  country?: string | null;
};

type ChannelRow = {
  channel: string;
  ordersCount: number;
  totalAmount: number;
  currency: string;
};

type SuggestionCandidate = {
  customerId: string; // Guid string
  firstName: string | null;
  lastName: string | null;
  providers: string[];
  channels: ChannelRow[];
  shippingAddress: Address | null;
  billingAddress: Address | null;
};

type SuggestionGroup = {
  groupKey: string;
  confidence: number;
  normalizedName: string;
  rationale: string;
  candidates: SuggestionCandidate[];
};

function fullName(first?: string | null, last?: string | null) {
  return `${(first ?? "").trim()} ${(last ?? "").trim()}`.trim() || "(İsimsiz)";
}

function fmtProviders(p?: string[]) {
  if (!p || p.length === 0) return "(provider yok)";
  return p.join(", ");
}

function fmtAddress(a: Address | null) {
  if (!a) return "(adres yok)";
  const parts = [
    a.addressLine1,
    a.addressLine2,
    a.district,
    a.city,
    a.postalCode,
    a.country,
  ]
    .map((x) => (x ?? "").trim())
    .filter(Boolean);

  return parts.length ? parts.join(", ") : "(adres yok)";
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

  const analyzeDisabled = useMemo(() => analyzing || deciding || suggestions.length > 0, [
    analyzing,
    deciding,
    suggestions.length,
  ]);

  const loadAll = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const [pendingRes, suggestionsRes] = await Promise.all([
        fetch("/api/customers/dedupe/pending", { cache: "no-store" }),
        fetch("/api/customers/dedupe/suggestions/details", { cache: "no-store" }),
      ]);

      if (!pendingRes.ok) throw new Error((await pendingRes.text()) || "Pending alınamadı");
      if (!suggestionsRes.ok) throw new Error((await suggestionsRes.text()) || "Suggestions alınamadı");

      const pendingJson = await pendingRes.json();
      const suggestionsJson = await suggestionsRes.json();

      setPending((pendingJson.items ?? []) as PendingGroup[]);
      setSuggestions((suggestionsJson.items ?? []) as SuggestionGroup[]);
    } catch (e: any) {
      setError(e?.message ?? "Beklenmeyen hata");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  const runAnalyze = useCallback(async () => {
    setAnalyzing(true);
    setError(null);
    setInfo(null);

    try {
      const res = await fetch("/api/customers/dedupe/analyze", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ threshold }),
      });

      if (!res.ok) throw new Error((await res.text()) || "Analiz başarısız");

      await loadAll();
      setInfo("Analiz tamamlandı. Onayla veya reddet.");
    } catch (e: any) {
      setError(e?.message ?? "Beklenmeyen hata");
    } finally {
      setAnalyzing(false);
    }
  }, [threshold, loadAll]);

  const decide = useCallback(
    async (groupKey: string, action: "approve" | "reject") => {
      setDeciding(true);
      setError(null);
      setInfo(null);

      try {
        const res = await fetch(
          `/api/customers/dedupe/suggestions/${encodeURIComponent(groupKey)}/${action}`,
          { method: "POST" },
        );

        if (!res.ok) throw new Error((await res.text()) || "İşlem başarısız");

        await loadAll();
        setInfo(action === "approve" ? "Onaylandı. Artık tüm ekranlarda tek müşteri." : "Reddedildi.");
      } catch (e: any) {
        setError(e?.message ?? "Beklenmeyen hata");
      } finally {
        setDeciding(false);
      }
    },
    [loadAll],
  );

  const decideAll = useCallback(
    async (action: "approve" | "reject") => {
      if (suggestions.length === 0) return;

      setDeciding(true);
      setError(null);
      setInfo(null);

      try {
        for (const g of suggestions) {
          const res = await fetch(
            `/api/customers/dedupe/suggestions/${encodeURIComponent(g.groupKey)}/${action}`,
            { method: "POST" },
          );
          if (!res.ok) throw new Error((await res.text()) || "İşlem başarısız");
        }

        await loadAll();
        setInfo(action === "approve" ? "Tüm gruplar onaylandı." : "Tüm gruplar reddedildi.");
      } catch (e: any) {
        setError(e?.message ?? "Beklenmeyen hata");
      } finally {
        setDeciding(false);
      }
    },
    [suggestions, loadAll],
  );

  return (
    <div className="space-y-6">
      <Breadcrumb pageName="Müşteri Tekilleştirme (Dedupe)" />

      {analyzing && (
        <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-xl dark:bg-gray-dark">
            <div className="text-lg font-semibold">Analiz ediliyor…</div>
            <div className="mt-2 text-sm text-gray-600 dark:text-gray-300">
              Fuzzy benzeştirme çalışıyor, bitince otomatik kapanacak.
            </div>
            <div className="mt-4 h-2 w-full overflow-hidden rounded bg-gray-200 dark:bg-gray-700">
              <div className="h-full w-1/2 animate-pulse bg-primary" />
            </div>
          </div>
        </div>
      )}

      <Card className="p-6">
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
          <div className="space-y-2">
            <div className="text-lg font-semibold">Analiz</div>
            <div className="text-sm text-gray-600 dark:text-gray-300">
              Aynı ad soyad gruplarını adres benzerliğine göre öneriye çevirir.
              Öneriler karar verilene kadar analiz butonu pasif.
            </div>
          </div>

          <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
            <label className="flex flex-col gap-1 text-sm">
              Threshold
              <input
                type="number"
                min={0}
                max={1}
                step={0.01}
                value={threshold}
                onChange={(e) => setThreshold(Number(e.target.value))}
                className="w-32 rounded-md border border-gray-200 bg-white px-3 py-2 text-sm outline-none dark:border-gray-700 dark:bg-gray-900"
              />
            </label>

            <div className="flex gap-2">
              <Button onClick={runAnalyze} disabled={analyzeDisabled}>
                Analiz Et
              </Button>

              {suggestions.length > 0 && (
                <>
                  <Button onClick={() => void decideAll("approve")} disabled={deciding}>
                    Onayla
                  </Button>
                  <Button variant="destructive" onClick={() => void decideAll("reject")} disabled={deciding}>
                    Reddet
                  </Button>
                </>
              )}
            </div>
          </div>
        </div>

        <div className="mt-4 space-y-2">
          {error && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900/40 dark:bg-red-900/20 dark:text-red-200">
              {error}
            </div>
          )}
          {info && (
            <div className="rounded-lg border border-green-200 bg-green-50 p-3 text-sm text-green-700 dark:border-green-900/40 dark:bg-green-900/20 dark:text-green-200">
              {info}
            </div>
          )}
        </div>
      </Card>

      {loading ? (
        <Card className="p-6">
          <div className="text-sm text-gray-600 dark:text-gray-300">Yükleniyor…</div>
        </Card>
      ) : suggestions.length > 0 ? (
        <Card className="p-6">
          <div className="flex items-baseline justify-between">
            <div>
              <div className="text-lg font-semibold">Analiz Sonucu</div>
              <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">
                Onaylarsan customers ve orders dahil her yerde tek müşteri görünür.
              </div>
            </div>
            <div className="text-sm text-gray-600 dark:text-gray-300">{suggestions.length} grup</div>
          </div>

          <div className="mt-4 space-y-4">
            {suggestions.map((g) => (
              <div key={g.groupKey} className="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
                <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                  <div>
                    <div className="text-base font-semibold">{g.normalizedName}</div>
                    <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">
                      Güven: <span className="font-medium">{Math.round(g.confidence * 100)}%</span>
                    </div>
                    <div className="mt-2 text-sm text-gray-700 dark:text-gray-200">{g.rationale}</div>
                  </div>

                  <div className="flex gap-2">
                    <Button onClick={() => void decide(g.groupKey, "approve")} disabled={deciding}>
                      Onayla
                    </Button>
                    <Button variant="destructive" onClick={() => void decide(g.groupKey, "reject")} disabled={deciding}>
                      Reddet
                    </Button>
                  </div>
                </div>

                <div className="mt-4 grid gap-3 md:grid-cols-2">
                  {g.candidates.map((c) => (
                    <div key={c.customerId} className="rounded-lg bg-gray-50 p-4 text-sm dark:bg-gray-900">
                      <div className="font-semibold">{fullName(c.firstName, c.lastName)}</div>
                      <div className="mt-1 text-gray-600 dark:text-gray-300">
                        Provider: <span className="font-medium">{fmtProviders(c.providers)}</span>
                      </div>

                      <div className="mt-3">
                        <div className="font-medium">Kanallar</div>
                        {c.channels?.length ? (
                          <div className="mt-1 space-y-1 text-gray-700 dark:text-gray-200">
                            {c.channels.map((ch) => (
                              <div key={`${c.customerId}-${ch.channel}`} className="flex justify-between">
                                <span>{ch.channel}</span>
                                <span className="text-gray-600 dark:text-gray-300">{ch.ordersCount} sipariş</span>
                              </div>
                            ))}
                          </div>
                        ) : (
                          <div className="mt-1 text-gray-600 dark:text-gray-300">(sipariş yok)</div>
                        )}
                      </div>

                      <div className="mt-3">
                        <div className="font-medium">Teslimat</div>
                        <div className="mt-1 text-gray-700 dark:text-gray-200">{fmtAddress(c.shippingAddress)}</div>
                      </div>

                      <div className="mt-3">
                        <div className="font-medium">Fatura</div>
                        <div className="mt-1 text-gray-700 dark:text-gray-200">{fmtAddress(c.billingAddress)}</div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </Card>
      ) : (
        <Card className="p-6">
          <div className="flex items-baseline justify-between">
            <div>
              <div className="text-lg font-semibold">Analiz Öncesi</div>
              <div className="mt-1 text-sm text-gray-600 dark:text-gray-300">
                Aynı ad soyadlı müşteri grupları (provider bilgisiyle).
              </div>
            </div>
            <div className="text-sm text-gray-600 dark:text-gray-300">{pending.length} grup</div>
          </div>

          <div className="mt-4 space-y-3">
            {pending.length === 0 ? (
              <div className="text-sm text-gray-600 dark:text-gray-300">
                Aynı ad soyadlı birden fazla müşteri bulunamadı.
              </div>
            ) : (
              pending.map((g) => (
                <div key={g.groupKey} className="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
                  <div className="font-semibold">{g.normalizedName}</div>
                  <div className="mt-2 space-y-1 text-sm">
                    {g.customers.map((c) => (
                      <div key={c.customerId} className="flex flex-col gap-1 md:flex-row md:justify-between">
                        <div className="text-gray-800 dark:text-gray-100">{fullName(c.firstName, c.lastName)}</div>
                        <div className="text-gray-600 dark:text-gray-300">{fmtProviders(c.providers)}</div>
                      </div>
                    ))}
                  </div>
                </div>
              ))
            )}
          </div>
        </Card>
      )}
    </div>
  );
}
