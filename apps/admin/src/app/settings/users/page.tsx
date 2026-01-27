"use client";

import React, { useEffect, useState } from "react";

type UserItem = {
  userId: string;
  email: string;
  displayName: string;
  status: string;
  roles: string[];
  createdAtUtc: string;
};

export default function TenantUsersPage() {
  const [items, setItems] = useState<UserItem[]>([]);
  const [err, setErr] = useState<string | null>(null);

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

    setItems(payload?.items || []);
  };

  useEffect(() => {
    load();
  }, []);

  const createUser = async () => {
    setErr(null);

    const res = await fetch("/api/tenant/users", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(form),
    });

    const payload = await res.json().catch(() => null);

    if (!res.ok) {
      setErr(payload?.message || JSON.stringify(payload));
      return;
    }

    setForm({ email: "", displayName: "", password: "", role: "Reporting" });
    await load();
  };

  const disable = async (id: string) => {
    const res = await fetch(`/api/tenant/users/${id}/disable`, { method: "POST" });
    if (!res.ok) setErr("Disable failed.");
    await load();
  };

  const activate = async (id: string) => {
    const res = await fetch(`/api/tenant/users/${id}/activate`, { method: "POST" });
    if (!res.ok) setErr("Activate failed.");
    await load();
  };

  return (
    <div className="p-4 sm:p-6">
      <div className="rounded-[10px] bg-white p-4 shadow-1 dark:bg-gray-dark dark:shadow-card">
        <h2 className="text-lg font-semibold text-dark dark:text-white">Firma Kullanıcıları</h2>

        {err && (
          <div className="mt-3 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-600 dark:text-red-400">
            {err}
          </div>
        )}

        <div className="mt-4 grid gap-3 md:grid-cols-4">
          <input className="rounded-lg border border-stroke bg-transparent px-3 py-2" placeholder="email"
            value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
          <input className="rounded-lg border border-stroke bg-transparent px-3 py-2" placeholder="display name"
            value={form.displayName} onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
          <input className="rounded-lg border border-stroke bg-transparent px-3 py-2" placeholder="password (min 8)"
            type="password"
            value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
          <select className="rounded-lg border border-stroke bg-transparent px-3 py-2"
            value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
            <option value="Admin">Admin</option>
            <option value="Integration">Integration</option>
            <option value="Reporting">Reporting</option>
          </select>
        </div>

        <button onClick={createUser}
          className="mt-3 rounded-lg bg-primary px-4 py-2 font-medium text-white hover:bg-opacity-90">
          Kullanıcı Oluştur
        </button>

        <div className="mt-6 overflow-x-auto">
          <table className="w-full table-auto">
            <thead>
              <tr className="text-left text-xs text-body-color dark:text-dark-6">
                <th className="px-2 py-2">Email</th>
                <th className="px-2 py-2">Name</th>
                <th className="px-2 py-2">Roles</th>
                <th className="px-2 py-2">Status</th>
                <th className="px-2 py-2">Action</th>
              </tr>
            </thead>
            <tbody>
              {items.map((u) => (
                <tr key={u.userId} className="border-t border-stroke dark:border-dark-3 text-sm">
                  <td className="px-2 py-2">{u.email}</td>
                  <td className="px-2 py-2">{u.displayName}</td>
                  <td className="px-2 py-2">{u.roles.join(", ")}</td>
                  <td className="px-2 py-2">{u.status}</td>
                  <td className="px-2 py-2">
                    {u.status === "Active" ? (
                      <button className="rounded border px-2 py-1" onClick={() => disable(u.userId)}>Disable</button>
                    ) : (
                      <button className="rounded border px-2 py-1" onClick={() => activate(u.userId)}>Activate</button>
                    )}
                  </td>
                </tr>
              ))}
              {!items.length && (
                <tr className="border-t border-stroke dark:border-dark-3 text-sm">
                  <td className="px-2 py-3" colSpan={5}>No users</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

      </div>
    </div>
  );
}
