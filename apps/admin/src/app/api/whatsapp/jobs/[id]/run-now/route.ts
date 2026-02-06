import { proxyJson } from "../../../_proxy";

export async function POST(
  _req: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const { id } = await ctx.params;

  return proxyJson({
    method: "POST",
    path: `/api/whatsapp/jobs/${id}/run-now`,
    body: {},
  });
}
