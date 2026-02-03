"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type ConnectionInfo = {
  hasConnection: boolean;
  connectionId?: string;
  displayName?: string;
  wabaId?: string;
  phoneNumberId?: string;
  status?: string;
  isTestMode?: boolean;
};

async function readJsonOrText(res: Response): Promise<{ json: any | null; text: string }> {
  const text = await res.text();
  try {
    return { json: text ? JSON.parse(text) : null, text };
  } catch {
    return { json: null, text };
  }
}

export default function WhatsappIntegrationPage() {
  const router = useRouter();

  const [info, setInfo] = useState<ConnectionInfo>({ hasConnection: false });

  const [displayName, setDisplayName] = useState("WhatsApp");
  const [wabaId, setWabaId] = useState("");
  const [phoneNumberId, setPhoneNumberId] = useState("");
  const [accessToken, setAccessToken] = useState("");
  const [isTestMode, setIsTestMode] = useState(true);

  const [log, setLog] = useState("");
  const append = (s: string) => setLog((x) => (x ? x + "\n" + s : s));

  const load = async () => {
    const res = await fetch("/api/integrations/whatsapp/connection", { cache: "no-store" });
    const { json } = await readJsonOrText(res);

    if (!res.ok) {
      append(json?.message || `Load failed (HTTP ${res.status})`);
      return;
    }

    const c: ConnectionInfo = json || { hasConnection: false };
    setInfo(c);

    if (c.hasConnection) {
      setDisplayName(c.displayName || "WhatsApp");
      setWabaId(c.wabaId || "");
      setPhoneNumberId(c.phoneNumberId || "");
      setIsTestMode(Boolean(c.isTestMode));
      setAccessToken(""); // token asla geri doldurulmaz
      append(`Loaded connection. status=${c.status} testMode=${c.isTestMode}`);
    } else {
      append("No WhatsApp connection yet.");
    }
  };

  useEffect(() => {
    load();
  }, []);

  const connectOrUpdate = async () => {
    const dn = displayName.trim();
    const w = wabaId.trim();
    const p = phoneNumberId.trim();
    const t = accessToken.trim();

    if (!dn) return alert("DisplayName zorunlu");
    if (!w) return alert("WabaId zorunlu");
    if (!p) return alert("PhoneNumberId zorunlu");
    if (!t) return alert("AccessToken zorunlu");

    const res = await fetch("/api/integrations/whatsapp/connect", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        displayName: dn,
        wabaId: w,
        phoneNumberId: p,
        accessToken: t,
        isTestMode,
      }),
    });

    const { json } = await readJsonOrText(res);

    if (!res.ok) {
      alert(json?.message || `Connect failed (HTTP ${res.status})`);
      return;
    }

    append(`Saved. connectionId=${json?.connectionId}`);
    setAccessToken("");
    await load();
  };

  const test = async () => {
    if (!info.connectionId) return alert("Önce connect kaydı gerekli.");

    const res = await fetch("/api/integrations/whatsapp/test", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ connectionId: info.connectionId }),
    });

    const { json } = await readJsonOrText(res);

    if (!res.ok) {
      alert(json?.message || `Test failed (HTTP ${res.status})`);
      return;
    }

    append(
      `TEST OK. mode=${json?.mode} verified=${json?.verifiedName ?? "-"} displayPhone=${json?.displayPhoneNumber ?? "-"}`
    );
  };

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4">
        <h2 className="text-xl font-semibold text-dark dark:text-white">WhatsApp</h2>
        <p className="text-sm text-body-color dark:text-dark-6">
          Test/Development Mode açıkken hiçbir WhatsApp API çağrısı yapılmaz. Prod’da kapatıp gerçek credentials ile çalışırsın.
        </p>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-3 flex items-center justify-between">
            <h3 className="text-lg font-semibold text-dark dark:text-white">Connection</h3>
            <span
              className={`text-xs px-2 py-1 rounded ${
                info.hasConnection ? "bg-green-500/20 text-green-700" : "bg-gray-500/20 text-gray-700"
              }`}
            >
              {info.hasConnection ? (info.status || "Connected") : "Not connected"}
            </span>
          </div>

          <div className="grid gap-3">
            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Display Name</label>
              <input
                className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">WABA ID</label>
              <input
                className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={wabaId}
                onChange={(e) => setWabaId(e.target.value)}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Phone Number ID</label>
              <input
                className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={phoneNumberId}
                onChange={(e) => setPhoneNumberId(e.target.value)}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Access Token</label>
              <input
                type="password"
                className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={accessToken}
                onChange={(e) => setAccessToken(e.target.value)}
              />
              <div className="mt-1 text-xs text-body-color dark:text-dark-6">
                Token UI’da tutulmaz. Kaydederken gönderilir, geri okunmaz.
              </div>
            </div>

            <label className="flex items-center gap-2 text-sm text-body-color dark:text-dark-6">
              <input type="checkbox" checked={isTestMode} onChange={(e) => setIsTestMode(e.target.checked)} />
              Test/Development Mode
            </label>

            <div className="flex gap-2">
              <button
                onClick={connectOrUpdate}
                className="rounded bg-primary px-4 py-2 text-sm font-semibold text-white hover:opacity-90"
              >
                {info.hasConnection ? "Update" : "Connect"}
              </button>

              <button
                onClick={test}
                disabled={!info.hasConnection}
                className={`rounded px-4 py-2 text-sm font-semibold ${
                  info.hasConnection ? "bg-dark text-white hover:opacity-90 dark:bg-white dark:text-dark" : "bg-gray-300 text-gray-500"
                }`}
              >
                Test
              </button>

              <button
                onClick={load}
                className="rounded bg-dark px-4 py-2 text-sm font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark"
              >
                Refresh
              </button>
            </div>
          </div>

          <div className="mt-4 grid gap-3 md:grid-cols-2">
            <button
              onClick={() => router.push("/integrations/whatsapp/templates")}
              className="rounded-[10px] border border-stroke bg-white p-4 text-left shadow-1 hover:border-primary dark:border-dark-3 dark:bg-gray-dark dark:shadow-card"
            >
              <div className="text-base font-semibold text-dark dark:text-white">Templates</div>
              <div className="mt-1 text-sm text-body-color dark:text-dark-6">Şablon tasarla</div>
            </button>

            <button
              onClick={() => router.push("/integrations/whatsapp/rules")}
              className="rounded-[10px] border border-stroke bg-white p-4 text-left shadow-1 hover:border-primary dark:border-dark-3 dark:bg-gray-dark dark:shadow-card"
            >
              <div className="text-base font-semibold text-dark dark:text-white">Rules</div>
              <div className="mt-1 text-sm text-body-color dark:text-dark-6">Daily veya order-event</div>
            </button>

            <button
              onClick={() => router.push("/integrations/whatsapp/jobs")}
              className="rounded-[10px] border border-stroke bg-white p-4 text-left shadow-1 hover:border-primary dark:border-dark-3 dark:bg-gray-dark dark:shadow-card"
            >
              <div className="text-base font-semibold text-dark dark:text-white">Jobs</div>
              <div className="mt-1 text-sm text-body-color dark:text-dark-6">Kural + şablon + hedef</div>
            </button>

            <button
              onClick={() => router.push("/integrations/whatsapp/dispatch")}
              className="rounded-[10px] border border-stroke bg-white p-4 text-left shadow-1 hover:border-primary dark:border-dark-3 dark:bg-gray-dark dark:shadow-card"
            >
              <div className="text-base font-semibold text-dark dark:text-white">Dispatch</div>
              <div className="mt-1 text-sm text-body-color dark:text-dark-6">Queue status ve log</div>
            </button>
          </div>
        </div>

        <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-2 text-lg font-semibold text-dark dark:text-white">Log</h3>
          <textarea
            readOnly
            className="h-[560px] w-full rounded border border-stroke bg-transparent px-3 py-2 font-mono text-xs dark:border-dark-3 dark:text-white"
            value={log}
          />
        </div>
      </div>
    </div>
  );
}
