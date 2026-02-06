// Path: apps/admin/src/app/api/tenant/users/[userId]/disable/route.ts
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

async function resolveParams(ctx: any): Promise<{ userId: string }> {
  if (ctx?.params && typeof ctx.params?.then === "function") return await ctx.params;
  if (ctx && typeof ctx?.then === "function") {
    const awaited = await ctx;
    if (awaited?.params && typeof awaited.params?.then === "function") return await awaited.params;
    return awaited?.params;
  }
  return ctx?.params;
}

export async function POST(_req: Request, ctx: any) {
  const params = await resolveParams(ctx);
  const userId = params?.userId;

  if (!userId) return NextResponse.json({ message: "userId missing" }, { status: 400 });

  const cs = await cookies();
  const token = cs.get(ACCESS_COOKIE)?.value;
  const tenantId = cs.get(TENANT_COOKIE)?.value;

  if (!token) return NextResponse.json({ message: "Unauthorized" }, { status: 401 });
  if (!tenantId) return NextResponse.json({ message: "Tenant cookie missing" }, { status: 400 });

  const upstream = await fetch(`${backendBaseUrl()}/api/tenant/users/${userId}/disable`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}`, "X-Tenant-Id": tenantId },
    cache: "no-store",
  });

  const text = await upstream.text();

  if (upstream.status === 401) {
    const res = NextResponse.json({ message: "Unauthorized" }, { status: 401 });
    clearAuthCookies(res);
    return res;
  }

  let payload: any;
  try {
    payload = text ? JSON.parse(text) : null;
  } catch {
    payload = { message: "Backend returned non-JSON", raw: text?.slice(0, 2000) || "" };
  }

  return NextResponse.json(payload, { status: upstream.status });
}
