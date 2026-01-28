module.exports = [
"[externals]/next/dist/compiled/next-server/app-route-turbo.runtime.dev.js [external] (next/dist/compiled/next-server/app-route-turbo.runtime.dev.js, cjs)", ((__turbopack_context__, module, exports) => {

const mod = __turbopack_context__.x("next/dist/compiled/next-server/app-route-turbo.runtime.dev.js", () => require("next/dist/compiled/next-server/app-route-turbo.runtime.dev.js"));

module.exports = mod;
}),
"[externals]/next/dist/compiled/@opentelemetry/api [external] (next/dist/compiled/@opentelemetry/api, cjs)", ((__turbopack_context__, module, exports) => {

const mod = __turbopack_context__.x("next/dist/compiled/@opentelemetry/api", () => require("next/dist/compiled/@opentelemetry/api"));

module.exports = mod;
}),
"[externals]/next/dist/compiled/next-server/app-page-turbo.runtime.dev.js [external] (next/dist/compiled/next-server/app-page-turbo.runtime.dev.js, cjs)", ((__turbopack_context__, module, exports) => {

const mod = __turbopack_context__.x("next/dist/compiled/next-server/app-page-turbo.runtime.dev.js", () => require("next/dist/compiled/next-server/app-page-turbo.runtime.dev.js"));

module.exports = mod;
}),
"[externals]/next/dist/server/app-render/work-unit-async-storage.external.js [external] (next/dist/server/app-render/work-unit-async-storage.external.js, cjs)", ((__turbopack_context__, module, exports) => {

const mod = __turbopack_context__.x("next/dist/server/app-render/work-unit-async-storage.external.js", () => require("next/dist/server/app-render/work-unit-async-storage.external.js"));

module.exports = mod;
}),
"[externals]/next/dist/server/app-render/work-async-storage.external.js [external] (next/dist/server/app-render/work-async-storage.external.js, cjs)", ((__turbopack_context__, module, exports) => {

const mod = __turbopack_context__.x("next/dist/server/app-render/work-async-storage.external.js", () => require("next/dist/server/app-render/work-async-storage.external.js"));

module.exports = mod;
}),
"[externals]/next/dist/shared/lib/no-fallback-error.external.js [external] (next/dist/shared/lib/no-fallback-error.external.js, cjs)", ((__turbopack_context__, module, exports) => {

const mod = __turbopack_context__.x("next/dist/shared/lib/no-fallback-error.external.js", () => require("next/dist/shared/lib/no-fallback-error.external.js"));

module.exports = mod;
}),
"[externals]/next/dist/server/app-render/after-task-async-storage.external.js [external] (next/dist/server/app-render/after-task-async-storage.external.js, cjs)", ((__turbopack_context__, module, exports) => {

const mod = __turbopack_context__.x("next/dist/server/app-render/after-task-async-storage.external.js", () => require("next/dist/server/app-render/after-task-async-storage.external.js"));

module.exports = mod;
}),
"[project]/src/app/api/auth/register/route.ts [app-route] (ecmascript)", ((__turbopack_context__) => {
"use strict";

__turbopack_context__.s([
    "POST",
    ()=>POST
]);
var __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$server$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__ = __turbopack_context__.i("[project]/node_modules/next/server.js [app-route] (ecmascript)");
;
const ACCESS_COOKIE = "profiqo_access_token";
const REFRESH_COOKIE = "profiqo_refresh_token";
const TENANT_COOKIE = "profiqo_tenant_id";
const USER_COOKIE = "profiqo_user_id";
function backendBaseUrl() {
    return process.env.PROFIQO_BACKEND_URL?.trim() || "http://localhost:5164";
}
function parseExpiryDate(payload) {
    const raw = payload.expiresAtUtc || payload.accessTokenExpiresAtUtc;
    if (!raw) return null;
    const d = new Date(raw);
    if (Number.isNaN(d.getTime())) return null;
    return d;
}
async function safeReadJson(res) {
    const text = await res.text();
    if (!text) return null;
    try {
        return JSON.parse(text);
    } catch  {
        return {
            message: text
        };
    }
}
async function POST(req) {
    const body = await req.json().catch(()=>null);
    const tenantName = body?.tenantName?.trim() || "";
    const tenantSlug = body?.tenantSlug?.trim() || "";
    const displayName = body?.displayName?.trim() || "";
    const email = body?.email?.trim() || "";
    const password = body?.password || "";
    const remember = Boolean(body?.remember);
    if (!tenantName || !tenantSlug || !email || !password) {
        return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$server$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__["NextResponse"].json({
            ok: false,
            message: "Tenant adı, tenant slug, email ve şifre zorunlu."
        }, {
            status: 400
        });
    }
    // Backend’in beklediği net payload
    const upstreamBody = {
        tenantName,
        tenantSlug,
        ownerEmail: email,
        ownerPassword: password,
        ownerDisplayName: displayName || email.split("@")[0]
    };
    const upstream = await fetch(`${backendBaseUrl()}/api/auth/register`, {
        method: "POST",
        headers: {
            "content-type": "application/json"
        },
        cache: "no-store",
        body: JSON.stringify(upstreamBody)
    }).catch(()=>null);
    if (!upstream) {
        return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$server$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__["NextResponse"].json({
            ok: false,
            message: "Backend erişilemiyor (network)."
        }, {
            status: 502
        });
    }
    const payload = await safeReadJson(upstream);
    if (!upstream.ok) {
        // Backend validation detail’i varsa UI’da göster
        const msg = payload?.message || payload?.title || payload?.detail || "Register başarısız.";
        return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$server$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__["NextResponse"].json({
            ok: false,
            message: msg,
            errors: payload?.errors ?? payload?.extensions?.errors ?? null
        }, {
            status: upstream.status
        });
    }
    const accessToken = payload?.accessToken || payload?.token || payload?.jwt;
    const refreshToken = payload?.refreshToken;
    const isProd = ("TURBOPACK compile-time value", "development") === "production";
    const res = __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$server$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__["NextResponse"].json({
        ok: true,
        tenantId: payload?.tenantId ?? null,
        userId: payload?.userId ?? null,
        roles: payload?.roles ?? null,
        hasTokens: Boolean(accessToken)
    });
    if (accessToken) {
        const expiresAt = parseExpiryDate(payload);
        if (expiresAt) {
            res.cookies.set(ACCESS_COOKIE, accessToken, {
                httpOnly: true,
                secure: isProd,
                sameSite: "lax",
                path: "/",
                expires: expiresAt
            });
        } else {
            res.cookies.set(ACCESS_COOKIE, accessToken, {
                httpOnly: true,
                secure: isProd,
                sameSite: "lax",
                path: "/",
                maxAge: 60 * 60
            });
        }
        if (remember && refreshToken) {
            res.cookies.set(REFRESH_COOKIE, refreshToken, {
                httpOnly: true,
                secure: isProd,
                sameSite: "lax",
                path: "/",
                maxAge: 30 * 24 * 60 * 60
            });
        } else {
            res.cookies.set(REFRESH_COOKIE, "", {
                httpOnly: true,
                secure: isProd,
                sameSite: "lax",
                path: "/",
                maxAge: 0
            });
        }
        if (payload?.tenantId) {
            res.cookies.set(TENANT_COOKIE, payload.tenantId, {
                httpOnly: true,
                secure: isProd,
                sameSite: "lax",
                path: "/",
                maxAge: 30 * 24 * 60 * 60
            });
        }
        if (payload?.userId) {
            res.cookies.set(USER_COOKIE, payload.userId, {
                httpOnly: true,
                secure: isProd,
                sameSite: "lax",
                path: "/",
                maxAge: 30 * 24 * 60 * 60
            });
        }
    }
    return res;
}
}),
];

//# sourceMappingURL=%5Broot-of-the-server%5D__f41e639a._.js.map