import { proxyJson } from "../../_proxy";

export async function GET(
  _req: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const { id } = await ctx.params;
  return proxyJson({ method: "GET", path: `/api/whatsapp/rules/${id}` });
}

export async function DELETE(
  _req: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const { id } = await ctx.params;
  return proxyJson({ method: "DELETE", path: `/api/whatsapp/rules/${id}` });
}
