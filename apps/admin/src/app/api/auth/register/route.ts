import { NextResponse } from "next/server";

type RegisterRequest = {
  tenantName?: string;
  tenantSlug?: string;
  displayName?: string;
  email?: string;
  password?: string;
  remember?: boolean;
};

type BackendRegisterResponse = {
  accessToken?: string;
  token?: string;
  jwt?: string;

  refreshToken?: string;

  expiresAtUtc?: string;
  accessTokenExpiresAtUtc?: string;

  tenantId?: string;
  userId?: string;
  roles?: string[] | number[];
};

const ACCESS_COOKIE = "profiqo_access_token";
const REFRESH_COOKIE = "profiqo_refresh_token";
const TENANT_COOKIE = "profiqo_tenant_id";
const USER_COOKIE = "profiqo_user_id";

function backendBaseUrl(): string {
  return process.env.PROFIQO_BACKEND_URL?.trim() || "http://localhost:5164";
}

function parseExpiryDate(payload: BackendRegisterResponse): Date | null {
  const raw = payload.expiresAtUtc || payload.accessTokenExpiresAtUtc;
  if (!raw) return null;
  const d = new Date(raw);
  if (Number.isNaN(d.getTime())) return null;
  return d;
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

  // Backend’in beklediği net payload
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

  const payload = (await safeReadJson(upstream)) as BackendRegisterResponse | any;

  if (!upstream.ok) {
    // Backend validation detail’i varsa UI’da göster
    const msg =
      payload?.message ||
      payload?.title ||
      payload?.detail ||
      "Register başarısız.";
    return NextResponse.json(
      { ok: false, message: msg, errors: payload?.errors ?? payload?.extensions?.errors ?? null },
      { status: upstream.status }
    );
  }

  const accessToken = payload?.accessToken || payload?.token || payload?.jwt;
  const refreshToken = payload?.refreshToken;

  const isProd = process.env.NODE_ENV === "production";
  const res = NextResponse.json({
    ok: true,
    tenantId: payload?.tenantId ?? null,
    userId: payload?.userId ?? null,
    roles: payload?.roles ?? null,
    hasTokens: Boolean(accessToken),
  });

  if (accessToken) {
    const expiresAt = parseExpiryDate(payload);

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

    if (remember && refreshToken) {
      res.cookies.set(REFRESH_COOKIE, refreshToken, {
        httpOnly: true,
        secure: isProd,
        sameSite: "lax",
        path: "/",
        maxAge: 30 * 24 * 60 * 60,
      });
    } else {
      res.cookies.set(REFRESH_COOKIE, "", {
        httpOnly: true,
        secure: isProd,
        sameSite: "lax",
        path: "/",
        maxAge: 0,
      });
    }

    if (payload?.tenantId) {
      res.cookies.set(TENANT_COOKIE, payload.tenantId, {
        httpOnly: true,
        secure: isProd,
        sameSite: "lax",
        path: "/",
        maxAge: 30 * 24 * 60 * 60,
      });
    }

    if (payload?.userId) {
      res.cookies.set(USER_COOKIE, payload.userId, {
        httpOnly: true,
        secure: isProd,
        sameSite: "lax",
        path: "/",
        maxAge: 30 * 24 * 60 * 60,
      });
    }
  }

  return res;
}
