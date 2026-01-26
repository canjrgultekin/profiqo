import { NextRequest, NextResponse } from "next/server";

const ACCESS_COOKIE = "profiqo_access_token";

function isPublicPath(pathname: string): boolean {
  if (pathname.startsWith("/auth")) return true;     // auth pages always public
  if (pathname.startsWith("/api")) return true;      // next api routes
  if (pathname.startsWith("/_next")) return true;
  if (pathname === "/favicon.ico") return true;
  if (pathname.startsWith("/images")) return true;
  if (pathname.startsWith("/fonts")) return true;
  return false;
}

export function middleware(req: NextRequest) {
  const { pathname } = req.nextUrl;

  if (isPublicPath(pathname)) {
    return NextResponse.next();
  }

  const token = req.cookies.get(ACCESS_COOKIE)?.value;

  if (!token) {
    const url = req.nextUrl.clone();
    url.pathname = "/auth/sign-in";
    return NextResponse.redirect(url);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};
