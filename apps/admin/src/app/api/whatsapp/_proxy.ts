import { NextResponse } from "next/server";
import { cookies } from "next/headers";

const ACCESS_COOKIE = "profiqo_access_token";
const TENANT_COOKIE = "profiqo_tenant_id";
const ROLES_COOKIE = "profiqo_roles";

function backendBaseUrl(): string {
  return (process.env.PROFIQO_BACKEND_URL || "http://localhost:5164").trim();
}

function clearAuthCookies(res: NextResponse) {
  res.cookies.set(ACCESS_COOKIE, "", { path: "/", maxAge: 0 });
  res.cookies.set(TENANT_COOKIE, "", { path: "/", maxAge: 0 });
  res.cookies.set(ROLES_COOKIE, "", { path: "/", maxAge: 0 });
}

export async function proxyJson(opts: {
  method: "GET" | "POST" | "DELETE";
  path: string;
  body?: any;
  query?: Record<string, string | number | boolean | null | undefined>;
}) {
  const cookieStore = await cookies();
  const token = cookieStore.get(ACCESS_COOKIE)?.value;
  const tenantId = cookieStore.get(TENANT_COOKIE)?.value;

  if (!token) return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  if (!tenantId) return NextResponse.json({ message: "Tenant cookie missing" }, { status: 400 });

  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(opts.query || {})) {
    if (v === null || v === undefined) continue;
    qs.set(k, String(v));
  }

  const url = `${backendBaseUrl()}${opts.path}${qs.toString() ? `?${qs.toString()}` : ""}`;

  const upstream = await fetch(url, {
    method: opts.method,
    headers: {
      Authorization: `Bearer ${token}`,
      "X-Tenant-Id": tenantId,
      ...(opts.method === "POST" ? { "content-type": "application/json" } : {}),
    },
    body: opts.method === "POST" ? JSON.stringify(opts.body ?? {}) : undefined,
    cache: "no-store",
  });

  const text = await upstream.text();

  if (upstream.status === 401) {
    const res = NextResponse.json({ message: "Unauthorized" }, { status: 401 });
    clearAuthCookies(res);
    return res;
  }

  // Backend bazen boş body dönebilir. HTML dönerse JSON parse etmeyelim.
  const ct = upstream.headers.get("content-type") || "";
  if (!ct.includes("application/json")) {
    return NextResponse.json(
      { message: `Upstream returned non-JSON (HTTP ${upstream.status})`, raw: text?.slice(0, 2000) || "" },
      { status: upstream.status }
    );
  }

  let payload: any = null;
  try {
    payload = text ? JSON.parse(text) : null;
  } catch {
    payload = { message: `Invalid JSON from upstream (HTTP ${upstream.status})`, raw: text?.slice(0, 2000) || "" };
  }

  return NextResponse.json(payload, { status: upstream.status });
}
