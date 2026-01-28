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
"[project]/src/app/api/auth/login/route.ts [app-route] (ecmascript)", ((__turbopack_context__) => {
"use strict";

__turbopack_context__.s([
    "POST",
    ()=>POST
]);
var __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$server$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__ = __turbopack_context__.i("[project]/node_modules/next/server.js [app-route] (ecmascript)");
var __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$headers$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__ = __turbopack_context__.i("[project]/node_modules/next/headers.js [app-route] (ecmascript)");
;
;
const ACCESS_COOKIE = "profiqo_access_token";
const REFRESH_COOKIE = "profiqo_refresh_token";
const TENANT_COOKIE = "profiqo_tenant_id";
const USER_COOKIE = "profiqo_user_id";
const ROLES_COOKIE = "profiqo_roles";
function backendBaseUrl() {
    return process.env.PROFIQO_BACKEND_URL?.trim() || "http://localhost:5164";
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
function pickAccessToken(payload) {
    const t1 = payload?.tokens?.accessToken || payload?.tokens?.token || payload?.accessToken || payload?.token || payload?.jwt;
    const t2 = payload?.Tokens?.AccessToken || payload?.Tokens?.Token || payload?.AccessToken || payload?.Token || payload?.Jwt;
    return t1 || t2 || null;
}
function pickExpiresAt(payload) {
    const raw = payload?.tokens?.accessTokenExpiresAtUtc || payload?.accessTokenExpiresAtUtc || payload?.expiresAtUtc || payload?.Tokens?.AccessTokenExpiresAtUtc || payload?.AccessTokenExpiresAtUtc || payload?.ExpiresAtUtc;
    if (!raw) return null;
    const d = new Date(raw);
    return Number.isNaN(d.getTime()) ? null : d;
}
function pickUserInfo(payload) {
    const u = payload?.user || payload?.User || null;
    const tenantId = u?.tenantId || u?.TenantId || payload?.tenantId || payload?.TenantId || null;
    const userId = u?.userId || u?.UserId || payload?.userId || payload?.UserId || null;
    const roles = u?.roles || u?.Roles || payload?.roles || payload?.Roles || null;
    return {
        tenantId,
        userId,
        roles
    };
}
// Map numeric role codes -> names
function roleNameFromCode(code) {
    switch(code){
        case "1":
            return "Owner";
        case "2":
            return "Admin";
        case "3":
            return "Reporting";
        case "4":
            return "Integration";
        default:
            return null;
    }
}
function normalizeRolesToString(roles) {
    if (!roles) return null;
    const out = [];
    if (Array.isArray(roles)) {
        for (const r of roles){
            const s = String(r).trim();
            if (!s) continue;
            // already a name?
            if ([
                "Owner",
                "Admin",
                "Reporting",
                "Integration"
            ].includes(s)) {
                out.push(s);
                continue;
            }
            // numeric?
            const mapped = roleNameFromCode(s);
            if (mapped) out.push(mapped);
        }
    } else {
        const s = String(roles).trim();
        if ([
            "Owner",
            "Admin",
            "Reporting",
            "Integration"
        ].includes(s)) out.push(s);
        else {
            const mapped = roleNameFromCode(s);
            if (mapped) out.push(mapped);
        }
    }
    const distinct = Array.from(new Set(out));
    return distinct.length ? distinct.join(",") : null;
}
// Try extract roles from JWT payload if backend didn't return roles.
// This is only for UI gating; signature verification isn't required here.
function tryGetRolesFromJwt(accessToken) {
    try {
        const parts = accessToken.split(".");
        if (parts.length < 2) return null;
        const payloadB64 = parts[1].replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(parts[1].length / 4) * 4, "=");
        const json = Buffer.from(payloadB64, "base64").toString("utf8");
        const payload = JSON.parse(json);
        // common claims patterns
        const candidates = payload?.roles ?? payload?.role ?? payload?.Roles ?? payload?.Role ?? payload?.["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ?? null;
        return normalizeRolesToString(candidates);
    } catch  {
        return null;
    }
}
async function POST(req) {
    const body = await req.json().catch(()=>null);
    const tenantSlug = body?.tenantSlug?.trim() || "";
    const email = body?.email?.trim() || "";
    const password = body?.password || "";
    const remember = Boolean(body?.remember);
    if (!tenantSlug || !email || !password) {
        return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$server$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__["NextResponse"].json({
            ok: false,
            message: "Tenant slug, email ve şifre zorunlu."
        }, {
            status: 400
        });
    }
    const upstream = await fetch(`${backendBaseUrl()}/api/auth/login`, {
        method: "POST",
        headers: {
            "content-type": "application/json"
        },
        cache: "no-store",
        body: JSON.stringify({
            tenantSlug,
            email,
            password
        })
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
        const msg = payload?.message || payload?.title || payload?.detail || "Login başarısız.";
        return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$server$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__["NextResponse"].json({
            ok: false,
            message: msg,
            errors: payload?.errors ?? payload?.extensions?.errors ?? null
        }, {
            status: upstream.status
        });
    }
    const accessToken = pickAccessToken(payload);
    if (!accessToken) {
        return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$server$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__["NextResponse"].json({
            ok: false,
            message: "Backend login response içinde access token yok."
        }, {
            status: 500
        });
    }
    const expiresAt = pickExpiresAt(payload);
    const { tenantId, userId, roles } = pickUserInfo(payload);
    const isProd = ("TURBOPACK compile-time value", "development") === "production";
    const res = __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$server$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__["NextResponse"].json({
        ok: true,
        tenantId,
        userId,
        roles
    });
    // Access token cookie
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
    // Tenant/User cookies
    if (tenantId) {
        res.cookies.set(TENANT_COOKIE, tenantId, {
            httpOnly: true,
            secure: isProd,
            sameSite: "lax",
            path: "/",
            maxAge: 30 * 24 * 60 * 60
        });
    }
    if (userId) {
        res.cookies.set(USER_COOKIE, userId, {
            httpOnly: true,
            secure: isProd,
            sameSite: "lax",
            path: "/",
            maxAge: 30 * 24 * 60 * 60
        });
    }
    // Roles cookie for middleware RBAC
    let rolesStr = normalizeRolesToString(roles);
    if (!rolesStr) {
        rolesStr = tryGetRolesFromJwt(accessToken);
    }
    if (rolesStr) {
        res.cookies.set(ROLES_COOKIE, rolesStr, {
            httpOnly: true,
            secure: isProd,
            sameSite: "lax",
            path: "/",
            maxAge: 30 * 24 * 60 * 60
        });
    } else {
        // clear to avoid stale RBAC
        res.cookies.set(ROLES_COOKIE, "", {
            httpOnly: true,
            secure: isProd,
            sameSite: "lax",
            path: "/",
            maxAge: 0
        });
    }
    // Refresh token handling (backend may not return; if remember false clear)
    if (!remember) {
        res.cookies.set(REFRESH_COOKIE, "", {
            httpOnly: true,
            secure: isProd,
            sameSite: "lax",
            path: "/",
            maxAge: 0
        });
    }
    // keep Next 16 async cookies API happy
    await (0, __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$headers$2e$js__$5b$app$2d$route$5d$__$28$ecmascript$29$__["cookies"])();
    return res;
}
}),
];

//# sourceMappingURL=%5Broot-of-the-server%5D__75f27e91._.js.map