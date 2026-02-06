"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type TemplateDto = {
  id: string;
  name: string;
  language: string;
  category: string;
  status: string;
  updatedAtUtc: string;
};

function statusBadge(s: string): { label: string; className: string } {
  const lower = (s || "").toLowerCase();
  if (lower === "approved")
    return { label: "Onaylı", className: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3" };
  if (lower === "draft")
    return { label: "Taslak", className: "bg-yellow-light-4 text-yellow-dark-2 dark:bg-yellow-dark/10 dark:text-yellow-light" };
  if (lower === "rejected")
    return { label: "Reddedildi", className: "bg-red-light-6 text-red-dark dark:bg-red/10 dark:text-red-light" };
  return { label: s, className: "bg-gray-200 text-dark-4 dark:bg-dark-3 dark:text-dark-6" };
}

function categoryBadge(c: string): string {
  const lower = (c || "").toLowerCase();
  if (lower === "marketing") return "bg-primary/10 text-primary";
  if (lower === "utility") return "bg-blue/10 text-blue";
  return "bg-gray-200 text-dark-4 dark:bg-dark-3 dark:text-dark-6";
}

export default function WhatsappTemplatesPage() {
  const router = useRouter();
  const [items, setItems] = useState<TemplateDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    setErr(null);
    setLoading(true);
    const res = await fetch("/api/whatsapp/templates", { cache: "no-store" });
    const payload = await res.json().catch(() => null);
    if (!res.ok) {
      setErr(payload?.message || "Failed to load templates.");
      setItems([]);
      setLoading(false);
      return;
    }
    setItems(payload?.items || []);
    setLoading(false);
  };

  useEffect(() => {
    load();
  }, []);

  const del = async (id: string) => {
    if (!confirm("Bu şablonu silmek istediğinize emin misiniz?")) return;
    const res = await fetch(`/api/whatsapp/templates/${id}`, {
      method: "DELETE",
    });
    if (!res.ok) {
      const p = await res.json().catch(() => null);
      alert(p?.message || "Silme başarısız");
      return;
    }
    await load();
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-dark dark:text-white">
            WhatsApp Şablonları
          </h1>
          <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
            Mesaj şablonlarınızı oluşturun ve yönetin.
          </p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={load}
            className="inline-flex items-center gap-1.5 rounded-lg border border-stroke px-3.5 py-2.5 text-sm font-medium text-dark transition-colors hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M16.023 9.348h4.992v-.001M2.985 19.644v-4.992m0 0h4.992m-4.993 0l3.181 3.183a8.25 8.25 0 0013.803-3.7M4.031 9.865a8.25 8.25 0 0113.803-3.7l3.181 3.182m0-4.991v4.99" />
            </svg>
            Yenile
          </button>
          <button
            onClick={() =>
              router.push("/integrations/whatsapp/templates/studio")
            }
            className="inline-flex items-center gap-1.5 rounded-lg bg-primary px-5 py-2.5 text-sm font-medium text-white transition-opacity hover:opacity-90"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
            </svg>
            Yeni Şablon
          </button>
        </div>
      </div>

      {err && (
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <svg className="h-5 w-5 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-red-600 dark:text-red-300">{err}</p>
        </div>
      )}

      {/* Table */}
      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex items-center justify-between border-b border-stroke px-5 py-4 dark:border-dark-3">
          <h2 className="text-base font-semibold text-dark dark:text-white">
            Şablon Listesi
          </h2>
          <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">
            {items.length} şablon
          </span>
        </div>

        {loading ? (
          <div className="flex items-center justify-center py-12">
            <div className="h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          </div>
        ) : items.length === 0 ? (
          <div className="px-5 py-16 text-center text-sm text-dark-5 dark:text-dark-6">
            <div className="flex flex-col items-center gap-3">
              <svg className="h-12 w-12 text-dark-7 dark:text-dark-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
              </svg>
              <p className="font-medium text-dark dark:text-white">Henüz şablon yok</p>
              <p className="text-xs">İlk şablonunuzu oluşturmak için "Yeni Şablon" butonuna tıklayın.</p>
            </div>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-stroke text-left text-xs font-medium uppercase tracking-wider text-dark-5 dark:border-dark-3 dark:text-dark-6">
                  <th className="px-5 py-3">Şablon Adı</th>
                  <th className="px-4 py-3">Dil</th>
                  <th className="px-4 py-3">Kategori</th>
                  <th className="px-4 py-3">Durum</th>
                  <th className="px-4 py-3 text-right">İşlemler</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-stroke dark:divide-dark-3">
                {items.map((x) => {
                  const sb = statusBadge(x.status);
                  return (
                    <tr
                      key={x.id}
                      className="text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2"
                    >
                      <td className="px-5 py-3.5">
                        <div className="flex items-center gap-3">
                          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-green/10 text-green">
                            💬
                          </div>
                          <span className="font-medium text-dark dark:text-white">
                            {x.name}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3.5 text-dark-5 dark:text-dark-6 uppercase">
                        {x.language}
                      </td>
                      <td className="px-4 py-3.5">
                        <span
                          className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${categoryBadge(x.category)}`}
                        >
                          {x.category}
                        </span>
                      </td>
                      <td className="px-4 py-3.5">
                        <span
                          className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${sb.className}`}
                        >
                          {sb.label}
                        </span>
                      </td>
                      <td className="px-4 py-3.5 text-right">
                        <div className="flex items-center justify-end gap-2">
                          <button
                            onClick={() =>
                              router.push(
                                `/integrations/whatsapp/templates/studio?id=${encodeURIComponent(x.id)}`
                              )
                            }
                            className="rounded-lg border border-stroke px-3 py-1.5 text-xs font-medium text-dark transition-colors hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2"
                          >
                            Düzenle
                          </button>
                          <button
                            onClick={() => del(x.id)}
                            className="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-medium text-red transition-colors hover:bg-red-50 dark:border-red/20 dark:hover:bg-red/10"
                          >
                            Sil
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
