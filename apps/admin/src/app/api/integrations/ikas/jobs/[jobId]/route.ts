import { NextResponse } from "next/server";
import { cookies } from "next/headers";

const ACCESS_COOKIE = "profiqo_access_token";
const TENANT_COOKIE = "profiqo_tenant_id";

function backendBaseUrl(): string {
  return process.env.PROFIQO_BACKEND_URL?.trim() || "http://localhost:5164";
}

async function resolveParams(ctx: any): Promise<{ jobId: string }> {
  if (ctx?.params && typeof ctx.params?.then === "function") {
    return await ctx.params;
  }
  if (ctx && typeof ctx?.then === "function") {
    const awaited = await ctx;
    if (awaited?.params && typeof awaited.params?.then === "function") {
      return await awaited.params;
    }
    return awaited?.params;
  }
  return ctx?.params;
}

export async function GET(_req: Request, ctx: any) {
  const params = await resolveParams(ctx);
  const jobId = params?.jobId;

  if (!jobId) {
    return NextResponse.json({ message: "jobId missing in route params" }, { status: 400 });
  }

  const cookieStore = await cookies();
  const token = cookieStore.get(ACCESS_COOKIE)?.value;
  const tenantId = cookieStore.get(TENANT_COOKIE)?.value;

  if (!token) return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  if (!tenantId) return NextResponse.json({ message: "Tenant cookie missing" }, { status: 400 });

  const upstream = await fetch(`${backendBaseUrl()}/api/integrations/ikas/jobs/${jobId}`, {
    method: "GET",
    headers: {
      Authorization: `Bearer ${token}`,
      "X-Tenant-Id": tenantId,
    },
    cache: "no-store",
  });

  const text = await upstream.text();
  let payload: any = null;
  try {
    payload = text ? JSON.parse(text) : null;
  } catch {
    payload = { message: "Backend returned non-JSON", raw: text?.slice(0, 2000) || "" };
  }

  return NextResponse.json(payload, { status: upstream.status });
}
