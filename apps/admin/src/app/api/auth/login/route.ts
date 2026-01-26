import { NextResponse } from "next/server";

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
  // camelCase variants
  const t1 =
    payload?.tokens?.accessToken ||
    payload?.tokens?.token ||
    payload?.accessToken ||
    payload?.token ||
    payload?.jwt;

  // PascalCase variants (because ASP.NET output is PascalCase in your API)
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

function pickUserInfo(payload: any): { tenantId: string | null; userId: string | null; roles: any } {
  const u = payload?.user || payload?.User || null;

  const tenantId =
    u?.tenantId ||
    u?.TenantId ||
    payload?.tenantId ||
    payload?.TenantId ||
    null;

  const userId =
    u?.userId ||
    u?.UserId ||
    payload?.userId ||
    payload?.UserId ||
    null;

  const roles = u?.roles || u?.Roles || payload?.roles || payload?.Roles || null;

  return { tenantId, userId, roles };
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
      {
        ok: false,
        message: "Backend login response içinde access token yok.",
        debug: payload, // local debug, istersen kaldırırız
      },
      { status: 500 }
    );
  }

  const expiresAt = pickExpiresAt(payload);
  const { tenantId, userId, roles } = pickUserInfo(payload);

  const isProd = process.env.NODE_ENV === "production";
  const res = NextResponse.json({ ok: true, tenantId, userId, roles });

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

  // Refresh token şimdilik backend dönmüyorsa temizle
  if (!remember) {
    res.cookies.set(REFRESH_COOKIE, "", {
      httpOnly: true,
      secure: isProd,
      sameSite: "lax",
      path: "/",
      maxAge: 0,
    });
  }

  if (tenantId) {
    res.cookies.set(TENANT_COOKIE, tenantId, {
      httpOnly: true,
      secure: isProd,
      sameSite: "lax",
      path: "/",
      maxAge: 30 * 24 * 60 * 60,
    });
  }

  if (userId) {
    res.cookies.set(USER_COOKIE, userId, {
      httpOnly: true,
      secure: isProd,
      sameSite: "lax",
      path: "/",
      maxAge: 30 * 24 * 60 * 60,
    });
  }

  return res;
}
