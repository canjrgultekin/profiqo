import { NextResponse } from "next/server";
import { cookies } from "next/headers";

const ACCESS_COOKIE = "profiqo_access_token";
const TENANT_COOKIE = "profiqo_tenant_id";

function backendBaseUrl(): string {
  return process.env.PROFIQO_BACKEND_URL?.trim() || "http://localhost:5164";
}

export async function GET(req: Request) {
  const url = new URL(req.url);

  const page = url.searchParams.get("page") || "1";
  const pageSize = url.searchParams.get("pageSize") || "25";
  const q = url.searchParams.get("q");
  const customerId = url.searchParams.get("customerId");

  const cookieStore = await cookies();
  const token = cookieStore.get(ACCESS_COOKIE)?.value;
  const tenantId = cookieStore.get(TENANT_COOKIE)?.value;

  if (!token) return NextResponse.json({ ok: false, message: "Unauthorized" }, { status: 401 });
  if (!tenantId) return NextResponse.json({ ok: false, message: "Tenant cookie missing" }, { status: 400 });

  const qs = new URLSearchParams({ page, pageSize });
  if (q) qs.set("q", q);
  if (customerId) qs.set("customerId", customerId);

  const upstream = await fetch(`${backendBaseUrl()}/api/orders?${qs.toString()}`, {
    method: "GET",
    headers: {
      Authorization: `Bearer ${token}`,
      "X-Tenant-Id": tenantId,
    },
    cache: "no-store",
  });

  const text = await upstream.text();
  const payload = text ? JSON.parse(text) : null;

  return NextResponse.json(payload, { status: upstream.status });
}
