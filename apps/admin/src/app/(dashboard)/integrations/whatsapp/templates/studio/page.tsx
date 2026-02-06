"use client";

import React, { useEffect, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";

type HeaderFormat = "NONE" | "TEXT" | "IMAGE" | "VIDEO" | "DOCUMENT";
type ButtonType = "QUICK_REPLY" | "URL" | "PHONE_NUMBER";

type StudioButton = { type: ButtonType; text: string; url?: string; phone_number?: string };

function normalizeTemplateName(input: string): string {
  const s = (input ?? "").trim().toLowerCase();
  if (!s) return "";
  let out = "";
  for (const ch of s) {
    const isAz = ch >= "a" && ch <= "z";
    const is09 = ch >= "0" && ch <= "9";
    if (isAz || is09) out += ch;
    else if (ch === "_" || ch === "-" || ch === " " || ch === ".") out += "_";
  }
  while (out.includes("__")) out = out.replaceAll("__", "_");
  out = out.replace(/^_+|_+$/g, "");
  if (!out) return "";
  if (!(out[0] >= "a" && out[0] <= "z")) out = "t_" + out;
  return out.slice(0, 512);
}

export default function WhatsappTemplateStudioPage() {
  const router = useRouter();
  const sp = useSearchParams();
  const editId = sp.get("id");

  const [id, setId] = useState<string | null>(editId);
  const [log, setLog] = useState("");
  const append = (s: string) => setLog((x) => (x ? x + "\n" + s : s));

  const [nameRaw, setNameRaw] = useState("");
  const name = useMemo(() => normalizeTemplateName(nameRaw), [nameRaw]);

  const [language, setLanguage] = useState("tr");
  const [category, setCategory] = useState("MARKETING");

  const [headerFormat, setHeaderFormat] = useState<HeaderFormat>("NONE");
  const [headerText, setHeaderText] = useState("");
  const [headerHandle, setHeaderHandle] = useState("");

  const [bodyText, setBodyText] = useState("Merhaba {{1}}, bugün sana özel {{2}} indirim var.");
  const [footerText, setFooterText] = useState("");

  const [buttons, setButtons] = useState<StudioButton[]>([{ type: "QUICK_REPLY", text: "Detay" }]);
  const [advancedJson, setAdvancedJson] = useState("");

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      if (!editId) return;

      const res = await fetch(`/api/whatsapp/templates/${editId}`, { cache: "no-store" });
      const payload = await res.json().catch(() => null);
      if (cancelled) return;

      if (!res.ok) {
        append(payload?.message || "Failed to load template.");
        return;
      }

      setId(payload?.id || editId);
      setNameRaw(payload?.name || "");
      setLanguage(payload?.language || "tr");
      setCategory(payload?.category || "MARKETING");

      // components json array parse edip studio state’e olabildiğince yansıtalım
      try {
        const comps = JSON.parse(payload?.componentsJson || "[]");
        if (Array.isArray(comps)) {
          const hdr = comps.find((x: any) => x?.type === "HEADER");
          const bdy = comps.find((x: any) => x?.type === "BODY");
          const ftr = comps.find((x: any) => x?.type === "FOOTER");
          const btn = comps.find((x: any) => x?.type === "BUTTONS");

          if (bdy?.text) setBodyText(String(bdy.text));
          if (ftr?.text) setFooterText(String(ftr.text));

          if (hdr) {
            const fmt = String(hdr?.format || "TEXT").toUpperCase();
            if (fmt === "TEXT") {
              setHeaderFormat("TEXT");
              setHeaderText(String(hdr?.text || ""));
            } else {
              setHeaderFormat(fmt as any);
              const hh = hdr?.example?.header_handle?.[0];
              setHeaderHandle(hh ? String(hh) : "");
            }
          }

          if (btn?.buttons && Array.isArray(btn.buttons)) {
            const mapped = btn.buttons.map((b: any) => ({
              type: b.type as ButtonType,
              text: String(b.text || ""),
              url: b.url ? String(b.url) : undefined,
              phone_number: b.phone_number ? String(b.phone_number) : undefined,
            }));
            setButtons(mapped.length ? mapped : [{ type: "QUICK_REPLY", text: "Detay" }]);
          }
        }
      } catch {}

      append("Template loaded.");
    };

    load();
    return () => {
      cancelled = true;
    };
  }, [editId]);

  const buildComponents = (): { ok: true; comps: any[] } | { ok: false; error: string } => {
    const comps: any[] = [];

    if (headerFormat !== "NONE") {
      if (headerFormat === "TEXT") {
        const t = headerText.trim();
        if (!t) return { ok: false, error: "Header TEXT boş olamaz." };
        comps.push({ type: "HEADER", format: "TEXT", text: t });
      } else {
        const h = headerHandle.trim();
        if (!h) return { ok: false, error: "Header media için header_handle (dummy) gir." };
        comps.push({ type: "HEADER", format: headerFormat, example: { header_handle: [h] } });
      }
    }

    const bt = bodyText.trim();
    if (!bt) return { ok: false, error: "Body boş olamaz." };
    comps.push({ type: "BODY", text: bt });

    const ft = footerText.trim();
    if (ft) comps.push({ type: "FOOTER", text: ft });

    const clean = buttons
      .map((b) => ({ ...b, text: (b.text || "").trim(), url: (b.url || "").trim(), phone_number: (b.phone_number || "").trim() }))
      .filter((b) => b.text);

    if (clean.length) {
      const mapped = [];
      for (const b of clean) {
        if (b.type === "QUICK_REPLY") mapped.push({ type: "QUICK_REPLY", text: b.text });
        if (b.type === "URL") {
          if (!b.url) return { ok: false, error: "URL button için url zorunlu." };
          mapped.push({ type: "URL", text: b.text, url: b.url });
        }
        if (b.type === "PHONE_NUMBER") {
          if (!b.phone_number) return { ok: false, error: "PHONE_NUMBER button için phone_number zorunlu." };
          mapped.push({ type: "PHONE_NUMBER", text: b.text, phone_number: b.phone_number });
        }
      }
      comps.push({ type: "BUTTONS", buttons: mapped });
    }

    return { ok: true, comps };
  };

  useEffect(() => {
    const built = buildComponents();
    if (!built.ok) {
      setAdvancedJson("");
      return;
    }
    setAdvancedJson(JSON.stringify(built.comps, null, 2));
  }, [headerFormat, headerText, headerHandle, bodyText, footerText, buttons]);

  const addButton = () => setButtons((x) => [...x, { type: "QUICK_REPLY", text: "" }]);
  const removeButton = (idx: number) => setButtons((x) => x.filter((_, i) => i !== idx));
  const updateButton = (idx: number, patch: Partial<StudioButton>) => setButtons((x) => x.map((b, i) => (i === idx ? { ...b, ...patch } : b)));

  const saveDraft = async () => {
    if (!name) return append("Name invalid. Düzelt.");
    if (!advancedJson) return append("Components invalid.");

    const res = await fetch("/api/whatsapp/templates", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        id,
        name,
        language: language.trim().toLowerCase() || "tr",
        category: category.trim().toUpperCase() || "MARKETING",
        componentsJson: advancedJson,
      }),
    });

    const payload = await res.json().catch(() => null);
    if (!res.ok) return append(payload?.message || "Save failed.");

    const newId = payload?.id;
    setId(newId);
    append(`Saved. id=${newId}`);
  };

  return (
    <div className="p-4 sm:p-6">
      <div className="mb-4 flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold text-dark dark:text-white">Template Studio</h2>
          <p className="text-sm text-body-color dark:text-dark-6">
            Connect yok. Draft kaydedilir. Placeholders için {"{{1}}"}, {"{{2}}"} formatını kullan.
          </p>
        </div>
        <button
          onClick={() => router.push("/integrations/whatsapp/templates")}
          className="rounded bg-dark px-4 py-2 text-sm font-semibold text-white hover:opacity-90 dark:bg-white dark:text-dark"
        >
          Back
        </button>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-[10px] border border-stroke bg-white p-4 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-3 text-lg font-semibold text-dark dark:text-white">Meta</h3>

          <div className="grid gap-3">
            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Name</label>
              <input
                className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={nameRaw}
                onChange={(e) => setNameRaw(e.target.value)}
                placeholder="kampanya_haftasonu_2026"
              />
              <div className="mt-1 text-xs text-body-color dark:text-dark-6">
                Normalized: <span className="font-mono">{name || "-"}</span>
              </div>
            </div>

            <div className="grid gap-3 md:grid-cols-2">
              <div>
                <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Language</label>
                <input
                  className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                  value={language}
                  onChange={(e) => setLanguage(e.target.value)}
                  placeholder="tr"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Category</label>
                <input
                  className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                  placeholder="MARKETING"
                />
              </div>
            </div>

            <h3 className="mt-2 text-lg font-semibold text-dark dark:text-white">Components</h3>

            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Header</label>
              <select
                className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={headerFormat}
                onChange={(e) => setHeaderFormat(e.target.value as any)}
              >
                <option value="NONE">None</option>
                <option value="TEXT">Text</option>
                <option value="IMAGE">Image (dummy handle)</option>
                <option value="VIDEO">Video (dummy handle)</option>
                <option value="DOCUMENT">Document (dummy handle)</option>
              </select>
            </div>

            {headerFormat === "TEXT" ? (
              <div>
                <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Header Text</label>
                <input
                  className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                  value={headerText}
                  onChange={(e) => setHeaderText(e.target.value)}
                />
              </div>
            ) : null}

            {headerFormat !== "NONE" && headerFormat !== "TEXT" ? (
              <div>
                <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Header Handle</label>
                <input
                  className="w-full rounded border border-stroke bg-transparent px-3 py-2 font-mono text-xs dark:border-dark-3 dark:text-white"
                  value={headerHandle}
                  onChange={(e) => setHeaderHandle(e.target.value)}
                  placeholder="dummy_handle"
                />
              </div>
            ) : null}

            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Body</label>
              <textarea
                className="h-28 w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={bodyText}
                onChange={(e) => setBodyText(e.target.value)}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs text-body-color dark:text-dark-6">Footer</label>
              <input
                className="w-full rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                value={footerText}
                onChange={(e) => setFooterText(e.target.value)}
              />
            </div>

            <div className="rounded border border-stroke p-3 dark:border-dark-3">
              <div className="mb-2 flex items-center justify-between">
                <div className="text-sm font-semibold text-dark dark:text-white">Buttons</div>
                <button
                  onClick={addButton}
                  className="rounded bg-primary px-3 py-1 text-xs font-semibold text-white hover:opacity-90"
                >
                  Add
                </button>
              </div>

              <div className="grid gap-3">
                {buttons.map((b, idx) => (
                  <div key={idx} className="rounded border border-stroke p-3 dark:border-dark-3">
                    <div className="mb-2 flex items-center justify-between">
                      <div className="text-xs text-body-color dark:text-dark-6">Button #{idx + 1}</div>
                      <button
                        onClick={() => removeButton(idx)}
                        className="rounded bg-red-500/20 px-2 py-1 text-xs text-red-700 dark:text-red-400"
                      >
                        Remove
                      </button>
                    </div>

                    <div className="grid gap-2 md:grid-cols-3">
                      <select
                        className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                        value={b.type}
                        onChange={(e) => updateButton(idx, { type: e.target.value as any })}
                      >
                        <option value="QUICK_REPLY">Quick Reply</option>
                        <option value="URL">URL</option>
                        <option value="PHONE_NUMBER">Phone</option>
                      </select>

                      <input
                        className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                        value={b.text}
                        onChange={(e) => updateButton(idx, { text: e.target.value })}
                        placeholder="Text"
                      />

                      {b.type === "URL" ? (
                        <input
                          className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                          value={b.url || ""}
                          onChange={(e) => updateButton(idx, { url: e.target.value })}
                          placeholder="https://..."
                        />
                      ) : null}

                      {b.type === "PHONE_NUMBER" ? (
                        <input
                          className="rounded border border-stroke bg-transparent px-3 py-2 text-sm dark:border-dark-3 dark:text-white"
                          value={b.phone_number || ""}
                          onChange={(e) => updateButton(idx, { phone_number: e.target.value })}
                          placeholder="+90..."
                        />
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <button
              onClick={saveDraft}
              className="w-fit rounded bg-primary px-4 py-2 text-sm font-semibold text-white hover:opacity-90"
            >
              Save Draft
            </button>
          </div>
        </div>

        <div className="rounded-[10px] border border-stroke bg-white p-4 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <h3 className="mb-3 text-lg font-semibold text-dark dark:text-white">Components JSON</h3>
          <textarea
            readOnly
            className="h-[520px] w-full rounded border border-stroke bg-transparent px-3 py-2 font-mono text-xs dark:border-dark-3 dark:text-white"
            value={advancedJson}
          />

          <div className="mt-3 rounded border border-stroke bg-transparent p-3 dark:border-dark-3">
            <div className="text-sm font-semibold text-dark dark:text-white">Log</div>
            <textarea
              readOnly
              className="mt-2 h-32 w-full rounded border border-stroke bg-transparent px-3 py-2 font-mono text-xs dark:border-dark-3 dark:text-white"
              value={log}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
