import { NextResponse, type NextRequest } from "next/server";

/**
 * Routing convenience only: visitors without a session hint cookie go straight to sign-in instead of loading the
 * app shell first. Authorization always happens in the API.
 */
const PUBLIC = ["/login", "/forgot-password", "/reset-password", "/accept-invite", "/apply", "/design"];

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  if (PUBLIC.some((p) => pathname === p || pathname.startsWith(`${p}/`))) return NextResponse.next();

  const hasSession = request.cookies.has("ss_s") || request.cookies.has("__Secure-ss_s");
  if (!hasSession) {
    const url = request.nextUrl.clone();
    url.pathname = "/login";
    url.search = pathname === "/" ? "" : `?next=${encodeURIComponent(pathname + search)}`;
    return NextResponse.redirect(url);
  }
  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico|.*\\.(?:png|jpg|jpeg|svg|webp|ico|woff2?)$).*)"],
};
