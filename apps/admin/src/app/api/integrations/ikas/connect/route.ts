import { NextResponse } from "next/server";
import { cookies } from "next/headers";

const ACCESS_COOKIE = "profiqo_access_token";
const TENANT_COOKIE = "profiqo_tenant_id";

function backendBaseUrl(): string {
  return process.env.PROFIQO_BACKEND_URL?.trim() || "http://localhost:5164";
}

export async function POST(req: Request) {
  const body = await req.json().catch(() => null);

  const cookieStore = await cookies();
  const token = cookieStore.get(ACCESS_COOKIE)?.value;
  const tenantId = cookieStore.get(TENANT_COOKIE)?.value;

  if (!token) return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  if (!tenantId) return NextResponse.json({ message: "Tenant cookie missing" }, { status: 400 });

  const upstream = await fetch(`${backendBaseUrl()}/api/integrations/ikas/connect`, {
    method: "POST",
    headers: {
      "content-type": "application/json",
      Authorization: `Bearer ${token}`,
      "X-Tenant-Id": tenantId,
    },
    body: JSON.stringify(body),
    cache: "no-store",
  });

  const text = await upstream.text();
  const payload = text ? JSON.parse(text) : null;

  return NextResponse.json(payload, { status: upstream.status });
}
