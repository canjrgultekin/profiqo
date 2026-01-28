(globalThis.TURBOPACK || (globalThis.TURBOPACK = [])).push([typeof document === "object" ? document.currentScript : undefined,
"[project]/src/app/integrations/ikas/page.tsx [app-client] (ecmascript)", ((__turbopack_context__) => {
"use strict";

// Path: apps/admin/src/app/integrations/ikas/page.tsx
__turbopack_context__.s([
    "default",
    ()=>IkasIntegrationPage
]);
var __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__ = __turbopack_context__.i("[project]/node_modules/next/dist/compiled/react/jsx-dev-runtime.js [app-client] (ecmascript)");
var __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__ = __turbopack_context__.i("[project]/node_modules/next/dist/compiled/react/index.js [app-client] (ecmascript)");
;
var _s = __turbopack_context__.k.signature();
"use client";
;
function normalizeStartSync(payload) {
    const batchId = payload?.batchId ?? payload?.BatchId ?? "";
    const jobsRaw = payload?.jobs ?? payload?.Jobs ?? [];
    const jobs = Array.isArray(jobsRaw) ? jobsRaw.map((x)=>({
            jobId: x?.jobId ?? x?.JobId ?? "",
            kind: x?.kind ?? x?.Kind ?? ""
        })) : [];
    return {
        batchId,
        jobs
    };
}
function normalizeBatch(payload) {
    const batchId = payload?.batchId ?? payload?.BatchId ?? "";
    const jobsRaw = payload?.jobs ?? payload?.Jobs ?? [];
    const jobs = Array.isArray(jobsRaw) ? jobsRaw.map((j)=>({
            jobId: j?.jobId ?? j?.JobId ?? "",
            batchId: j?.batchId ?? j?.BatchId ?? batchId,
            tenantId: j?.tenantId ?? j?.TenantId ?? "",
            connectionId: j?.connectionId ?? j?.ConnectionId ?? "",
            kind: String(j?.kind ?? j?.Kind ?? ""),
            status: String(j?.status ?? j?.Status ?? ""),
            pageSize: Number(j?.pageSize ?? j?.PageSize ?? 0),
            maxPages: Number(j?.maxPages ?? j?.MaxPages ?? 0),
            processedItems: Number(j?.processedItems ?? j?.ProcessedItems ?? 0),
            createdAtUtc: String(j?.createdAtUtc ?? j?.CreatedAtUtc ?? ""),
            startedAtUtc: j?.startedAtUtc ?? j?.StartedAtUtc ?? null,
            finishedAtUtc: j?.finishedAtUtc ?? j?.FinishedAtUtc ?? null,
            lastError: j?.lastError ?? j?.LastError ?? null
        })) : [];
    return {
        batchId,
        jobs
    };
}
function IkasIntegrationPage() {
    _s();
    const [storeLabel, setStoreLabel] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])("");
    const [storeDomain, setStoreDomain] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])("");
    const [accessToken, setAccessToken] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])("");
    const [hasExisting, setHasExisting] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])(false);
    const [connectionId, setConnectionId] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])(null);
    const [log, setLog] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])("");
    const append = (s)=>setLog((x)=>x ? x + "\n" + s : s);
    const [batchId, setBatchId] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])(null);
    const [batch, setBatch] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])(null);
    const [polling, setPolling] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])(false);
    const pollTimer = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useRef"])(null);
    const [pageSize, setPageSize] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])(50);
    const [maxPages, setMaxPages] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])(20);
    // Default "both" now means: Customers + Orders + Abandoned
    const [scope, setScope] = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useState"])("both");
    const anyRunning = (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useMemo"])({
        "IkasIntegrationPage.useMemo[anyRunning]": ()=>{
            const jobs = batch?.jobs || [];
            return jobs.some({
                "IkasIntegrationPage.useMemo[anyRunning]": (j)=>j.status === "Queued" || j.status === "Running"
            }["IkasIntegrationPage.useMemo[anyRunning]"]);
        }
    }["IkasIntegrationPage.useMemo[anyRunning]"], [
        batch
    ]);
    (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useEffect"])({
        "IkasIntegrationPage.useEffect": ()=>{
            let cancelled = false;
            const load = {
                "IkasIntegrationPage.useEffect.load": async ()=>{
                    const res = await fetch("/api/integrations/ikas/connection", {
                        method: "GET",
                        cache: "no-store"
                    });
                    const payload = await res.json().catch({
                        "IkasIntegrationPage.useEffect.load": ()=>null
                    }["IkasIntegrationPage.useEffect.load"]);
                    if (cancelled) return;
                    if (!res.ok) {
                        append(`LOAD CONNECTION ERROR: ${payload?.message || JSON.stringify(payload)}`);
                        return;
                    }
                    const hasConnection = Boolean(payload?.hasConnection ?? payload?.HasConnection);
                    if (!hasConnection) {
                        setHasExisting(false);
                        setConnectionId(null);
                        return;
                    }
                    setHasExisting(true);
                    const cid = payload?.connectionId ?? payload?.ConnectionId;
                    setConnectionId(cid);
                    setStoreLabel(payload?.displayName ?? payload?.DisplayName ?? "");
                    setStoreDomain(payload?.externalAccountId ?? payload?.ExternalAccountId ?? "");
                    setAccessToken("");
                    append(`Existing Ikas connection loaded. connectionId=${cid}`);
                }
            }["IkasIntegrationPage.useEffect.load"];
            load();
            return ({
                "IkasIntegrationPage.useEffect": ()=>{
                    cancelled = true;
                }
            })["IkasIntegrationPage.useEffect"];
        // eslint-disable-next-line react-hooks/exhaustive-deps
        }
    }["IkasIntegrationPage.useEffect"], []);
    const connectOrUpdate = async ()=>{
        setBatchId(null);
        setBatch(null);
        if (hasExisting && !accessToken.trim()) {
            append("UPDATE TOKEN ERROR: Mevcut connection var. Token güncellemek için Access Token girmen lazım.");
            return;
        }
        const res = await fetch("/api/integrations/ikas/connect", {
            method: "POST",
            headers: {
                "content-type": "application/json"
            },
            body: JSON.stringify({
                storeLabel,
                storeDomain,
                accessToken
            })
        });
        const payload = await res.json().catch(()=>null);
        if (!res.ok) {
            append(`CONNECT/UPDATE ERROR: ${payload?.message || JSON.stringify(payload)}`);
            return;
        }
        const cid = payload?.connectionId ?? payload?.ConnectionId;
        setConnectionId(cid);
        setHasExisting(true);
        setAccessToken("");
        append(`${hasExisting ? "Updated" : "Connected"}. connectionId=${cid}`);
    };
    const test = async ()=>{
        if (!connectionId) return;
        const res = await fetch("/api/integrations/ikas/test", {
            method: "POST",
            headers: {
                "content-type": "application/json"
            },
            body: JSON.stringify({
                connectionId
            })
        });
        const payload = await res.json().catch(()=>null);
        if (!res.ok) {
            append(`TEST ERROR: ${payload?.message || JSON.stringify(payload)}`);
            return;
        }
        append(`Test OK. meId=${payload?.meId ?? payload?.MeId}`);
    };
    const startSync = async ()=>{
        if (!connectionId) return;
        setBatchId(null);
        setBatch(null);
        const res = await fetch("/api/integrations/ikas/sync/start", {
            method: "POST",
            headers: {
                "content-type": "application/json"
            },
            body: JSON.stringify({
                connectionId,
                scope,
                pageSize,
                maxPages
            })
        });
        const payload = await res.json().catch(()=>null);
        if (!res.ok) {
            append(`START SYNC ERROR: ${payload?.message || JSON.stringify(payload)}`);
            return;
        }
        const r = normalizeStartSync(payload);
        if (!r.batchId) {
            append(`START SYNC ERROR: batchId missing. payload=${JSON.stringify(payload)}`);
            return;
        }
        setBatchId(r.batchId);
        append(`Sync started. batchId=${r.batchId}`);
        if (r.jobs?.length) {
            append(`Jobs: ${r.jobs.map((j)=>`${j.kind}:${j.jobId}`).join(", ")}`);
        } else {
            append("Jobs: (empty)");
        }
        setPolling(true);
    };
    const fetchBatch = async (id)=>{
        const res = await fetch(`/api/integrations/ikas/jobs/batch/${id}`, {
            method: "GET",
            cache: "no-store"
        });
        const payload = await res.json().catch(()=>null);
        if (!res.ok) {
            append(`POLL ERROR: ${payload?.message || JSON.stringify(payload)}`);
            return null;
        }
        return normalizeBatch(payload);
    };
    (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$index$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["useEffect"])({
        "IkasIntegrationPage.useEffect": ()=>{
            if (!polling || !batchId) return;
            let cancelled = false;
            const tick = {
                "IkasIntegrationPage.useEffect.tick": async ()=>{
                    if (cancelled) return;
                    const b = await fetchBatch(batchId);
                    if (b) {
                        setBatch(b);
                        const running = (b.jobs || []).some({
                            "IkasIntegrationPage.useEffect.tick.running": (j)=>j.status === "Queued" || j.status === "Running"
                        }["IkasIntegrationPage.useEffect.tick.running"]);
                        if (!running) {
                            setPolling(false);
                            append(`Batch finished. batchId=${batchId}`);
                        }
                    }
                }
            }["IkasIntegrationPage.useEffect.tick"];
            tick();
            pollTimer.current = window.setInterval({
                "IkasIntegrationPage.useEffect": ()=>tick()
            }["IkasIntegrationPage.useEffect"], 2000);
            return ({
                "IkasIntegrationPage.useEffect": ()=>{
                    cancelled = true;
                    if (pollTimer.current) {
                        window.clearInterval(pollTimer.current);
                        pollTimer.current = null;
                    }
                }
            })["IkasIntegrationPage.useEffect"];
        // eslint-disable-next-line react-hooks/exhaustive-deps
        }
    }["IkasIntegrationPage.useEffect"], [
        polling,
        batchId
    ]);
    return /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
        className: "p-4 sm:p-6",
        children: /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
            className: "rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card",
            children: [
                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("h2", {
                    className: "mb-4 text-lg font-semibold text-dark dark:text-white",
                    children: "Ikas Integration"
                }, void 0, false, {
                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                    lineNumber: 276,
                    columnNumber: 9
                }, this),
                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                    className: "grid gap-3 md:grid-cols-2",
                    children: [
                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                            children: [
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("label", {
                                    className: "text-sm text-body-color dark:text-dark-6",
                                    children: "Store Label"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 280,
                                    columnNumber: 13
                                }, this),
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("input", {
                                    className: "w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white",
                                    value: storeLabel,
                                    onChange: (e)=>setStoreLabel(e.target.value),
                                    placeholder: "Örn: profiqo"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 281,
                                    columnNumber: 13
                                }, this)
                            ]
                        }, void 0, true, {
                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                            lineNumber: 279,
                            columnNumber: 11
                        }, this),
                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                            children: [
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("label", {
                                    className: "text-sm text-body-color dark:text-dark-6",
                                    children: "Store Domain (opsiyonel)"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 290,
                                    columnNumber: 13
                                }, this),
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("input", {
                                    className: "w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white",
                                    value: storeDomain,
                                    onChange: (e)=>setStoreDomain(e.target.value),
                                    placeholder: "Örn: https://www.example.com"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 291,
                                    columnNumber: 13
                                }, this)
                            ]
                        }, void 0, true, {
                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                            lineNumber: 289,
                            columnNumber: 11
                        }, this),
                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                            className: "md:col-span-2",
                            children: [
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("label", {
                                    className: "text-sm text-body-color dark:text-dark-6",
                                    children: [
                                        "Access Token ",
                                        hasExisting ? "(stored, paste here to rotate/update)" : ""
                                    ]
                                }, void 0, true, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 300,
                                    columnNumber: 13
                                }, this),
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("textarea", {
                                    className: "min-h-[120px] w-full rounded-lg border border-stroke bg-transparent px-4 py-2 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white",
                                    value: accessToken,
                                    onChange: (e)=>setAccessToken(e.target.value),
                                    placeholder: hasExisting ? "Token DB'de var. Güncellemek istersen buraya yeni token yapıştır." : "Bearer token"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 303,
                                    columnNumber: 13
                                }, this)
                            ]
                        }, void 0, true, {
                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                            lineNumber: 299,
                            columnNumber: 11
                        }, this)
                    ]
                }, void 0, true, {
                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                    lineNumber: 278,
                    columnNumber: 9
                }, this),
                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                    className: "mt-4 flex flex-wrap gap-2",
                    children: [
                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("button", {
                            className: "rounded-lg bg-primary px-4 py-2 font-medium text-white hover:bg-opacity-90",
                            onClick: connectOrUpdate,
                            children: hasExisting ? "Update Token" : "Connect"
                        }, void 0, false, {
                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                            lineNumber: 313,
                            columnNumber: 11
                        }, this),
                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("button", {
                            className: "rounded-lg border border-stroke px-4 py-2 text-dark dark:border-dark-3 dark:text-white",
                            onClick: test,
                            disabled: !connectionId,
                            children: "Test Connection"
                        }, void 0, false, {
                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                            lineNumber: 317,
                            columnNumber: 11
                        }, this),
                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                            className: "ml-auto flex items-center gap-2",
                            children: [
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("label", {
                                    className: "text-sm text-body-color dark:text-dark-6",
                                    children: "Scope"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 322,
                                    columnNumber: 13
                                }, this),
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("select", {
                                    value: scope,
                                    onChange: (e)=>setScope(e.target.value),
                                    className: "rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm text-dark outline-none dark:border-dark-3 dark:text-white",
                                    children: [
                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("option", {
                                            value: "both",
                                            children: "All (Customers + Orders + Abandoned)"
                                        }, void 0, false, {
                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                            lineNumber: 328,
                                            columnNumber: 15
                                        }, this),
                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("option", {
                                            value: "customers",
                                            children: "Customers"
                                        }, void 0, false, {
                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                            lineNumber: 329,
                                            columnNumber: 15
                                        }, this),
                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("option", {
                                            value: "orders",
                                            children: "Orders"
                                        }, void 0, false, {
                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                            lineNumber: 330,
                                            columnNumber: 15
                                        }, this),
                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("option", {
                                            value: "abandoned",
                                            children: "Abandoned Carts"
                                        }, void 0, false, {
                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                            lineNumber: 331,
                                            columnNumber: 15
                                        }, this)
                                    ]
                                }, void 0, true, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 323,
                                    columnNumber: 13
                                }, this),
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("label", {
                                    className: "text-sm text-body-color dark:text-dark-6",
                                    children: "PageSize"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 334,
                                    columnNumber: 13
                                }, this),
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("input", {
                                    type: "number",
                                    value: pageSize,
                                    onChange: (e)=>setPageSize(Number(e.target.value || 50)),
                                    className: "w-[90px] rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm text-dark outline-none dark:border-dark-3 dark:text-white"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 335,
                                    columnNumber: 13
                                }, this),
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("label", {
                                    className: "text-sm text-body-color dark:text-dark-6",
                                    children: "MaxPages"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 342,
                                    columnNumber: 13
                                }, this),
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("input", {
                                    type: "number",
                                    value: maxPages,
                                    onChange: (e)=>setMaxPages(Number(e.target.value || 20)),
                                    className: "w-[90px] rounded-lg border border-stroke bg-transparent px-3 py-2 text-sm text-dark outline-none dark:border-dark-3 dark:text-white"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 343,
                                    columnNumber: 13
                                }, this),
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("button", {
                                    className: "rounded-lg border border-stroke px-4 py-2 text-dark dark:border-dark-3 dark:text-white",
                                    onClick: startSync,
                                    disabled: !connectionId || polling || anyRunning,
                                    children: polling || anyRunning ? "Sync Running..." : "Start Sync"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 350,
                                    columnNumber: 13
                                }, this)
                            ]
                        }, void 0, true, {
                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                            lineNumber: 321,
                            columnNumber: 11
                        }, this)
                    ]
                }, void 0, true, {
                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                    lineNumber: 312,
                    columnNumber: 9
                }, this),
                batchId && /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                    className: "mt-4 rounded-lg border border-stroke p-3 dark:border-dark-3",
                    children: [
                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                            className: "mb-2 flex items-center justify-between",
                            children: [
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                                    className: "text-sm text-dark dark:text-white",
                                    children: [
                                        "Batch: ",
                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("span", {
                                            className: "font-mono text-xs",
                                            children: batchId
                                        }, void 0, false, {
                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                            lineNumber: 364,
                                            columnNumber: 24
                                        }, this)
                                    ]
                                }, void 0, true, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 363,
                                    columnNumber: 15
                                }, this),
                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                                    className: "text-sm text-body-color dark:text-dark-6",
                                    children: polling ? "Polling..." : "Idle"
                                }, void 0, false, {
                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                    lineNumber: 366,
                                    columnNumber: 15
                                }, this)
                            ]
                        }, void 0, true, {
                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                            lineNumber: 362,
                            columnNumber: 13
                        }, this),
                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("div", {
                            className: "overflow-x-auto",
                            children: /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("table", {
                                className: "w-full table-auto",
                                children: [
                                    /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("thead", {
                                        children: /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("tr", {
                                            className: "text-left text-xs text-body-color dark:text-dark-6",
                                            children: [
                                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("th", {
                                                    className: "px-2 py-2",
                                                    children: "Kind"
                                                }, void 0, false, {
                                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                    lineNumber: 373,
                                                    columnNumber: 21
                                                }, this),
                                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("th", {
                                                    className: "px-2 py-2",
                                                    children: "Status"
                                                }, void 0, false, {
                                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                    lineNumber: 374,
                                                    columnNumber: 21
                                                }, this),
                                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("th", {
                                                    className: "px-2 py-2",
                                                    children: "Processed"
                                                }, void 0, false, {
                                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                    lineNumber: 375,
                                                    columnNumber: 21
                                                }, this),
                                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("th", {
                                                    className: "px-2 py-2",
                                                    children: "Started"
                                                }, void 0, false, {
                                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                    lineNumber: 376,
                                                    columnNumber: 21
                                                }, this),
                                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("th", {
                                                    className: "px-2 py-2",
                                                    children: "Finished"
                                                }, void 0, false, {
                                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                    lineNumber: 377,
                                                    columnNumber: 21
                                                }, this),
                                                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("th", {
                                                    className: "px-2 py-2",
                                                    children: "Error"
                                                }, void 0, false, {
                                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                    lineNumber: 378,
                                                    columnNumber: 21
                                                }, this)
                                            ]
                                        }, void 0, true, {
                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                            lineNumber: 372,
                                            columnNumber: 19
                                        }, this)
                                    }, void 0, false, {
                                        fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                        lineNumber: 371,
                                        columnNumber: 17
                                    }, this),
                                    /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("tbody", {
                                        children: [
                                            (batch?.jobs || []).map((j)=>/*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("tr", {
                                                    className: "border-t border-stroke dark:border-dark-3 text-xs",
                                                    children: [
                                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("td", {
                                                            className: "px-2 py-2",
                                                            children: j.kind
                                                        }, void 0, false, {
                                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                            lineNumber: 384,
                                                            columnNumber: 23
                                                        }, this),
                                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("td", {
                                                            className: "px-2 py-2",
                                                            children: j.status
                                                        }, void 0, false, {
                                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                            lineNumber: 385,
                                                            columnNumber: 23
                                                        }, this),
                                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("td", {
                                                            className: "px-2 py-2",
                                                            children: j.processedItems
                                                        }, void 0, false, {
                                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                            lineNumber: 386,
                                                            columnNumber: 23
                                                        }, this),
                                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("td", {
                                                            className: "px-2 py-2",
                                                            children: j.startedAtUtc ? new Date(j.startedAtUtc).toLocaleString() : "-"
                                                        }, void 0, false, {
                                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                            lineNumber: 387,
                                                            columnNumber: 23
                                                        }, this),
                                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("td", {
                                                            className: "px-2 py-2",
                                                            children: j.finishedAtUtc ? new Date(j.finishedAtUtc).toLocaleString() : "-"
                                                        }, void 0, false, {
                                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                            lineNumber: 388,
                                                            columnNumber: 23
                                                        }, this),
                                                        /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("td", {
                                                            className: "px-2 py-2",
                                                            children: j.lastError || "-"
                                                        }, void 0, false, {
                                                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                            lineNumber: 389,
                                                            columnNumber: 23
                                                        }, this)
                                                    ]
                                                }, j.jobId, true, {
                                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                    lineNumber: 383,
                                                    columnNumber: 21
                                                }, this)),
                                            !batch?.jobs?.length && /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("tr", {
                                                className: "border-t border-stroke dark:border-dark-3 text-xs",
                                                children: /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("td", {
                                                    className: "px-2 py-2",
                                                    colSpan: 6,
                                                    children: "Jobs not loaded yet..."
                                                }, void 0, false, {
                                                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                    lineNumber: 394,
                                                    columnNumber: 23
                                                }, this)
                                            }, void 0, false, {
                                                fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                                lineNumber: 393,
                                                columnNumber: 21
                                            }, this)
                                        ]
                                    }, void 0, true, {
                                        fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                        lineNumber: 381,
                                        columnNumber: 17
                                    }, this)
                                ]
                            }, void 0, true, {
                                fileName: "[project]/src/app/integrations/ikas/page.tsx",
                                lineNumber: 370,
                                columnNumber: 15
                            }, this)
                        }, void 0, false, {
                            fileName: "[project]/src/app/integrations/ikas/page.tsx",
                            lineNumber: 369,
                            columnNumber: 13
                        }, this)
                    ]
                }, void 0, true, {
                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                    lineNumber: 361,
                    columnNumber: 11
                }, this),
                /*#__PURE__*/ (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$compiled$2f$react$2f$jsx$2d$dev$2d$runtime$2e$js__$5b$app$2d$client$5d$__$28$ecmascript$29$__["jsxDEV"])("pre", {
                    className: "mt-4 whitespace-pre-wrap rounded-lg border border-stroke bg-transparent p-3 text-xs text-dark dark:border-dark-3 dark:text-white",
                    children: log || "Logs..."
                }, void 0, false, {
                    fileName: "[project]/src/app/integrations/ikas/page.tsx",
                    lineNumber: 403,
                    columnNumber: 9
                }, this)
            ]
        }, void 0, true, {
            fileName: "[project]/src/app/integrations/ikas/page.tsx",
            lineNumber: 275,
            columnNumber: 7
        }, this)
    }, void 0, false, {
        fileName: "[project]/src/app/integrations/ikas/page.tsx",
        lineNumber: 274,
        columnNumber: 5
    }, this);
}
_s(IkasIntegrationPage, "mM44JeFmb4K+QVgy0LgkNyo5cXU=");
_c = IkasIntegrationPage;
var _c;
__turbopack_context__.k.register(_c, "IkasIntegrationPage");
if (typeof globalThis.$RefreshHelpers$ === 'object' && globalThis.$RefreshHelpers !== null) {
    __turbopack_context__.k.registerExports(__turbopack_context__.m, globalThis.$RefreshHelpers$);
}
}),
]);

//# sourceMappingURL=src_app_integrations_ikas_page_tsx_e757f0b6._.js.map