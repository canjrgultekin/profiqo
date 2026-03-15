// Path: apps/admin/src/components/Auth/SignupWithPassword.tsx
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

const inputCls =
  "w-full rounded-lg border border-[#1E3048] bg-[#060E1A] py-3.5 pl-5 pr-5 text-sm text-white outline-none transition-colors placeholder:text-[#64748B] focus:border-primary";

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
      setError("Firma adı, firma kodu, e-posta ve şifre alanları zorunludur.");
      return;
    }

    if (password !== password2) {
      setError("Girdiğiniz şifreler eşleşmiyor.");
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
        setError((payload?.message || "Kayıt başarısız. Lütfen bilgilerinizi kontrol edin.") + extra);
        return;
      }

      if (payload?.hasTokens) {
        router.push("/");
      } else {
        router.push("/auth/sign-in");
      }
      router.refresh();
    } catch {
      setError("Bağlantı hatası oluştu. Lütfen tekrar deneyin.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={submit}>
      <div className="mb-4">
        <label className="mb-2 block text-sm font-medium text-[#CBD5E1]">
          Firma Adı
        </label>
        <input
          type="text"
          placeholder="Örn: Profiqo Demo"
          value={form.tenantName}
          onChange={(e) => setForm({ ...form, tenantName: e.target.value })}
          className={inputCls}
        />
      </div>

      <div className="mb-4">
        <label className="mb-2 block text-sm font-medium text-[#CBD5E1]">
          Firma Kodu
        </label>
        <input
          type="text"
          placeholder={autoSlug || "Örn: profiqo-demo"}
          value={form.tenantSlug}
          onChange={(e) => setForm({ ...form, tenantSlug: e.target.value })}
          className={inputCls}
        />
        <p className="mt-1.5 text-xs text-[#64748B]">
          Boş bırakırsanız otomatik oluşturulur: <span className="font-medium text-[#94A3B8]">{effectiveSlug || "-"}</span>
        </p>
      </div>

      <div className="mb-4">
        <label className="mb-2 block text-sm font-medium text-[#CBD5E1]">
          Ad Soyad
        </label>
        <input
          type="text"
          placeholder="Hesap yöneticisinin adı"
          value={form.displayName}
          onChange={(e) => setForm({ ...form, displayName: e.target.value })}
          className={inputCls}
        />
      </div>

      <div className="mb-4">
        <label className="mb-2 block text-sm font-medium text-[#CBD5E1]">
          E-posta Adresi
        </label>
        <input
          type="email"
          placeholder="ornek@firma.com"
          value={form.email}
          onChange={(e) => setForm({ ...form, email: e.target.value })}
          className={inputCls}
          autoComplete="email"
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-2 mb-5">
        <div>
          <label className="mb-2 block text-sm font-medium text-[#CBD5E1]">
            Şifre
          </label>
          <input
            type="password"
            placeholder="Min. 8 karakter"
            value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
            className={inputCls}
            autoComplete="new-password"
          />
        </div>
        <div>
          <label className="mb-2 block text-sm font-medium text-[#CBD5E1]">
            Şifre Tekrar
          </label>
          <input
            type="password"
            placeholder="Şifrenizi tekrarlayın"
            value={form.password2}
            onChange={(e) => setForm({ ...form, password2: e.target.value })}
            className={inputCls}
            autoComplete="new-password"
          />
        </div>
      </div>

      <div className="mb-5 flex items-center gap-2">
        <input
          type="checkbox"
          id="remember-signup"
          checked={remember}
          onChange={(e) => setRemember(e.target.checked)}
          className="h-4 w-4 rounded border-[#1E3048] bg-[#060E1A] text-primary accent-primary"
        />
        <label htmlFor="remember-signup" className="cursor-pointer text-sm text-[#94A3B8]">
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
            Hesap oluşturuluyor...
          </>
        ) : (
          "Hesap Oluştur"
        )}
      </button>
    </form>
  );
};

export default SignupWithPassword;