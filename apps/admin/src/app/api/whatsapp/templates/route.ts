import { proxyJson } from "../_proxy";

export async function GET() {
  return proxyJson({ method: "GET", path: "/api/whatsapp/templates" });
}
