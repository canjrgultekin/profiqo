"use client";

import React, { useEffect, useState } from "react";

type UserItem = {
  userId: string;
  email: string;
  displayName: string;
  status: string;
  roles: string[] | string | null;
  createdAtUtc: string;
};

function normalizeRoles(r: UserItem["roles"]): string[] {
  if (!r) return [];
  if (Array.isArray(r)) return r.filter(Boolean);
  const s = String(r).trim();
  if (!s) return [];
  return s.split(",").map((x) => x.trim()).filter(Boolean);
}

function roleBadgeClass(role: string): string {
  const r = role.toLowerCase();
  if (r === "admin" || r === "owner")
    return "bg-primary/10 text-primary";
  if (r === "integration")
    return "bg-blue/10 text-blue";
  return "bg-green/10 text-green";
}

function statusBadge(status: string): { label: string; className: string } {
  const s = (status || "").toLowerCase();
  if (s === "active")
    return {
      label: "Aktif",
      className: "bg-green-light-7 text-green-dark dark:bg-green/10 dark:text-green-light-3",
    };
  return {
    label: status || "Pasif",
    className: "bg-gray-200 text-dark-4 dark:bg-dark-3 dark:text-dark-6",
  };
}

export default function TenantUsersPage() {
  const [items, setItems] = useState<UserItem[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const [form, setForm] = useState({
    email: "",
    displayName: "",
    password: "",
    role: "Reporting",
  });

  const load = async () => {
    setErr(null);
    const res = await fetch("/api/tenant/users", { cache: "no-store" });
    const payload = await res.json().catch(() => null);

    if (!res.ok) {
      setErr(payload?.message || "Failed to load users.");
      setItems([]);
      return;
    }

    const raw = payload?.items || [];
    const mapped: UserItem[] = Array.isArray(raw)
      ? raw.map((u: any) => ({
          userId: u?.userId ?? u?.UserId ?? "",
          email: u?.email ?? u?.Email ?? "",
          displayName: u?.displayName ?? u?.DisplayName ?? "",
          status: u?.status ?? u?.Status ?? "",
          roles: u?.roles ?? u?.Roles ?? null,
          createdAtUtc: u?.createdAtUtc ?? u?.CreatedAtUtc ?? "",
        }))
      : [];

    setItems(mapped);
  };

  useEffect(() => {
    load();
  }, []);

  const createUser = async () => {
    setErr(null);
    setCreating(true);

    const res = await fetch("/api/tenant/users", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(form),
    });

    const payload = await res.json().catch(() => null);

    if (!res.ok) {
      setErr(payload?.message || JSON.stringify(payload));
      setCreating(false);
      return;
    }

    setForm({ email: "", displayName: "", password: "", role: "Reporting" });
    setCreating(false);
    await load();
  };

  const disable = async (id: string) => {
    const res = await fetch(`/api/tenant/users/${id}/disable`, {
      method: "POST",
    });
    if (!res.ok) setErr("Disable failed.");
    await load();
  };

  const activate = async (id: string) => {
    const res = await fetch(`/api/tenant/users/${id}/activate`, {
      method: "POST",
    });
    if (!res.ok) setErr("Activate failed.");
    await load();
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-dark dark:text-white">
          Kullanıcı Yönetimi
        </h1>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
          Firma kullanıcılarını ekleyin ve yönetin.
        </p>
      </div>

      {err && (
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <svg className="h-5 w-5 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-red-600 dark:text-red-300">{err}</p>
        </div>
      )}

      {/* Create User Form */}
      <div className="rounded-xl border border-stroke bg-white p-5 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <h2 className="text-lg font-semibold text-dark dark:text-white">
          Yeni Kullanıcı Ekle
        </h2>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
          Kullanıcı bilgilerini doldurup rolünü seçin.
        </p>

        <div className="mt-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-dark dark:text-white">
              E-posta
            </label>
            <input
              className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white"
              placeholder="kullanici@firma.com"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-dark dark:text-white">
              Ad Soyad
            </label>
            <input
              className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white"
              placeholder="Ad Soyad"
              value={form.displayName}
              onChange={(e) =>
                setForm({ ...form, displayName: e.target.value })
              }
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-dark dark:text-white">
              Şifre
            </label>
            <input
              className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white"
              placeholder="Min. 8 karakter"
              type="password"
              value={form.password}
              onChange={(e) =>
                setForm({ ...form, password: e.target.value })
              }
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-dark dark:text-white">
              Rol
            </label>
            <select
              className="w-full rounded-lg border border-stroke bg-transparent px-4 py-2.5 text-sm text-dark outline-none transition-colors focus:border-primary dark:border-dark-3 dark:text-white dark:bg-gray-dark"
              value={form.role}
              onChange={(e) => setForm({ ...form, role: e.target.value })}
            >
              <option value="Admin">Admin</option>
              <option value="Integration">Integration</option>
              <option value="Reporting">Reporting</option>
            </select>
          </div>
        </div>

        <button
          onClick={createUser}
          disabled={creating}
          className="mt-5 inline-flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-sm font-medium text-white transition-opacity hover:opacity-90 disabled:opacity-60"
        >
          <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M19 7.5v3m0 0v3m0-3h3m-3 0h-3m-2.25-4.125a3.375 3.375 0 11-6.75 0 3.375 3.375 0 016.75 0zM4 19.235v-.11a6.375 6.375 0 0112.75 0v.109A12.318 12.318 0 0110.374 21c-2.331 0-4.512-.645-6.374-1.766z" />
          </svg>
          Kullanıcı Oluştur
        </button>
      </div>

      {/* Users Table */}
      <div className="rounded-xl border border-stroke bg-white shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card">
        <div className="flex items-center justify-between border-b border-stroke px-5 py-4 dark:border-dark-3">
          <h2 className="text-base font-semibold text-dark dark:text-white">
            Mevcut Kullanıcılar
          </h2>
          <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-medium text-primary">
            {items.length} kullanıcı
          </span>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-stroke text-left text-xs font-medium uppercase tracking-wider text-dark-5 dark:border-dark-3 dark:text-dark-6">
                <th className="px-5 py-3">Kullanıcı</th>
                <th className="px-4 py-3">Roller</th>
                <th className="px-4 py-3">Durum</th>
                <th className="px-4 py-3">Oluşturulma</th>
                <th className="px-4 py-3 text-right">İşlem</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-stroke dark:divide-dark-3">
              {items.map((u) => {
                const rolesArr = normalizeRoles(u.roles);
                const sBadge = statusBadge(u.status);

                return (
                  <tr
                    key={u.userId}
                    className="text-sm transition-colors hover:bg-gray-1 dark:hover:bg-dark-2"
                  >
                    <td className="px-5 py-3.5">
                      <div className="flex items-center gap-3">
                        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
                          {(u.displayName || u.email || "?")[0].toUpperCase()}
                        </div>
                        <div>
                          <p className="font-medium text-dark dark:text-white">
                            {u.displayName || "-"}
                          </p>
                          <p className="text-xs text-dark-5 dark:text-dark-6">
                            {u.email}
                          </p>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3.5">
                      <div className="flex flex-wrap gap-1">
                        {rolesArr.length > 0
                          ? rolesArr.map((role) => (
                              <span
                                key={role}
                                className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${roleBadgeClass(role)}`}
                              >
                                {role}
                              </span>
                            ))
                          : <span className="text-dark-6">-</span>}
                      </div>
                    </td>
                    <td className="px-4 py-3.5">
                      <span
                        className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${sBadge.className}`}
                      >
                        {sBadge.label}
                      </span>
                    </td>
                    <td className="px-4 py-3.5 text-dark-5 dark:text-dark-6">
                      {u.createdAtUtc
                        ? new Date(u.createdAtUtc).toLocaleDateString("tr-TR")
                        : "-"}
                    </td>
                    <td className="px-4 py-3.5 text-right">
                      {u.status === "Active" ? (
                        <button
                          onClick={() => disable(u.userId)}
                          className="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-medium text-red transition-colors hover:bg-red-50 dark:border-red/20 dark:hover:bg-red/10"
                        >
                          Devre Dışı
                        </button>
                      ) : (
                        <button
                          onClick={() => activate(u.userId)}
                          className="rounded-lg border border-green-200 px-3 py-1.5 text-xs font-medium text-green transition-colors hover:bg-green-50 dark:border-green/20 dark:hover:bg-green/10"
                        >
                          Aktifleştir
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}

              {!items.length && (
                <tr>
                  <td
                    className="px-5 py-12 text-center text-sm text-dark-5 dark:text-dark-6"
                    colSpan={5}
                  >
                    Henüz kullanıcı yok.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
