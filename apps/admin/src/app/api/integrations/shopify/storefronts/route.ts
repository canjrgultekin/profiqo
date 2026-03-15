// Path: apps/admin/src/app/api/integrations/ikas/storefronts/route.ts
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

export async function GET() {
  const cookieStore = await cookies();
  const token = cookieStore.get(ACCESS_COOKIE)?.value;
  const tenantId = cookieStore.get(TENANT_COOKIE)?.value;

  if (!token) return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  if (!tenantId) return NextResponse.json({ message: "Tenant cookie missing" }, { status: 400 });

  const upstream = await fetch(`${backendBaseUrl()}/api/integrations/ikas/storefronts`, {
    method: "GET",
    headers: { Authorization: `Bearer ${token}`, "X-Tenant-Id": tenantId },
    cache: "no-store",
  });

  const text = await upstream.text();

  if (upstream.status === 401) {
    const res = NextResponse.json({ message: "Unauthorized" }, { status: 401 });
    clearAuthCookies(res);
    return res;
  }

  let payload: any = null;
  try {
    payload = text ? JSON.parse(text) : null;
  } catch {
    payload = { message: "Backend returned non-JSON", raw: text?.slice(0, 2000) || "" };
  }

  return NextResponse.json(payload, { status: upstream.status });
}
