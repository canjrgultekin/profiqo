import { NextResponse } from "next/server";
import { cookies } from "next/headers";

export const dynamic = "force-dynamic";

const ACCESS_COOKIE = "profiqo_access_token";
const TENANT_COOKIE = "profiqo_tenant_id";

function apiBaseUrl(): string {
  return process.env.API_BASE_URL?.trim() || "http://localhost:5164";
}

export async function GET(_req: Request, { params }: { params: Promise<{ batchId: string }> }) {
  const { batchId } = await params;

  const c = await cookies();
  const token = c.get(ACCESS_COOKIE)?.value;
  const tenantId = c.get(TENANT_COOKIE)?.value;
  if (!token || !tenantId) return NextResponse.json({ message: "Unauthorized" }, { status: 401 });

  const upstream = await fetch(`${apiBaseUrl()}/api/automation/sync/runs/${batchId}`, {
    headers: { Authorization: `Bearer ${token}`, "X-Tenant-Id": tenantId },
    cache: "no-store",
  });

  const text = await upstream.text();
  return new NextResponse(text, { status: upstream.status, headers: { "content-type": "application/json" } });
}
