import { NextResponse } from "next/server";

type RegisterRequest = {
  tenantName?: string;
  tenantSlug?: string;
  displayName?: string;
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
  return (
    payload?.tokens?.accessToken ||
    payload?.tokens?.token ||
    payload?.accessToken ||
    payload?.token ||
    payload?.jwt ||
    payload?.Tokens?.AccessToken ||
    payload?.Tokens?.Token ||
    payload?.AccessToken ||
    payload?.Token ||
    payload?.Jwt ||
    null
  );
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

  return {
    tenantId: u?.tenantId || u?.TenantId || payload?.tenantId || payload?.TenantId || null,
    userId: u?.userId || u?.UserId || payload?.userId || payload?.UserId || null,
    roles: u?.roles || u?.Roles || payload?.roles || payload?.Roles || null,
    displayName: u?.displayName || u?.DisplayName || payload?.displayName || payload?.DisplayName || null,
    email: u?.email || u?.Email || payload?.email || payload?.Email || null,
  };
}

function normalizeRolesToString(roles: any): string | null {
  if (!roles) return null;
  const roleMap: Record<string, string> = { "1": "Owner", "2": "Admin", "3": "Reporting", "4": "Integration" };
  const names = ["Owner", "Admin", "Reporting", "Integration"];
  const out: string[] = [];

  const arr = Array.isArray(roles) ? roles : [roles];
  for (const r of arr) {
    const s = String(r).trim();
    if (names.includes(s)) out.push(s);
    else if (roleMap[s]) out.push(roleMap[s]);
  }

  return [...new Set(out)].join(",") || null;
}

export async function POST(req: Request) {
  const body = (await req.json().catch(() => null)) as RegisterRequest | null;

  const tenantName = body?.tenantName?.trim() || "";
  const tenantSlug = body?.tenantSlug?.trim() || "";
  const displayName = body?.displayName?.trim() || "";
  const email = body?.email?.trim() || "";
  const password = body?.password || "";
  const remember = Boolean(body?.remember);

  if (!tenantName || !tenantSlug || !email || !password) {
    return NextResponse.json(
      { ok: false, message: "Tenant adı, tenant slug, email ve şifre zorunlu." },
      { status: 400 }
    );
  }

  const upstreamBody = {
    tenantName,
    tenantSlug,
    ownerEmail: email,
    ownerPassword: password,
    ownerDisplayName: displayName || email.split("@")[0],
  };

  const upstream = await fetch(`${backendBaseUrl()}/api/auth/register`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    cache: "no-store",
    body: JSON.stringify(upstreamBody),
  }).catch(() => null);

  if (!upstream) {
    return NextResponse.json(
      { ok: false, message: "Backend erişilemiyor (network)." },
      { status: 502 }
    );
  }

  const payload = await safeReadJson(upstream);

  if (!upstream.ok) {
    const msg = payload?.message || payload?.title || payload?.detail || "Register başarısız.";
    return NextResponse.json(
      { ok: false, message: msg, errors: payload?.errors ?? payload?.extensions?.errors ?? null },
      { status: upstream.status }
    );
  }

  const accessToken = pickAccessToken(payload);
  const expiresAt = pickExpiresAt(payload);
  const { tenantId, userId, roles, displayName: respDisplayName, email: respEmail } = pickUserInfo(payload);

  const isProd = process.env.NODE_ENV === "production";
  const longMaxAge = 30 * 24 * 60 * 60;

  const res = NextResponse.json({
    ok: true,
    tenantId,
    userId,
    roles,
    hasTokens: Boolean(accessToken),
  });

  if (accessToken) {
    if (expiresAt) {
      res.cookies.set(ACCESS_COOKIE, accessToken, {
        httpOnly: true, secure: isProd, sameSite: "lax", path: "/", expires: expiresAt,
      });
    } else {
      res.cookies.set(ACCESS_COOKIE, accessToken, {
        httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: 60 * 60,
      });
    }

    if (!remember) {
      res.cookies.set(REFRESH_COOKIE, "", {
        httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: 0,
      });
    }

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
    const nameToStore = respDisplayName || displayName || email.split("@")[0];
    if (nameToStore) {
      res.cookies.set(DISPLAY_NAME_COOKIE, nameToStore, {
        httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: longMaxAge,
      });
    }

    const emailToStore = respEmail || email;
    if (emailToStore) {
      res.cookies.set(EMAIL_COOKIE, emailToStore, {
        httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: longMaxAge,
      });
    }

    const rolesStr = normalizeRolesToString(roles);
    if (rolesStr) {
      res.cookies.set(ROLES_COOKIE, rolesStr, {
        httpOnly: true, secure: isProd, sameSite: "lax", path: "/", maxAge: longMaxAge,
      });
    }
  }

  return res;
}
