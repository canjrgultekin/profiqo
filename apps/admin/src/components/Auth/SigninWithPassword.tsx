"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import React, { useState } from "react";

const SigninWithPassword = () => {
  const router = useRouter();

  const [data, setData] = useState({
    tenantSlug: "",
    email: "",
    password: "",
  });

  const [remember, setRemember] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const tenantSlug = data.tenantSlug.trim();
    const email = data.email.trim();
    const password = data.password;

    if (!tenantSlug || !email || !password) {
      setError("Tenant slug, email ve şifre zorunlu.");
      return;
    }

    setLoading(true);
    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ tenantSlug, email, password, remember }),
      });

      const payload = (await res.json().catch(() => null)) as
        | { ok?: boolean; message?: string; errors?: any }
        | null;

      if (!res.ok || !payload?.ok) {
        const extra = payload?.errors ? `\n${JSON.stringify(payload.errors)}` : "";
        setError((payload?.message || "Login başarısız.") + extra);
        return;
      }

      router.push("/");
      router.refresh();
    } catch {
      setError("Login sırasında beklenmeyen hata.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      <div className="mb-4">
        <label className="mb-2.5 block font-medium text-dark dark:text-white">
          Tenant Slug
        </label>
        <input
          type="text"
          placeholder="örn: demo-shop"
          value={data.tenantSlug}
          onChange={(e) => setData({ ...data, tenantSlug: e.target.value })}
          className="w-full rounded-lg border border-stroke bg-transparent py-4 pl-6 pr-6 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
          autoComplete="organization"
        />
      </div>

      <div className="mb-4">
        <label className="mb-2.5 block font-medium text-dark dark:text-white">
          Email
        </label>
        <input
          type="email"
          placeholder="Email adresini gir"
          value={data.email}
          onChange={(e) => setData({ ...data, email: e.target.value })}
          className="w-full rounded-lg border border-stroke bg-transparent py-4 pl-6 pr-6 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
          autoComplete="email"
        />
      </div>

      <div className="mb-6">
        <label className="mb-2.5 block font-medium text-dark dark:text-white">
          Şifre
        </label>
        <input
          type="password"
          placeholder="Şifreni gir"
          value={data.password}
          onChange={(e) => setData({ ...data, password: e.target.value })}
          className="w-full rounded-lg border border-stroke bg-transparent py-4 pl-6 pr-6 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
          autoComplete="current-password"
        />
      </div>

      <div className="mb-6 flex items-center justify-between gap-2">
        <label className="flex cursor-pointer select-none items-center gap-2">
          <input
            type="checkbox"
            checked={remember}
            onChange={(e) => setRemember(e.target.checked)}
          />
          <span className="text-sm text-body-color dark:text-dark-6">
            Beni hatırla
          </span>
        </label>

        <Link
          href="/auth/forgot-password"
          className="text-sm text-primary hover:underline"
        >
          Şifremi unuttum
        </Link>
      </div>

      {error && (
        <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-600 dark:text-red-400 whitespace-pre-wrap">
          {error}
        </div>
      )}

      <button
        type="submit"
        disabled={loading}
        className="flex w-full items-center justify-center gap-2 rounded-lg bg-primary p-4 font-medium text-white transition hover:bg-opacity-90 disabled:opacity-60"
      >
        {loading ? "Giriş yapılıyor..." : "Giriş Yap"}
      </button>
    </form>
  );
};

export default SigninWithPassword;
