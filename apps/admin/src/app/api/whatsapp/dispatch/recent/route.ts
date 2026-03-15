import { proxyJson } from "../../_proxy";

export async function GET(req: Request) {
  const url = new URL(req.url);
  const take = url.searchParams.get("take") || "100";
  return proxyJson({ method: "GET", path: `/api/whatsapp/dispatch/recent`, query: { take } });
}
