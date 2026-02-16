// Path: apps/admin/src/app/(dashboard)/products/page.tsx
"use client";

import React, { useEffect, useState, useCallback } from "react";

type ProductItem = {
  productId: string;
  providerProductId: string;
  name: string;
  description: string | null;
  brandId: string | null;
  brandName: string | null;
  categories: { id: string; name: string }[] | null;
  totalStock: number;
  variantCount: number;
  providerCreatedAtMs: number;
  providerUpdatedAtMs: number;
};

type ProductDetail = {
  productId: string;
  providerProductId: string;
  name: string;
  description: string | null;
  brandName: string | null;
  categories: { id: string; name: string }[] | null;
  totalStock: number;
  variants: {
    variantId: string;
    providerVariantId: string;
    sku: string | null;
    hsCode: string | null;
    barcodeList: string[] | null;
    sellIfOutOfStock: boolean | null;
    prices: { buyPrice: number; sellPrice: number; discountPrice: number | null; currency: string | null; currencyCode: string | null }[] | null;
    stocks: { stockCount: number; id: string }[] | null;
  }[];
};

function fmtMs(ms: number): string {
  if (!ms || ms <= 0) return "-";
  return new Date(ms).toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

function fmtPrice(n: number | null | undefined): string {
  if (n === null || n === undefined) return "-";
  return n.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

const cardCls = "rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card";
const inputCls = "w-full rounded-lg border border-stroke bg-transparent px-3.5 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white";

export default function ProductsPage() {
  const [items, setItems] = useState<ProductItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);

  // Detail panel
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<ProductDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const loadProducts = useCallback(async () => {
    setLoading(true);
    setErr(null);
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (search.trim()) params.set("q", search.trim());

    const res = await fetch(`/api/products?${params}`, { cache: "no-store" });
    const payload = await res.json().catch(() => null);

    if (!res.ok) {
      setErr(payload?.message || "Ürünler yüklenemedi.");
      setItems([]);
      setTotal(0);
      setLoading(false);
      return;
    }

    setItems(payload?.items || []);
    setTotal(payload?.total || 0);
    setLoading(false);
  }, [page, pageSize, search]);

  useEffect(() => { loadProducts(); }, [loadProducts]);

  const loadDetail = async (productId: string) => {
    setSelectedId(productId);
    setDetailLoading(true);
    setDetail(null);

    const res = await fetch(`/api/products/${productId}`, { cache: "no-store" });
    const payload = await res.json().catch(() => null);

    if (res.ok) setDetail(payload);
    setDetailLoading(false);
  };

  const totalPages = Math.ceil(total / pageSize);

  return (
    <div className="space-y-6 p-4 sm:p-6">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-dark dark:text-white">Ürünler</h1>
          <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
            ikas mağazanızdan senkronize edilen ürünleriniz. Toplam: {total.toLocaleString("tr-TR")}
          </p>
        </div>
        <div className="w-full max-w-xs">
          <input
            className={inputCls}
            placeholder="Ürün veya marka ara..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          />
        </div>
      </div>

      {err && (
        <div className="rounded-xl border border-red-200 bg-red-50 px-5 py-4 text-sm text-red-700 dark:border-red-900/40 dark:bg-red-900/20 dark:text-red-400">{err}</div>
      )}

      {/* Product Table */}
      <div className={cardCls}>
        <div className="overflow-x-auto">
          <table className="w-full table-auto">
            <thead>
              <tr className="border-b border-stroke bg-gray-50 dark:border-dark-3 dark:bg-dark-2">
                <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Ürün</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Marka</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Kategori</th>
                <th className="px-4 py-3 text-center text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Stok</th>
                <th className="px-4 py-3 text-center text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Varyant</th>
                <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Güncelleme</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan={6} className="px-4 py-8 text-center text-sm text-body-color dark:text-dark-6">Yükleniyor...</td></tr>
              ) : items.length === 0 ? (
                <tr><td colSpan={6} className="px-4 py-8 text-center text-sm text-body-color dark:text-dark-6">
                  {search ? "Arama sonucu bulunamadı." : "Henüz ürün senkronize edilmemiş. Entegrasyonlar sayfasından sync başlatın."}
                </td></tr>
              ) : items.map((p) => (
                <tr
                  key={p.productId}
                  className={`cursor-pointer border-b border-stroke/50 transition-colors hover:bg-gray-50 dark:border-dark-3/50 dark:hover:bg-dark-2/50 ${selectedId === p.productId ? "bg-primary/5 dark:bg-primary/10" : ""}`}
                  onClick={() => loadDetail(p.productId)}
                >
                  <td className="px-4 py-3">
                    <div className="text-sm font-medium text-dark dark:text-white">{p.name}</div>
                    {p.description && <div className="mt-0.5 max-w-xs truncate text-xs text-body-color dark:text-dark-6">{p.description}</div>}
                  </td>
                  <td className="px-4 py-3 text-sm text-dark-5 dark:text-dark-6">{p.brandName || "-"}</td>
                  <td className="px-4 py-3">
                    {p.categories && p.categories.length > 0 ? (
                      <div className="flex flex-wrap gap-1">
                        {p.categories.map((c: any, i: number) => (
                          <span key={i} className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-medium text-primary">{c.name || c}</span>
                        ))}
                      </div>
                    ) : <span className="text-xs text-body-color dark:text-dark-6">-</span>}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className={`text-sm font-semibold ${p.totalStock > 0 ? "text-green-600 dark:text-green-400" : "text-red-500"}`}>
                      {p.totalStock.toLocaleString("tr-TR")}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-center text-sm text-dark-5 dark:text-dark-6">{p.variantCount}</td>
                  <td className="px-4 py-3 text-xs text-body-color dark:text-dark-6">{fmtMs(p.providerUpdatedAtMs)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between border-t border-stroke px-4 py-3 dark:border-dark-3">
            <span className="text-xs text-body-color dark:text-dark-6">
              Sayfa {page}/{totalPages} ({total} ürün)
            </span>
            <div className="flex items-center gap-2">
              <button disabled={page <= 1} onClick={() => setPage(page - 1)} className="rounded-lg border border-stroke px-3 py-1.5 text-xs font-medium text-dark hover:bg-gray-50 disabled:opacity-40 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2">
                ← Önceki
              </button>
              <button disabled={page >= totalPages} onClick={() => setPage(page + 1)} className="rounded-lg border border-stroke px-3 py-1.5 text-xs font-medium text-dark hover:bg-gray-50 disabled:opacity-40 dark:border-dark-3 dark:text-white dark:hover:bg-dark-2">
                Sonraki →
              </button>
            </div>
          </div>
        )}
      </div>

      {/* ── Product Detail Panel ── */}
      {selectedId && (
        <div className={cardCls + " p-5"}>
          <div className="mb-4 flex items-center justify-between">
            <h3 className="text-lg font-bold text-dark dark:text-white">Ürün Detayı</h3>
            <button onClick={() => { setSelectedId(null); setDetail(null); }} className="text-xs text-body-color hover:text-dark dark:text-dark-6 dark:hover:text-white">✕ Kapat</button>
          </div>

          {detailLoading ? (
            <p className="text-sm text-body-color dark:text-dark-6">Yükleniyor...</p>
          ) : detail ? (
            <div className="space-y-4">
              {/* Product Info */}
              <div className="grid gap-3 md:grid-cols-3">
                <div>
                  <span className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Ürün Adı</span>
                  <div className="text-sm font-medium text-dark dark:text-white">{detail.name}</div>
                </div>
                <div>
                  <span className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Marka</span>
                  <div className="text-sm text-dark dark:text-white">{detail.brandName || "-"}</div>
                </div>
                <div>
                  <span className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Toplam Stok</span>
                  <div className="text-sm font-semibold text-dark dark:text-white">{detail.totalStock.toLocaleString("tr-TR")}</div>
                </div>
              </div>

              {detail.description && (
                <div>
                  <span className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Açıklama</span>
                  <div className="mt-1 text-sm text-dark-5 dark:text-dark-6">{detail.description}</div>
                </div>
              )}

              {/* Categories */}
              {detail.categories && detail.categories.length > 0 && (
                <div>
                  <span className="text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">Kategoriler</span>
                  <div className="mt-1 flex flex-wrap gap-1.5">
                    {detail.categories.map((c: any, i: number) => (
                      <span key={i} className="rounded-full bg-primary/10 px-2.5 py-1 text-xs font-medium text-primary">{c.name || c}</span>
                    ))}
                  </div>
                </div>
              )}

              {/* Variants Table */}
              {detail.variants && detail.variants.length > 0 && (
                <div>
                  <span className="mb-2 block text-[10px] font-medium uppercase tracking-wider text-body-color dark:text-dark-6">
                    Varyantlar ({detail.variants.length})
                  </span>
                  <div className="overflow-x-auto rounded-lg border border-stroke dark:border-dark-3">
                    <table className="w-full table-auto">
                      <thead>
                        <tr className="border-b border-stroke bg-gray-50 dark:border-dark-3 dark:bg-dark-2">
                          <th className="px-3 py-2 text-left text-[10px] font-medium uppercase text-body-color dark:text-dark-6">SKU</th>
                          <th className="px-3 py-2 text-right text-[10px] font-medium uppercase text-body-color dark:text-dark-6">Alış</th>
                          <th className="px-3 py-2 text-right text-[10px] font-medium uppercase text-body-color dark:text-dark-6">Satış</th>
                          <th className="px-3 py-2 text-right text-[10px] font-medium uppercase text-body-color dark:text-dark-6">İndirimli</th>
                          <th className="px-3 py-2 text-center text-[10px] font-medium uppercase text-body-color dark:text-dark-6">Stok</th>
                          <th className="px-3 py-2 text-left text-[10px] font-medium uppercase text-body-color dark:text-dark-6">Barkod</th>
                          <th className="px-3 py-2 text-left text-[10px] font-medium uppercase text-body-color dark:text-dark-6">HS Code</th>
                        </tr>
                      </thead>
                      <tbody>
                        {detail.variants.map((v) => {
                          const price = Array.isArray(v.prices) && v.prices.length > 0 ? v.prices[0] : null;
                          const stock = Array.isArray(v.stocks) && v.stocks.length > 0 ? v.stocks[0] : null;
                          const barcodes = Array.isArray(v.barcodeList) ? v.barcodeList.join(", ") : null;

                          return (
                            <tr key={v.variantId} className="border-b border-stroke/50 dark:border-dark-3/50">
                              <td className="px-3 py-2 text-xs font-mono text-dark dark:text-white">{v.sku || "-"}</td>
                              <td className="px-3 py-2 text-right text-xs text-dark-5 dark:text-dark-6">{price ? fmtPrice(price.buyPrice) : "-"}</td>
                              <td className="px-3 py-2 text-right text-xs font-medium text-dark dark:text-white">{price ? fmtPrice(price.sellPrice) : "-"}</td>
                              <td className="px-3 py-2 text-right text-xs text-orange-600 dark:text-orange-400">{price?.discountPrice ? fmtPrice(price.discountPrice) : "-"}</td>
                              <td className="px-3 py-2 text-center">
                                <span className={`text-xs font-semibold ${(stock?.stockCount ?? 0) > 0 ? "text-green-600 dark:text-green-400" : "text-red-500"}`}>
                                  {stock?.stockCount ?? 0}
                                </span>
                              </td>
                              <td className="max-w-[120px] truncate px-3 py-2 text-xs text-body-color dark:text-dark-6">{barcodes || "-"}</td>
                              <td className="px-3 py-2 text-xs text-body-color dark:text-dark-6">{v.hsCode || "-"}</td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>
          ) : (
            <p className="text-sm text-red-500">Ürün detayı yüklenemedi.</p>
          )}
        </div>
      )}
    </div>
  );
}