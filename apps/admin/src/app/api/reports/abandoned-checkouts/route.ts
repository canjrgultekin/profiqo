import { NextResponse } from "next/server";
import { cookies } from "next/headers";

const ACCESS_COOKIE = "profiqo_access_token";
const TENANT_COOKIE = "profiqo_tenant_id";
const ROLES_COOKIE = "profiqo_roles";

function backendBaseUrl(): string {
  return process.env.PROFIQO_BACKEND_URL?.trim() || "http://localhost:5164";
}

function clear(res: NextResponse) {
  res.cookies.set(ACCESS_COOKIE, "", { path: "/", maxAge: 0 });
  res.cookies.set(TENANT_COOKIE, "", { path: "/", maxAge: 0 });
  res.cookies.set(ROLES_COOKIE, "", { path: "/", maxAge: 0 });
}

export async function GET(req: Request) {
  const url = new URL(req.url);
  const page = url.searchParams.get("page") || "1";
  const pageSize = url.searchParams.get("pageSize") || "25";
  const q = url.searchParams.get("q");

  const cs = await cookies();
  const token = cs.get(ACCESS_COOKIE)?.value;
  const tenantId = cs.get(TENANT_COOKIE)?.value;

  if (!token) return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  if (!tenantId) return NextResponse.json({ message: "Tenant cookie missing" }, { status: 400 });

  const qs = new URLSearchParams({ page, pageSize });
  if (q) qs.set("q", q);

  const upstream = await fetch(`${backendBaseUrl()}/api/reports/abandoned-checkouts?${qs.toString()}`, {
    method: "GET",
    headers: { Authorization: `Bearer ${token}`, "X-Tenant-Id": tenantId },
    cache: "no-store",
  });

  const text = await upstream.text();
  if (upstream.status === 401) {
    const res = NextResponse.json({ message: "Unauthorized" }, { status: 401 });
    clear(res);
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
