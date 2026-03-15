import { NextResponse } from "next/server";
import { cookies } from "next/headers";

export const dynamic = "force-dynamic";

const ACCESS_COOKIE = "profiqo_access_token";
const TENANT_COOKIE = "profiqo_tenant_id";

function apiBaseUrl(): string {
  return process.env.API_BASE_URL?.trim() || "http://localhost:5164";
}

export async function POST(req: Request) {
  const body = await req.json().catch(() => ({}));

  const c = await cookies();
  const token = c.get(ACCESS_COOKIE)?.value;
  const tenantId = c.get(TENANT_COOKIE)?.value;

  if (!token) return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  if (!tenantId) return NextResponse.json({ message: "Tenant cookie missing" }, { status: 400 });

  const upstream = await fetch(`${apiBaseUrl()}/api/customers/dedupe/analyze`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      Authorization: `Bearer ${token}`,
      "X-Tenant-Id": tenantId,
    },
    cache: "no-store",
    body: JSON.stringify(body),
  });

  const text = await upstream.text();
  return new NextResponse(text, { status: upstream.status, headers: { "content-type": "application/json" } });
}
