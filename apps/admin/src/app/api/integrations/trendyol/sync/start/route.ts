// Path: apps/admin/src/app/api/integrations/trendyol/sync/start/route.ts
import { NextResponse } from "next/server";
import { cookies } from "next/headers";

const ACCESS_COOKIE = "profiqo_access_token";
const TENANT_COOKIE = "profiqo_tenant_id";
const ROLES_COOKIE = "profiqo_roles";

function backendBaseUrl(): string {
  return process.env.PROFIQO_BACKEND_URL?.trim() || "http://localhost:5164";
}

function clearAuthCookies(res: NextResponse) {
  res.cookies.set(ACCESS_COOKIE, "", { path: "/", maxAge: 0 });
  res.cookies.set(TENANT_COOKIE, "", { path: "/", maxAge: 0 });
  res.cookies.set(ROLES_COOKIE, "", { path: "/", maxAge: 0 });
}

async function safeReadJson(res: Response): Promise<any> {
  const text = await res.text();
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return { message: "Backend returned non-JSON", raw: text.slice(0, 2000) };
  }
}

export async function POST(req: Request) {
  const body = await req.json().catch(() => null);

  const cs = await cookies();
  const token = cs.get(ACCESS_COOKIE)?.value;
  const tenantId = cs.get(TENANT_COOKIE)?.value;

  if (!token) return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  if (!tenantId) return NextResponse.json({ message: "Tenant cookie missing" }, { status: 400 });

  const upstream = await fetch(`${backendBaseUrl()}/api/integrations/trendyol/sync/start`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      Authorization: `Bearer ${token}`,
      "X-Tenant-Id": tenantId,
    },
    cache: "no-store",
    body: JSON.stringify(body),
  });

  if (upstream.status === 401) {
    const res = NextResponse.json({ message: "Unauthorized" }, { status: 401 });
    clearAuthCookies(res);
    return res;
  }

  const payload = await safeReadJson(upstream);
  return NextResponse.json(payload, { status: upstream.status });
}
