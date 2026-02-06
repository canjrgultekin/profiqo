import { NextResponse } from "next/server";

const COOKIES_TO_CLEAR = [
  "profiqo_access_token",
  "profiqo_refresh_token",
  "profiqo_tenant_id",
  "profiqo_user_id",
  "profiqo_roles",
  "profiqo_display_name",
  "profiqo_email",
];

export async function POST() {
  const res = NextResponse.json({ ok: true });

  for (const name of COOKIES_TO_CLEAR) {
    res.cookies.set(name, "", { path: "/", maxAge: 0 });
  }

  return res;
}
