import { NextRequest, NextResponse } from "next/server";

const ACCESS_COOKIE = "profiqo_access_token";
const TENANT_COOKIE = "profiqo_tenant_id";
const ROLES_COOKIE = "profiqo_roles";
const DISPLAY_NAME_COOKIE = "profiqo_display_name";
const EMAIL_COOKIE = "profiqo_email";
const USER_COOKIE = "profiqo_user_id";

function isPublic(pathname: string): boolean {
  if (pathname.startsWith("/auth")) return true;
  if (pathname.startsWith("/api")) return true;
  if (pathname.startsWith("/_next")) return true;
  if (pathname === "/favicon.ico") return true;
  return false;
}

function b64UrlDecode(input: string): string | null {
  try {
    const b64 = input.replace(/-/g, "+").replace(/_/g, "/");
    const padded = b64.padEnd(Math.ceil(b64.length / 4) * 4, "=");
    return atob(padded);
  } catch {
    return null;
  }
}

function isJwtExpired(token: string): boolean {
  const parts = token.split(".");
  if (parts.length < 2) return false;

  const json = b64UrlDecode(parts[1]);
  if (!json) return false;

  try {
    const payload = JSON.parse(json);
    const exp = payload?.exp;
    if (typeof exp === "number") {
      const nowSec = Math.floor(Date.now() / 1000);
      return exp <= nowSec;
    }
    return false;
  } catch {
    return false;
  }
}

function clearAuth(res: NextResponse) {
  const cookiesToClear = [
    ACCESS_COOKIE,
    TENANT_COOKIE,
    ROLES_COOKIE,
    DISPLAY_NAME_COOKIE,
    EMAIL_COOKIE,
    USER_COOKIE,
  ];
  for (const name of cookiesToClear) {
    res.cookies.set(name, "", { path: "/", maxAge: 0 });
  }
}

function redirectToSignIn(req: NextRequest) {
  const url = req.nextUrl.clone();
  url.pathname = "/auth/sign-in";
  const res = NextResponse.redirect(url);
  clearAuth(res);
  return res;
}

export function middleware(req: NextRequest) {
  const { pathname } = req.nextUrl;

  const token = req.cookies.get(ACCESS_COOKIE)?.value;
  const tokenExpired = token ? isJwtExpired(token) : false;

  // signed-in user should not see auth pages
  if (pathname.startsWith("/auth")) {
    if (token && !tokenExpired) {
      const url = req.nextUrl.clone();
      url.pathname = "/";
      return NextResponse.redirect(url);
    }
    return NextResponse.next();
  }

  if (isPublic(pathname)) return NextResponse.next();

  if (!token) return redirectToSignIn(req);
  if (tokenExpired) return redirectToSignIn(req);

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
