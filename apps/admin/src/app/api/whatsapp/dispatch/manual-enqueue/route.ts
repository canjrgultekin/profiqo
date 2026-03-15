import { proxyJson } from "../../_proxy";

export async function POST(req: Request) {
  const body = await req.json().catch(() => null);
  return proxyJson({ method: "POST", path: `/api/whatsapp/dispatch/manual-enqueue`, body });
}
