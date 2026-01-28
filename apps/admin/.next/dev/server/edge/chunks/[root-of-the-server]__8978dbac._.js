(globalThis.TURBOPACK || (globalThis.TURBOPACK = [])).push(["chunks/[root-of-the-server]__8978dbac._.js",
"[externals]/node:buffer [external] (node:buffer, cjs)", ((__turbopack_context__, module, exports) => {

const mod = __turbopack_context__.x("node:buffer", () => require("node:buffer"));

module.exports = mod;
}),
"[externals]/node:async_hooks [external] (node:async_hooks, cjs)", ((__turbopack_context__, module, exports) => {

const mod = __turbopack_context__.x("node:async_hooks", () => require("node:async_hooks"));

module.exports = mod;
}),
"[project]/src/middleware.ts [middleware-edge] (ecmascript)", ((__turbopack_context__) => {
"use strict";

__turbopack_context__.s([
    "config",
    ()=>config,
    "middleware",
    ()=>middleware
]);
var __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$esm$2f$api$2f$server$2e$js__$5b$middleware$2d$edge$5d$__$28$ecmascript$29$__$3c$locals$3e$__ = __turbopack_context__.i("[project]/node_modules/next/dist/esm/api/server.js [middleware-edge] (ecmascript) <locals>");
var __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$esm$2f$server$2f$web$2f$exports$2f$index$2e$js__$5b$middleware$2d$edge$5d$__$28$ecmascript$29$__ = __turbopack_context__.i("[project]/node_modules/next/dist/esm/server/web/exports/index.js [middleware-edge] (ecmascript)");
;
const ACCESS_COOKIE = "profiqo_access_token";
const ROLES_COOKIE = "profiqo_roles";
const TENANT_COOKIE = "profiqo_tenant_id";
function isPublic(pathname) {
    if (pathname.startsWith("/auth")) return true;
    if (pathname.startsWith("/api")) return true;
    if (pathname.startsWith("/_next")) return true;
    if (pathname === "/favicon.ico") return true;
    return false;
}
function mapRoleToken(s) {
    const t = s.trim();
    if (!t) return null;
    if ([
        "Owner",
        "Admin",
        "Integration",
        "Reporting"
    ].includes(t)) return t;
    if (t === "1") return "Owner";
    if (t === "2") return "Admin";
    if (t === "3") return "Reporting";
    if (t === "4") return "Integration";
    return null;
}
function parseRoles(raw) {
    const set = new Set();
    if (!raw) return set;
    raw.split(",").map((x)=>mapRoleToken(x)).filter((x)=>Boolean(x)).forEach((x)=>set.add(x));
    return set;
}
function hasAnyRole(roles, allowed) {
    return allowed.some((r)=>roles.has(r));
}
// Edge-safe base64url decode (no Buffer)
function b64UrlDecode(input) {
    try {
        const b64 = input.replace(/-/g, "+").replace(/_/g, "/");
        const padded = b64.padEnd(Math.ceil(b64.length / 4) * 4, "=");
        return atob(padded);
    } catch  {
        return null;
    }
}
function isJwtExpired(token) {
    // token: header.payload.signature
    const parts = token.split(".");
    if (parts.length < 2) return false;
    const json = b64UrlDecode(parts[1]);
    if (!json) return false;
    try {
        const payload = JSON.parse(json);
        const exp = payload?.exp;
        // exp typically seconds
        if (typeof exp === "number") {
            const nowSec = Math.floor(Date.now() / 1000);
            return exp <= nowSec;
        }
        return false;
    } catch  {
        return false;
    }
}
function redirectToSignIn(req) {
    const url = req.nextUrl.clone();
    url.pathname = "/auth/sign-in";
    const res = __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$esm$2f$server$2f$web$2f$exports$2f$index$2e$js__$5b$middleware$2d$edge$5d$__$28$ecmascript$29$__["NextResponse"].redirect(url);
    // clear auth cookies immediately to prevent flash / loops
    res.cookies.set(ACCESS_COOKIE, "", {
        path: "/",
        maxAge: 0
    });
    res.cookies.set(TENANT_COOKIE, "", {
        path: "/",
        maxAge: 0
    });
    res.cookies.set(ROLES_COOKIE, "", {
        path: "/",
        maxAge: 0
    });
    return res;
}
function middleware(req) {
    const { pathname } = req.nextUrl;
    if (isPublic(pathname)) {
        return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$esm$2f$server$2f$web$2f$exports$2f$index$2e$js__$5b$middleware$2d$edge$5d$__$28$ecmascript$29$__["NextResponse"].next();
    }
    const token = req.cookies.get(ACCESS_COOKIE)?.value;
    // No token => no render, immediate redirect
    if (!token) {
        return redirectToSignIn(req);
    }
    // Token exists but expired => clear + redirect BEFORE any page render
    if (isJwtExpired(token)) {
        return redirectToSignIn(req);
    }
    const roles = parseRoles(req.cookies.get(ROLES_COOKIE)?.value);
    // Owner-only
    if (pathname.startsWith("/settings/users")) {
        if (!hasAnyRole(roles, [
            "Owner"
        ])) {
            const url = req.nextUrl.clone();
            url.pathname = "/403";
            return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$esm$2f$server$2f$web$2f$exports$2f$index$2e$js__$5b$middleware$2d$edge$5d$__$28$ecmascript$29$__["NextResponse"].redirect(url);
        }
    }
    // Integrations
    if (pathname.startsWith("/integrations")) {
        if (!hasAnyRole(roles, [
            "Owner",
            "Admin",
            "Integration"
        ])) {
            const url = req.nextUrl.clone();
            url.pathname = "/403";
            return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$esm$2f$server$2f$web$2f$exports$2f$index$2e$js__$5b$middleware$2d$edge$5d$__$28$ecmascript$29$__["NextResponse"].redirect(url);
        }
    }
    // Reporting
    if (pathname.startsWith("/customers") || pathname.startsWith("/orders") || pathname.startsWith("/reports")) {
        if (!hasAnyRole(roles, [
            "Owner",
            "Admin",
            "Integration",
            "Reporting"
        ])) {
            const url = req.nextUrl.clone();
            url.pathname = "/403";
            return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$esm$2f$server$2f$web$2f$exports$2f$index$2e$js__$5b$middleware$2d$edge$5d$__$28$ecmascript$29$__["NextResponse"].redirect(url);
        }
    }
    return __TURBOPACK__imported__module__$5b$project$5d2f$node_modules$2f$next$2f$dist$2f$esm$2f$server$2f$web$2f$exports$2f$index$2e$js__$5b$middleware$2d$edge$5d$__$28$ecmascript$29$__["NextResponse"].next();
}
const config = {
    matcher: [
        "/((?!_next/static|_next/image|favicon.ico).*)"
    ]
};
}),
]);

//# sourceMappingURL=%5Broot-of-the-server%5D__8978dbac._.js.map