import { proxyJson } from "../_proxy";

export async function GET() {
  return proxyJson({ method: "GET", path: "/api/whatsapp/jobs" });
}

export async function POST(req: Request) {
  const body = await req.json().catch(() => null);
  return proxyJson({ method: "POST", path: "/api/whatsapp/jobs", body });
}
