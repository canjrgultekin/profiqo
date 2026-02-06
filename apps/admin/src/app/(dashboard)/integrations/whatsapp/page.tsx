"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type ConnectionInfo = { hasConnection: boolean; connectionId?: string; displayName?: string; wabaId?: string; phoneNumberId?: string; status?: string; isTestMode?: boolean };

async function readJsonOrText(res: Response): Promise<{ json: any | null; text: string }> { const text = await res.text(); try { return { json: text ? JSON.parse(text) : null, text }; } catch { return { json: null, text }; } }

const navCards = [
  { href: "/integrations/whatsapp/templates", icon: "📝", title: "Templates", desc: "Şablon tasarla ve yönet" },
  { href: "/integrations/whatsapp/rules", icon: "⏰", title: "Rules", desc: "Daily veya order-event kuralları" },
  { href: "/integrations/whatsapp/jobs", icon: "🎯", title: "Jobs", desc: "Kural + şablon + hedef" },
  { href: "/integrations/whatsapp/dispatch", icon: "📤", title: "Dispatch", desc: "Queue status ve log" },
];

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
    if (!res.ok) { append(json?.message || `Yükleme başarısız (HTTP ${res.status})`); return; }
    const c: ConnectionInfo = json || { hasConnection: false };
    setInfo(c);
    if (c.hasConnection) { setDisplayName(c.displayName || "WhatsApp"); setWabaId(c.wabaId || ""); setPhoneNumberId(c.phoneNumberId || ""); setIsTestMode(Boolean(c.isTestMode)); setAccessToken(""); append(`Bağlantı yüklendi. status=${c.status} testMode=${c.isTestMode}`); }
    else { append("WhatsApp bağlantısı henüz yapılmamış."); }
  };

  useEffect(() => { load(); }, []);

  const connectOrUpdate = async () => {
    const dn = displayName.trim(); const w = wabaId.trim(); const p = phoneNumberId.trim(); const t = accessToken.trim();
    if (!dn) return alert("Display Name zorunlu"); if (!w) return alert("WABA ID zorunlu"); if (!p) return alert("Phone Number ID zorunlu"); if (!t) return alert("Access Token zorunlu");
    const res = await fetch("/api/integrations/whatsapp/connect", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ displayName: dn, wabaId: w, phoneNumberId: p, accessToken: t, isTestMode }) });
    const { json } = await readJsonOrText(res);
    if (!res.ok) { alert(json?.message || `Bağlantı başarısız (HTTP ${res.status})`); return; }
    append(`Kaydedildi. connectionId=${json?.connectionId}`); setAccessToken(""); await load();
  };

  const test = async () => {
    if (!info.connectionId) return alert("Önce bağlantı kaydedin.");
    const res = await fetch("/api/integrations/whatsapp/test", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ connectionId: info.connectionId }) });
    const { json } = await readJsonOrText(res);
    if (!res.ok) { alert(json?.message || `Test başarısız (HTTP ${res.status})`); return; }
    append(`TEST OK. mode=${json?.mode} verified=${json?.verifiedName ?? "-"} phone=${json?.displayPhoneNumber ?? "-"}`);
  };

  const inputCls = "w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white";

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-6 flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-green-100 text-lg dark:bg-green-900/20">💬</div>
        <div>
          <h2 className="text-xl font-bold text-dark dark:text-white">WhatsApp</h2>
          <p className="text-sm text-body-color dark:text-dark-6">
            {isTestMode ? "⚠️ Test/Development Mode açık — hiçbir gerçek WhatsApp API çağrısı yapılmaz." : "🟢 Production Mode — gerçek mesajlar gönderilecek."}
          </p>
        </div>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Connection Form */}
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="text-lg font-semibold text-dark dark:text-white">Bağlantı</h3>
            <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${info.hasConnection ? "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400" : "bg-gray-100 text-gray-500 dark:bg-dark-3 dark:text-dark-6"}`}>
              {info.hasConnection ? `● ${info.status || "Active"}` : "○ Bağlı Değil"}
            </span>
          </div>
          <div className="grid gap-4">
            <div>
              <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Display Name</label>
              <input className={inputCls} value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
            </div>
            <div>
              <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">WABA ID</label>
              <input className={inputCls} value={wabaId} onChange={(e) => setWabaId(e.target.value)} />
            </div>
            <div>
              <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Phone Number ID</label>
              <input className={inputCls} value={phoneNumberId} onChange={(e) => setPhoneNumberId(e.target.value)} />
            </div>
            <div>
              <label className="mb-1.5 block text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Access Token</label>
              <input type="password" className={inputCls} value={accessToken} onChange={(e) => setAccessToken(e.target.value)} />
              <div className="mt-1 text-[10px] text-body-color/70 dark:text-dark-6/70">Token UI'da saklanmaz. Kaydedildiğinde gönderilir.</div>
            </div>
            <label className="flex cursor-pointer items-center gap-2.5" onClick={() => setIsTestMode(!isTestMode)}>
              <div className={`relative h-5 w-9 rounded-full transition-colors ${isTestMode ? "bg-yellow-500" : "bg-green-500"}`}>
                <div className={`absolute top-0.5 h-4 w-4 rounded-full bg-white shadow transition-transform ${isTestMode ? "translate-x-0.5" : "translate-x-4"}`} />
              </div>
              <span className="text-sm text-dark dark:text-white">{isTestMode ? "Test/Development Mode" : "Production Mode"}</span>
            </label>
            <div className="flex flex-wrap gap-2">
              <button onClick={connectOrUpdate} className="rounded-lg bg-primary px-5 py-2.5 text-sm font-semibold text-white shadow-sm hover:opacity-90">
                {info.hasConnection ? "Güncelle" : "Bağlan"}
              </button>
              <button onClick={test} disabled={!info.hasConnection} className={`rounded-lg px-5 py-2.5 text-sm font-semibold transition-colors ${info.hasConnection ? "border border-stroke text-dark hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2" : "bg-gray-100 text-gray-400 dark:bg-dark-3"}`}>
                Test
              </button>
              <button onClick={load} className="rounded-lg border border-stroke px-5 py-2.5 text-sm font-semibold text-dark hover:bg-gray-1 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2">
                Yenile
              </button>
            </div>
          </div>

          {/* Quick Nav Cards */}
          <div className="mt-5 grid gap-3 sm:grid-cols-2">
            {navCards.map((c) => (
              <button key={c.href} onClick={() => router.push(c.href)}
                className="group rounded-lg border border-stroke p-3 text-left transition-all hover:border-primary hover:shadow-sm dark:border-dark-3 dark:hover:border-primary">
                <div className="flex items-center gap-2">
                  <span className="text-lg">{c.icon}</span>
                  <span className="text-sm font-semibold text-dark group-hover:text-primary dark:text-white">{c.title}</span>
                </div>
                <p className="mt-1 text-xs text-body-color dark:text-dark-6">{c.desc}</p>
              </button>
            ))}
          </div>
        </div>

        {/* Log */}
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-3 text-lg font-semibold text-dark dark:text-white">📋 Log</h3>
          <textarea readOnly className="h-[560px] w-full rounded-lg border border-stroke bg-gray-1/30 px-3.5 py-2.5 font-mono text-xs text-dark outline-none dark:border-dark-3 dark:bg-dark-2/30 dark:text-white" value={log} />
        </div>
      </div>
    </div>
  );
}
