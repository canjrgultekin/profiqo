// Path: apps/admin/src/app/(dashboard)/products/page.tsx
"use client";

import React, { useCallback, useEffect, useMemo, useState } from "react";
import { type ColumnDef } from "@tanstack/react-table";
import { DataTable } from "@/components/DataTable";

// ── Types ─────────────────────────────────────────────────────────────────────

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

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmtMs(ms: number): string {
  if (!ms || ms <= 0) return "-";
  return new Date(ms).toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

function fmtPrice(n: number | null | undefined): string {
  if (n === null || n === undefined) return "-";
  return n.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function exportCSV(items: ProductItem[]) {
  const bom = "\uFEFF";
  const headers = ["Ürün Adı", "Marka", "Kategoriler", "Stok", "Varyant", "Son Güncelleme"];
  const rows = items.map((p) => [
    p.name, p.brandName || "", p.categories?.map((c) => c.name).join(", ") || "",
    String(p.totalStock), String(p.variantCount), fmtMs(p.providerUpdatedAtMs),
  ]);
  const csv = bom + [headers, ...rows].map((r) => r.map((c) => `"${c.replace(/"/g, '""')}"`).join(";")).join("\n");
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url; a.download = `profiqo-urunler-${new Date().toISOString().slice(0, 10)}.csv`; a.click();
  URL.revokeObjectURL(url);
}

// ── Component ─────────────────────────────────────────────────────────────────

export default function ProductsPage() {
  const [items, setItems] = useState<ProductItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<ProductDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const loadProducts = useCallback(async () => {
    setLoading(true); setErr(null);
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (search.trim()) params.set("q", search.trim());
    try {
      const res = await fetch(`/api/products?${params}`, { cache: "no-store" });
      const payload = await res.json().catch(() => null);
      if (!res.ok) { setErr(payload?.message || "Ürünler yüklenemedi."); setItems([]); setTotal(0); return; }
      setItems(payload?.items || []); setTotal(payload?.total || 0);
    } catch { setErr("Bağlantı hatası oluştu."); setItems([]); setTotal(0); }
    finally { setLoading(false); }
  }, [page, pageSize, search]);

  useEffect(() => { loadProducts(); }, [loadProducts]);

  const handleSearch = () => { setPage(1); };

  const loadDetail = async (productId: string) => {
    if (selectedId === productId) { setSelectedId(null); setDetail(null); return; }
    setSelectedId(productId); setDetailLoading(true); setDetail(null);
    try {
      const res = await fetch(`/api/products/${productId}`, { cache: "no-store" });
      const payload = await res.json().catch(() => null);
      if (res.ok) setDetail(payload);
    } catch { /* ignore */ }
    setDetailLoading(false);
  };

  // ── Columns ─────────────────────────────────────────────────────────────

  const columns: ColumnDef<ProductItem, any>[] = useMemo(() => [
    {
      accessorKey: "name",
      header: "Ürün",
      enableSorting: true,
      meta: { thClassName: "px-5 py-3", tdClassName: "px-5 py-3.5" },
      cell: ({ row }) => (
        <div>
          <div className="font-medium text-dark dark:text-white">{row.original.name}</div>
          {row.original.description && <div className="mt-0.5 max-w-xs truncate text-xs text-dark-5 dark:text-dark-6">{row.original.description}</div>}
        </div>
      ),
    },
    {
      accessorKey: "brandName",
      header: "Marka",
      enableSorting: true,
      meta: { tdClassName: "px-4 py-3.5 text-dark-5 dark:text-dark-6" },
      cell: ({ getValue }) => (getValue() as string | null) || "-",
    },
    {
      id: "categories",
      header: "Kategori",
      enableSorting: false,
      cell: ({ row }) => {
        const cats = row.original.categories;
        if (!cats || cats.length === 0) return <span className="text-dark-6">-</span>;
        return (
          <div className="flex flex-wrap gap-1">
            {cats.map((c, i) => <span key={i} className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-medium text-primary">{c.name}</span>)}
          </div>
        );
      },
    },
    {
      accessorKey: "totalStock",
      header: "Stok",
      enableSorting: true,
      meta: { thClassName: "px-4 py-3 text-center", tdClassName: "px-4 py-3.5 text-center" },
      cell: ({ getValue }) => {
        const v = getValue() as number;
        return <span className={`font-semibold ${v > 0 ? "text-green dark:text-green-light" : "text-red dark:text-red-light"}`}>{v.toLocaleString("tr-TR")}</span>;
      },
    },
    {
      accessorKey: "variantCount",
      header: "Varyant",
      enableSorting: true,
      meta: { thClassName: "px-4 py-3 text-center", tdClassName: "px-4 py-3.5 text-center text-dark-5 dark:text-dark-6" },
    },
    {
      accessorKey: "providerUpdatedAtMs",
      header: "Güncelleme",
      enableSorting: true,
      meta: { tdClassName: "whitespace-nowrap px-4 py-3.5 text-xs text-dark-5 dark:text-dark-6" },
      cell: ({ getValue }) => fmtMs(getValue() as number),
    },
  ], []);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-dark dark:text-white">Ürünler</h1>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">Entegrasyonlardan senkronize edilen ürün kataloğu.</p>
      </div>

      {err && (
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <svg className="h-5 w-5 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-red-600 dark:text-red-300">{err}</p>
        </div>
      )}

      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex flex-col gap-4 border-b border-stroke px-5 py-4 dark:border-dark-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <h2 className="text-base font-semibold text-dark dark:text-white">Ürün Kataloğu</h2>
            <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">{total.toLocaleString("tr-TR")} ürün</span>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative">
              <input type="text" value={search} onChange={(e) => { setSearch(e.target.value); setPage(1); }} onKeyDown={(e) => e.key === "Enter" && handleSearch()} placeholder="Ürün veya marka ara..." className="h-9 w-full rounded-lg border border-stroke bg-transparent pl-9 pr-3 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white sm:w-56" />
              <svg className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-dark-5 dark:text-dark-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-5.197-5.197m0 0A7.5 7.5 0 105.196 5.196a7.5 7.5 0 0010.607 10.607z" /></svg>
            </div>
            {items.length > 0 && (
              <button onClick={() => exportCSV(items)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-stroke px-3 text-xs font-medium text-dark-5 transition-colors hover:border-primary hover:text-primary dark:border-dark-3 dark:text-dark-6 dark:hover:border-primary dark:hover:text-primary">
                <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5M16.5 12L12 16.5m0 0L7.5 12m4.5 4.5V3" /></svg>
                CSV
              </button>
            )}
          </div>
        </div>

        <DataTable
          columns={columns}
          data={items}
          loading={loading}
          getRowId={(row) => row.productId}
          onRowClick={(row) => loadDetail(row.original.productId)}
          rowClassName={(row) => selectedId === row.original.productId ? "bg-primary/5 dark:bg-primary/10" : ""}
          emptyTitle={search.trim() ? `"${search}" için ürün bulunamadı.` : "Henüz ürün senkronize edilmemiş."}
          emptyDescription={!search.trim() ? "Entegrasyonlar sayfasından senkronizasyon başlatın." : undefined}
          pagination={total > 0 ? { page, pageSize, total, onPageChange: setPage } : undefined}
        />
      </div>

      {/* Product Detail Panel */}
      {selectedId && (
        <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="text-lg font-semibold text-dark dark:text-white">Ürün Detayı</h3>
            <button onClick={() => { setSelectedId(null); setDetail(null); }} className="flex h-7 w-7 items-center justify-center rounded-full text-dark-5 transition-colors hover:bg-gray-2 hover:text-dark dark:text-dark-6 dark:hover:bg-dark-3 dark:hover:text-white">
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" /></svg>
            </button>
          </div>

          {detailLoading ? (
            <div className="space-y-3">
              <div className="h-5 w-64 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
              <div className="h-4 w-40 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
              <div className="h-4 w-48 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
            </div>
          ) : detail ? (
            <div className="space-y-5">
              <div className="grid gap-4 md:grid-cols-3">
                <div><span className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Ürün Adı</span><div className="mt-0.5 text-sm font-medium text-dark dark:text-white">{detail.name}</div></div>
                <div><span className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Marka</span><div className="mt-0.5 text-sm text-dark dark:text-white">{detail.brandName || "-"}</div></div>
                <div><span className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Toplam Stok</span><div className="mt-0.5 text-sm font-semibold text-dark dark:text-white">{detail.totalStock.toLocaleString("tr-TR")}</div></div>
              </div>

              {detail.description && (
                <div><span className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Açıklama</span><div className="mt-1 text-sm text-dark-5 dark:text-dark-6">{detail.description}</div></div>
              )}

              {detail.categories && detail.categories.length > 0 && (
                <div>
                  <span className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Kategoriler</span>
                  <div className="mt-1.5 flex flex-wrap gap-1.5">{detail.categories.map((c, i) => <span key={i} className="rounded-full bg-primary/10 px-2.5 py-1 text-xs font-medium text-primary">{c.name}</span>)}</div>
                </div>
              )}

              {detail.variants && detail.variants.length > 0 && (
                <div>
                  <div className="mb-2 flex items-center gap-2">
                    <span className="text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:text-dark-6">Varyantlar</span>
                    <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-medium text-primary">{detail.variants.length}</span>
                  </div>
                  <div className="overflow-x-auto rounded-lg border border-stroke dark:border-dark-3">
                    <table className="w-full">
                      <thead><tr className="border-b border-stroke text-left text-[10px] font-medium uppercase tracking-wider text-dark-5 dark:border-dark-3 dark:text-dark-6">
                        <th className="px-3 py-2">SKU</th><th className="px-3 py-2 text-right">Alış</th><th className="px-3 py-2 text-right">Satış</th><th className="px-3 py-2 text-right">İndirimli</th><th className="px-3 py-2 text-center">Stok</th><th className="px-3 py-2">Barkod</th><th className="px-3 py-2">HS Kodu</th>
                      </tr></thead>
                      <tbody className="divide-y divide-stroke dark:divide-dark-3">
                        {detail.variants.map((v) => {
                          const price = Array.isArray(v.prices) && v.prices.length > 0 ? v.prices[0] : null;
                          const stock = Array.isArray(v.stocks) && v.stocks.length > 0 ? v.stocks[0] : null;
                          const barcodes = Array.isArray(v.barcodeList) ? v.barcodeList.join(", ") : null;
                          return (
                            <tr key={v.variantId} className="text-xs transition-colors hover:bg-gray-1 dark:hover:bg-dark-2">
                              <td className="px-3 py-2 font-mono text-dark dark:text-white">{v.sku || "-"}</td>
                              <td className="px-3 py-2 text-right text-dark-5 dark:text-dark-6">{price ? fmtPrice(price.buyPrice) : "-"}</td>
                              <td className="px-3 py-2 text-right font-medium text-dark dark:text-white">{price ? fmtPrice(price.sellPrice) : "-"}</td>
                              <td className="px-3 py-2 text-right text-accent-dark dark:text-accent-light">{price?.discountPrice ? fmtPrice(price.discountPrice) : "-"}</td>
                              <td className="px-3 py-2 text-center"><span className={`font-semibold ${(stock?.stockCount ?? 0) > 0 ? "text-green dark:text-green-light" : "text-red dark:text-red-light"}`}>{stock?.stockCount ?? 0}</span></td>
                              <td className="max-w-[120px] truncate px-3 py-2 text-dark-5 dark:text-dark-6">{barcodes || "-"}</td>
                              <td className="px-3 py-2 text-dark-5 dark:text-dark-6">{v.hsCode || "-"}</td>
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
            <p className="text-sm text-red dark:text-red-light">Ürün detayı yüklenemedi.</p>
          )}
        </div>
      )}
    </div>
  );
}