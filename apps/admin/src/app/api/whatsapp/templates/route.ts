import { proxyJson } from "../_proxy";

export async function GET() {
  return proxyJson({ method: "GET", path: "/api/whatsapp/templates" });
}

export async function POST(req: Request) {
  const body = await req.json();
  return proxyJson({
    method: "POST",
    path: "/api/whatsapp/templates",
    body,
  });
}