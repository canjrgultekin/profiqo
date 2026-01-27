import { NextRequest, NextResponse } from "next/server";

const ACCESS_COOKIE = "profiqo_access_token";
const ROLES_COOKIE = "profiqo_roles";
const TENANT_COOKIE = "profiqo_tenant_id";

function isPublic(pathname: string): boolean {
  if (pathname.startsWith("/auth")) return true;
  if (pathname.startsWith("/api")) return true;
  if (pathname.startsWith("/_next")) return true;
  if (pathname === "/favicon.ico") return true;
  return false;
}

function mapRoleToken(s: string): string | null {
  const t = s.trim();
  if (!t) return null;
  if (["Owner", "Admin", "Integration", "Reporting"].includes(t)) return t;
  if (t === "1") return "Owner";
  if (t === "2") return "Admin";
  if (t === "3") return "Reporting";
  if (t === "4") return "Integration";
  return null;
}

function parseRoles(raw: string | undefined): Set<string> {
  const set = new Set<string>();
  if (!raw) return set;

  raw
    .split(",")
    .map((x) => mapRoleToken(x))
    .filter((x): x is string => Boolean(x))
    .forEach((x) => set.add(x));

  return set;
}

function hasAnyRole(roles: Set<string>, allowed: string[]): boolean {
  return allowed.some((r) => roles.has(r));
}

// Edge-safe base64url decode (no Buffer)
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
  // token: header.payload.signature
  const parts = token.split(".");
  if (parts.length < 2) return false;

  const json = b64UrlDecode(parts[1]);
  if (!json) return false;

  try {
    const payload = JSON.parse(json);
    const exp = payload?.exp;

    // exp typically seconds
    if (typeof exp === "number") {
      const nowSec = Math.floor(Date.now() / 1000);
      return exp <= nowSec;
    }

    return false;
  } catch {
    return false;
  }
}

function redirectToSignIn(req: NextRequest) {
  const url = req.nextUrl.clone();
  url.pathname = "/auth/sign-in";

  const res = NextResponse.redirect(url);

  // clear auth cookies immediately to prevent flash / loops
  res.cookies.set(ACCESS_COOKIE, "", { path: "/", maxAge: 0 });
  res.cookies.set(TENANT_COOKIE, "", { path: "/", maxAge: 0 });
  res.cookies.set(ROLES_COOKIE, "", { path: "/", maxAge: 0 });

  return res;
}

export function middleware(req: NextRequest) {
  const { pathname } = req.nextUrl;

  if (isPublic(pathname)) {
    return NextResponse.next();
  }

  const token = req.cookies.get(ACCESS_COOKIE)?.value;

  // No token => no render, immediate redirect
  if (!token) {
    return redirectToSignIn(req);
  }

  // Token exists but expired => clear + redirect BEFORE any page render
  if (isJwtExpired(token)) {
    return redirectToSignIn(req);
  }

  const roles = parseRoles(req.cookies.get(ROLES_COOKIE)?.value);

  // Owner-only
  if (pathname.startsWith("/settings/users")) {
    if (!hasAnyRole(roles, ["Owner"])) {
      const url = req.nextUrl.clone();
      url.pathname = "/403";
      return NextResponse.redirect(url);
    }
  }

  // Integrations
  if (pathname.startsWith("/integrations")) {
    if (!hasAnyRole(roles, ["Owner", "Admin", "Integration"])) {
      const url = req.nextUrl.clone();
      url.pathname = "/403";
      return NextResponse.redirect(url);
    }
  }

  // Reporting
  if (pathname.startsWith("/customers") || pathname.startsWith("/orders") || pathname.startsWith("/reports")) {
    if (!hasAnyRole(roles, ["Owner", "Admin", "Integration", "Reporting"])) {
      const url = req.nextUrl.clone();
      url.pathname = "/403";
      return NextResponse.redirect(url);
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
