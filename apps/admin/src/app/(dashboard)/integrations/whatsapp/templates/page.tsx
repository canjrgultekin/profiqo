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

export default function WhatsappTemplatesPage() {
  const router = useRouter();
  const [items, setItems] = useState<TemplateDto[]>([]);
  const [err, setErr] = useState<string | null>(null);

  const load = async () => {
    setErr(null);
    const res = await fetch("/api/whatsapp/templates", { cache: "no-store" });
    const payload = await res.json().catch(() => null);
    if (!res.ok) {
      setErr(payload?.message || "Failed to load templates.");
      setItems([]);
      return;
    }
    setItems(payload?.items || []);
  };

  useEffect(() => {
    load();
  }, []);

  const del = async (id: string) => {
    if (!confirm("Delete template?")) return;
    const res = await fetch(`/api/whatsapp/templates/${id}`, { method: "DELETE" });
    if (!res.ok) {
      const p = await res.json().catch(() => null);
      alert(p?.message || "Delete failed");
      return;
    }
    await load();
  };

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4 flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold text-dark dark:text-white">WhatsApp Templates</h2>
          <p className="text-sm text-body-color dark:text-dark-6">Local draft templates.</p>
        </div>
        <button
          onClick={() => router.push("/integrations/whatsapp/templates/studio")}
          className="rounded bg-primary px-4 py-2 text-sm font-semibold text-white hover:opacity-90"
        >
          New Template
        </button>
      </div>

      {err && (
        <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-600 dark:text-red-400">
          {err}
        </div>
      )}

      <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        {items.length === 0 ? (
          <div className="text-sm text-body-color dark:text-dark-6">No templates.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full table-auto">
              <thead>
                <tr className="text-left text-xs text-body-color dark:text-dark-6">
                  <th className="px-2 py-2">Name</th>
                  <th className="px-2 py-2">Lang</th>
                  <th className="px-2 py-2">Category</th>
                  <th className="px-2 py-2">Status</th>
                  <th className="px-2 py-2">Actions</th>
                </tr>
              </thead>
              <tbody>
                {items.map((x) => (
                  <tr key={x.id} className="border-t border-stroke dark:border-dark-3 text-sm">
                    <td className="px-2 py-2">{x.name}</td>
                    <td className="px-2 py-2">{x.language}</td>
                    <td className="px-2 py-2">{x.category}</td>
                    <td className="px-2 py-2">{x.status}</td>
                    <td className="px-2 py-2 flex gap-2">
                      <button
                        onClick={() => router.push(`/integrations/whatsapp/templates/studio?id=${encodeURIComponent(x.id)}`)}
                        className="rounded bg-dark px-3 py-1 text-xs font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark"
                      >
                        Edit
                      </button>
                      <button
                        onClick={() => del(x.id)}
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
      </div>

      <div className="mt-4">
        <button
          onClick={load}
          className="rounded bg-dark px-4 py-2 text-sm font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark"
        >
          Refresh
        </button>
      </div>
    </div>
  );
}
