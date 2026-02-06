import { NextResponse } from "next/server";
import { cookies } from "next/headers";

type LoginRequest = {
  tenantSlug?: string;
  email?: string;
  password?: string;
  remember?: boolean;
};

const ACCESS_COOKIE = "profiqo_access_token";
const REFRESH_COOKIE = "profiqo_refresh_token";
const TENANT_COOKIE = "profiqo_tenant_id";
const USER_COOKIE = "profiqo_user_id";
const ROLES_COOKIE = "profiqo_roles";
const DISPLAY_NAME_COOKIE = "profiqo_display_name";
const EMAIL_COOKIE = "profiqo_email";

function backendBaseUrl(): string {
  return process.env.PROFIQO_BACKEND_URL?.trim() || "http://localhost:5164";
}

async function safeReadJson(res: Response): Promise<any> {
  const text = await res.text();
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return { message: text };
  }
}

function pickAccessToken(payload: any): string | null {
  const t1 =
    payload?.tokens?.accessToken ||
    payload?.tokens?.token ||
    payload?.accessToken ||
    payload?.token ||
    payload?.jwt;

  const t2 =
    payload?.Tokens?.AccessToken ||
    payload?.Tokens?.Token ||
    payload?.AccessToken ||
    payload?.Token ||
    payload?.Jwt;

  return (t1 || t2 || null) as string | null;
}

function pickExpiresAt(payload: any): Date | null {
  const raw =
    payload?.tokens?.accessTokenExpiresAtUtc ||
    payload?.accessTokenExpiresAtUtc ||
    payload?.expiresAtUtc ||
    payload?.Tokens?.AccessTokenExpiresAtUtc ||
    payload?.AccessTokenExpiresAtUtc ||
    payload?.ExpiresAtUtc;

  if (!raw) return null;
  const d = new Date(raw);
  return Number.isNaN(d.getTime()) ? null : d;
}

function pickUserInfo(payload: any): {
  tenantId: string | null;
  userId: string | null;
  roles: any;
  displayName: string | null;
  email: string | null;
} {
  const u = payload?.user || payload?.User || null;

  const tenantId =
    u?.tenantId || u?.TenantId || payload?.tenantId || payload?.TenantId || null;

  const userId =
    u?.userId || u?.UserId || payload?.userId || payload?.UserId || null;

  const roles = u?.roles || u?.Roles || payload?.roles || payload?.Roles || null;

  const displayName =
    u?.displayName || u?.DisplayName || payload?.displayName || payload?.DisplayName || null;

  const email =
    u?.email || u?.Email || payload?.email || payload?.Email || null;

  return { tenantId, userId, roles, displayName, email };
}

// Map numeric role codes -> names
function roleNameFromCode(code: string): string | null {
  switch (code) {
    case "1": return "Owner";
    case "2": return "Admin";
    case "3": return "Reporting";
    case "4": return "Integration";
    default: return null;
  }
}

function normalizeRolesToString(roles: any): string | null {
  if (!roles) return null;

  const out: string[] = [];

  if (Array.isArray(roles)) {
    for (const r of roles) {
      const s = String(r).trim();
      if (!s) continue;

      if (["Owner", "Admin", "Reporting", "Integration"].includes(s)) {
        out.push(s);
        continue;
      }

      const mapped = roleNameFromCode(s);
      if (mapped) out.push(mapped);
    }
  } else {
    const s = String(roles).trim();
    if (["Owner", "Admin", "Reporting", "Integration"].includes(s)) out.push(s);
    else {
      const mapped = roleNameFromCode(s);
      if (mapped) out.push(mapped);
    }
  }

  const distinct = Array.from(new Set(out));
  return distinct.length ? distinct.join(",") : null;
}

function tryGetRolesFromJwt(accessToken: string): string | null {
  try {
    const parts = accessToken.split(".");
    if (parts.length < 2) return null;

    const payloadB64 = parts[1]
      .replace(/-/g, "+")
      .replace(/_/g, "/")
      .padEnd(Math.ceil(parts[1].length / 4) * 4, "=");

    const json = Buffer.from(payloadB64, "base64").toString("utf8");
    const payload = JSON.parse(json);

    const candidates =
      payload?.roles ??
      payload?.role ??
      payload?.Roles ??
      payload?.Role ??
      payload?.["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ??
      null;

    return normalizeRolesToString(candidates);
  } catch {
    return null;
  }
}

export async function POST(req: Request) {
  const body = (await req.json().catch(() => null)) as LoginRequest | null;

  const tenantSlug = body?.tenantSlug?.trim() || "";
  const email = body?.email?.trim() || "";
  const password = body?.password || "";
  const remember = Boolean(body?.remember);

  if (!tenantSlug || !email || !password) {
    return NextResponse.json(
      { ok: false, message: "Tenant slug, email ve şifre zorunlu." },
      { status: 400 }
    );
  }

  const upstream = await fetch(`${backendBaseUrl()}/api/auth/login`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    cache: "no-store",
    body: JSON.stringify({ tenantSlug, email, password }),
  }).catch(() => null);

  if (!upstream) {
    return NextResponse.json(
      { ok: false, message: "Backend erişilemiyor (network)." },
      { status: 502 }
    );
  }

  const payload = await safeReadJson(upstream);

  if (!upstream.ok) {
    const msg =
      payload?.message ||
      payload?.title ||
      payload?.detail ||
      "Login başarısız.";

    return NextResponse.json(
      { ok: false, message: msg, errors: payload?.errors ?? payload?.extensions?.errors ?? null },
      { status: upstream.status }
    );
  }

  const accessToken = pickAccessToken(payload);
  if (!accessToken) {
    return NextResponse.json(
      { ok: false, message: "Backend login response içinde access token yok." },
      { status: 500 }
    );
  }

  const expiresAt = pickExpiresAt(payload);
  const { tenantId, userId, roles, displayName, email: userEmail } = pickUserInfo(payload);

  const isProd = process.env.NODE_ENV === "production";
  const res = NextResponse.json({ ok: true, tenantId, userId, roles });

  const longMaxAge = 30 * 24 * 60 * 60;

  // Access token cookie
  if (expiresAt) {
    res.cookies.set(ACCESS_COOKIE, accessToken, {
      httpOnly: true,
      secure: isProd,
      sameSite: "lax",
      path: "/",
      expires: expiresAt,
    });
  } else {
    res.cookies.set(ACCESS_COOKIE, accessToken, {
      httpOnly: true,
      secure: isProd,
      sameSite: "lax",
      path: "/",
      maxAge: 60 * 60,
    });
  }

  // Tenant/User cookies
  if (tenantId) {
    res.cookies.set(TENANT_COOKIE, tenantId, {
      httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: longMaxAge,
    });
  }

  if (userId) {
    res.cookies.set(USER_COOKIE, userId, {
      httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: longMaxAge,
    });
  }

  // User display info cookies
  if (displayName) {
    res.cookies.set(DISPLAY_NAME_COOKIE, displayName, {
      httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: longMaxAge,
    });
  }

  if (userEmail) {
    res.cookies.set(EMAIL_COOKIE, userEmail, {
      httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: longMaxAge,
    });
  }

  // Roles cookie for middleware RBAC
  let rolesStr = normalizeRolesToString(roles);
  if (!rolesStr) {
    rolesStr = tryGetRolesFromJwt(accessToken);
  }

  if (rolesStr) {
    res.cookies.set(ROLES_COOKIE, rolesStr, {
      httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: longMaxAge,
    });
  } else {
    res.cookies.set(ROLES_COOKIE, "", {
      httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: 0,
    });
  }

  // Refresh token handling
  if (!remember) {
    res.cookies.set(REFRESH_COOKIE, "", {
      httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: 0,
    });
  }

  await cookies();

  return res;
}
