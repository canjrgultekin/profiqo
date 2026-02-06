import { NextResponse } from "next/server";
import { cookies } from "next/headers";

export async function GET() {
  const c = await cookies();

  const accessToken = c.get("profiqo_access_token")?.value;
  if (!accessToken) {
    return NextResponse.json(
      { ok: false, message: "Not authenticated" },
      { status: 401 }
    );
  }

  const displayName = c.get("profiqo_display_name")?.value || null;
  const email = c.get("profiqo_email")?.value || null;
  const userId = c.get("profiqo_user_id")?.value || null;
  const tenantId = c.get("profiqo_tenant_id")?.value || null;
  const roles = c.get("profiqo_roles")?.value || null;

  return NextResponse.json({
    ok: true,
    user: {
      displayName,
      email,
      userId,
      tenantId,
      roles: roles ? roles.split(",") : [],
    },
  });
}
