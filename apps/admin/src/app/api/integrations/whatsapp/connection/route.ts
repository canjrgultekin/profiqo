import { proxyJson } from "../../../whatsapp/_proxy";

export async function GET() {
  return proxyJson({ method: "GET", path: "/api/integrations/whatsapp/connection" });
}
