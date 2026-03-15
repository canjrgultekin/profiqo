// Path: apps/admin/src/components/Auth/SigninWithPassword.tsx
"use client";

import { useRouter } from "next/navigation";
import React, { useState } from "react";

const inputCls =
  "w-full rounded-lg border border-[#1E3048] bg-[#060E1A] py-3.5 pl-5 pr-5 text-sm text-white outline-none transition-colors placeholder:text-[#64748B] focus:border-primary";

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
      setError("Firma kodu, e-posta ve şifre alanları zorunludur.");
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
        setError((payload?.message || "Giriş başarısız. Bilgilerinizi kontrol edin.") + extra);
        return;
      }

      router.push("/");
      router.refresh();
    } catch {
      setError("Bağlantı hatası oluştu. Lütfen tekrar deneyin.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      <div className="mb-4">
        <label className="mb-2 block text-sm font-medium text-[#CBD5E1]">
          Firma Kodu
        </label>
        <input
          type="text"
          placeholder="Örn: demo-shop"
          value={data.tenantSlug}
          onChange={(e) => setData({ ...data, tenantSlug: e.target.value })}
          className={inputCls}
          autoComplete="organization"
        />
      </div>

      <div className="mb-4">
        <label className="mb-2 block text-sm font-medium text-[#CBD5E1]">
          E-posta Adresi
        </label>
        <input
          type="email"
          placeholder="ornek@firma.com"
          value={data.email}
          onChange={(e) => setData({ ...data, email: e.target.value })}
          className={inputCls}
          autoComplete="email"
        />
      </div>

      <div className="mb-5">
        <label className="mb-2 block text-sm font-medium text-[#CBD5E1]">
          Şifre
        </label>
        <input
          type="password"
          placeholder="Şifrenizi girin"
          value={data.password}
          onChange={(e) => setData({ ...data, password: e.target.value })}
          className={inputCls}
          autoComplete="current-password"
        />
      </div>

      <div className="mb-5 flex items-center gap-2">
        <input
          type="checkbox"
          id="remember-me"
          checked={remember}
          onChange={(e) => setRemember(e.target.checked)}
          className="h-4 w-4 rounded border-[#1E3048] bg-[#060E1A] text-primary accent-primary"
        />
        <label htmlFor="remember-me" className="cursor-pointer text-sm text-[#94A3B8]">
          Oturumumu açık tut
        </label>
      </div>

      {error && (
        <div className="mb-4 rounded-lg border border-red/20 bg-red/5 px-4 py-3 text-sm text-red-light whitespace-pre-wrap">
          {error}
        </div>
      )}

      <button
        type="submit"
        disabled={loading}
        className="flex w-full items-center justify-center gap-2 rounded-lg bg-primary py-3.5 text-sm font-semibold text-white transition-all hover:bg-primary/90 hover:shadow-lg hover:shadow-primary/20 disabled:opacity-60"
      >
        {loading ? (
          <>
            <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
            Giriş yapılıyor...
          </>
        ) : (
          "Giriş Yap"
        )}
      </button>
    </form>
  );
};

export default SigninWithPassword;