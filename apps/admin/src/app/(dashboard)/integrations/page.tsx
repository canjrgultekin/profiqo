"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type StatusItem = {
  provider: string;
  connectionId: string;
  status: string;
  displayName: string;
  externalAccountId: string | null;
  healthy: boolean;
  cursors?: {
    customersUpdatedAtMs?: string | null;
    ordersUpdatedAtMs?: string | null;
  };
};

export default function IntegrationsHubPage() {
  const router = useRouter();
  const [items, setItems] = useState<StatusItem[]>([]);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      setErr(null);
      const res = await fetch("/api/integrations/status", { cache: "no-store" });
      const payload = await res.json().catch(() => null);

      if (cancelled) return;

      if (!res.ok) {
        setErr(payload?.message || "Failed to load integrations status.");
        setItems([]);
        return;
      }

      setItems(payload?.items || []);
    };

    load();

    return () => {
      cancelled = true;
    };
  }, []);

  const ikas = items.find((x) => x.provider === "ikas");
  const trendyol = items.find((x) => x.provider === "trendyol");
  const whatsapp = items.find((x) => x.provider === "whatsapp");

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4">
        <h2 className="text-xl font-semibold text-dark dark:text-white">Integrations</h2>
        <p className="text-sm text-body-color dark:text-dark-6">
          Provider connectorlarını buradan yönet.
        </p>
      </div>

      {err && (
        <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-600 dark:text-red-400">
          {err}
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-3">
        <button
          onClick={() => router.push("/integrations/ikas")}
          className="rounded-[10px] border border-stroke bg-white p-4 text-left shadow-1 hover:border-primary dark:border-dark-3 dark:bg-gray-dark dark:shadow-card"
        >
          <div className="flex items-center justify-between">
            <div className="text-lg font-semibold text-dark dark:text-white">ikas</div>
            <div className={`text-xs px-2 py-1 rounded ${ikas?.healthy ? "bg-green-500/20 text-green-700" : "bg-gray-500/20 text-gray-700"}`}>
              {ikas?.healthy ? "Connected" : "Not connected"}
            </div>
          </div>
          <div className="mt-2 text-sm text-body-color dark:text-dark-6">
            Admin API connector
          </div>
        </button>

		<button
		  onClick={() => router.push("/integrations/trendyol")}
		  className="rounded-[10px] border border-stroke bg-white p-4 text-left shadow-1 hover:border-primary dark:border-dark-3 dark:bg-gray-dark dark:shadow-card"
		>
		  <div className="flex items-center justify-between">
			<div className="text-lg font-semibold text-dark dark:text-white">Trendyol</div>
			<div className={`text-xs px-2 py-1 rounded ${trendyol?.healthy ? "bg-green-500/20 text-green-700" : "bg-gray-500/20 text-gray-700"}`}>
			  {trendyol?.healthy ? "Connected" : "Not connected"}
			</div>
		  </div>
		  <div className="mt-2 text-sm text-body-color dark:text-dark-6">Marketplace connector</div>
		</button>

		<button
		  onClick={() => router.push("/integrations/whatsapp")}
		  className="rounded-[10px] border border-stroke bg-white p-4 text-left shadow-1 hover:border-primary dark:border-dark-3 dark:bg-gray-dark dark:shadow-card"
		>
		  <div className="flex items-center justify-between">
			<div className="text-lg font-semibold text-dark dark:text-white">WhatsApp Push</div>
			<div className="text-xs px-2 py-1 rounded bg-blue-500/20 text-blue-700">
			  Local mode
			</div>
		  </div>
		  <div className="mt-2 text-sm text-body-color dark:text-dark-6">
			Templates + Rules + Jobs + Dummy send
		  </div>
		</button>




      </div>

      <div className="mt-6 rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <div className="mb-3 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-dark dark:text-white">Connected Providers</h3>
          <span className="text-sm text-body-color dark:text-dark-6">{items.length} item</span>
        </div>

        {items.length === 0 ? (
          <div className="text-sm text-body-color dark:text-dark-6">No active integrations.</div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full table-auto">
              <thead>
                <tr className="text-left text-xs text-body-color dark:text-dark-6">
                  <th className="px-2 py-2">Provider</th>
                  <th className="px-2 py-2">Status</th>
                  <th className="px-2 py-2">Store</th>
                  <th className="px-2 py-2">Last cursor</th>
                </tr>
              </thead>
              <tbody>
                {items.map((x) => (
                  <tr key={x.connectionId} className="border-t border-stroke dark:border-dark-3 text-sm">
                    <td className="px-2 py-2">{x.provider}</td>
                    <td className="px-2 py-2">
                      <span className={`text-xs px-2 py-1 rounded ${x.healthy ? "bg-green-500/20 text-green-700" : "bg-gray-500/20 text-gray-700"}`}>
                        {x.healthy ? "Healthy" : x.status}
                      </span>
                    </td>
                    <td className="px-2 py-2">{x.displayName || "-"}</td>
                    <td className="px-2 py-2 text-xs text-body-color dark:text-dark-6">
                      cust:{x.cursors?.customersUpdatedAtMs || "-"} ord:{x.cursors?.ordersUpdatedAtMs || "-"}
                    </td>
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
