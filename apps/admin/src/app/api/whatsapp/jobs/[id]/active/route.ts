import { proxyJson } from "../../../_proxy";

export async function POST(
  req: Request,
  ctx: { params: Promise<{ id: string }> }
) {
  const { id } = await ctx.params;
  const body = await req.json().catch(() => null);

  return proxyJson({
    method: "POST",
    path: `/api/whatsapp/jobs/${id}/active`,
    body,
  });
}
