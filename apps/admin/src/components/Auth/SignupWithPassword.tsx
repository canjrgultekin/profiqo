"use client";

import React, { useMemo, useState } from "react";
import { useRouter } from "next/navigation";

function slugify(input: string): string {
  return input
    .trim()
    .toLowerCase()
    .replace(/ğ/g, "g")
    .replace(/ü/g, "u")
    .replace(/ş/g, "s")
    .replace(/ı/g, "i")
    .replace(/ö/g, "o")
    .replace(/ç/g, "c")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .replace(/-{2,}/g, "-");
}

const SignupWithPassword = () => {
  const router = useRouter();

  const [form, setForm] = useState({
    tenantName: "",
    tenantSlug: "",
    displayName: "",
    email: "",
    password: "",
    password2: "",
  });

  const [remember, setRemember] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const autoSlug = useMemo(() => slugify(form.tenantName), [form.tenantName]);
  const effectiveSlug = form.tenantSlug.trim() || autoSlug;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const tenantName = form.tenantName.trim();
    const tenantSlug = effectiveSlug;
    const displayName = form.displayName.trim();
    const email = form.email.trim();
    const password = form.password;
    const password2 = form.password2;

    if (!tenantName || !tenantSlug || !email || !password) {
      setError("Tenant adı, tenant slug, email ve şifre zorunlu.");
      return;
    }

    if (password !== password2) {
      setError("Şifreler aynı değil.");
      return;
    }

    setLoading(true);
    try {
      const res = await fetch("/api/auth/register", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          tenantName,
          tenantSlug,
          displayName,
          email,
          password,
          remember,
        }),
      });

      const payload = (await res.json().catch(() => null)) as
        | { ok?: boolean; message?: string; hasTokens?: boolean; errors?: any }
        | null;

      if (!res.ok || !payload?.ok) {
        const extra =
          payload?.errors ? `\n${JSON.stringify(payload.errors)}` : "";
        setError((payload?.message || "Register başarısız.") + extra);
        return;
      }

      if (payload?.hasTokens) {
        router.push("/");
      } else {
        router.push("/auth/sign-in");
      }
      router.refresh();
    } catch {
      setError("Register sırasında beklenmeyen hata.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={submit}>
      <div className="mb-4">
        <label className="mb-2.5 block font-medium text-dark dark:text-white">
          Şirket Adı (Tenant)
        </label>
        <input
          type="text"
          placeholder="Örn: Profiqo Demo"
          value={form.tenantName}
          onChange={(e) =>
            setForm({ ...form, tenantName: e.target.value })
          }
          className="w-full rounded-lg border border-stroke bg-transparent py-4 pl-6 pr-6 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
        />
      </div>

      <div className="mb-4">
        <label className="mb-2.5 block font-medium text-dark dark:text-white">
          Tenant Slug
        </label>
        <input
          type="text"
          placeholder={autoSlug || "örn: profiqo-demo"}
          value={form.tenantSlug}
          onChange={(e) =>
            setForm({ ...form, tenantSlug: e.target.value })
          }
          className="w-full rounded-lg border border-stroke bg-transparent py-4 pl-6 pr-6 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
        />
        <p className="mt-2 text-xs text-body-color dark:text-dark-6">
          Boş bırakırsan otomatik: <b>{effectiveSlug || "-"}</b>
        </p>
      </div>

      <div className="mb-4">
        <label className="mb-2.5 block font-medium text-dark dark:text-white">
          Ad Soyad
        </label>
        <input
          type="text"
          placeholder="Örn: Can Küçükgültekin"
          value={form.displayName}
          onChange={(e) =>
            setForm({ ...form, displayName: e.target.value })
          }
          className="w-full rounded-lg border border-stroke bg-transparent py-4 pl-6 pr-6 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
        />
      </div>

      <div className="mb-4">
        <label className="mb-2.5 block font-medium text-dark dark:text-white">
          Email
        </label>
        <input
          type="email"
          placeholder="Email adresini gir"
          value={form.email}
          onChange={(e) => setForm({ ...form, email: e.target.value })}
          className="w-full rounded-lg border border-stroke bg-transparent py-4 pl-6 pr-6 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
          autoComplete="email"
        />
      </div>

      <div className="mb-4">
        <label className="mb-2.5 block font-medium text-dark dark:text-white">
          Şifre
        </label>
        <input
          type="password"
          placeholder="Şifre"
          value={form.password}
          onChange={(e) =>
            setForm({ ...form, password: e.target.value })
          }
          className="w-full rounded-lg border border-stroke bg-transparent py-4 pl-6 pr-6 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
          autoComplete="new-password"
        />
      </div>

      <div className="mb-6">
        <label className="mb-2.5 block font-medium text-dark dark:text-white">
          Şifre Tekrar
        </label>
        <input
          type="password"
          placeholder="Şifre tekrar"
          value={form.password2}
          onChange={(e) =>
            setForm({ ...form, password2: e.target.value })
          }
          className="w-full rounded-lg border border-stroke bg-transparent py-4 pl-6 pr-6 text-dark outline-none focus:border-primary dark:border-dark-3 dark:text-white"
          autoComplete="new-password"
        />
      </div>

      <div className="mb-6 flex items-center gap-2">
        <input
          type="checkbox"
          checked={remember}
          onChange={(e) => setRemember(e.target.checked)}
        />
        <span className="text-sm text-body-color dark:text-dark-6">
          Beni hatırla
        </span>
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
        {loading ? "Oluşturuluyor..." : "Tenant Oluştur"}
      </button>
    </form>
  );
};

export default SignupWithPassword;
