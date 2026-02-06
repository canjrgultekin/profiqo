import { proxyJson } from "../_proxy";

export async function GET(req: Request) {
  const url = new URL(req.url);
  const q = url.searchParams.get("q") || "";
  const page = url.searchParams.get("page") || "1";
  const pageSize = url.searchParams.get("pageSize") || "20";

  return proxyJson({
    method: "GET",
    path: "/api/whatsapp/targets",
    query: { q, page, pageSize },
  });
}
