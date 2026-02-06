import { proxyJson } from "../../../whatsapp/_proxy";

export async function POST(req: Request) {
  const body = await req.json().catch(() => null);
  return proxyJson({ method: "POST", path: "/api/integrations/whatsapp/test", body });
}
