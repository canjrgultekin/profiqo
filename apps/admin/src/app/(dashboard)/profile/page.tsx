// Path: apps/admin/src/app/(dashboard)/profile/page.tsx
"use client";

import { useEffect, useState } from "react";

type UserData = {
  displayName: string | null;
  email: string | null;
  userId: string | null;
  tenantId: string | null;
  roles: string[];
};

function getInitials(name: string | null): string {
  if (!name) return "?";
  const parts = name.trim().split(/\s+/);
  if (parts.length >= 2) {
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
  return name.substring(0, 2).toUpperCase();
}

function roleBadgeClass(role: string): string {
  const r = role.toLowerCase();
  if (r === "admin" || r === "owner")
    return "bg-primary/10 text-primary";
  if (r === "integration")
    return "bg-blue/10 text-blue";
  return "bg-green/10 text-green";
}

export default function ProfilePage() {
  const [user, setUser] = useState<UserData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch("/api/auth/me")
      .then((r) => r.json())
      .then((data) => {
        if (data?.ok && data?.user) {
          setUser({
            displayName: data.user.displayName,
            email: data.user.email,
            userId: data.user.userId,
            tenantId: data.user.tenantId,
            roles: data.user.roles || [],
          });
        } else {
          setError("Kullanıcı bilgileri alınamadı.");
        }
      })
      .catch(() => setError("Profil yüklenirken bir hata oluştu."))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="mx-auto w-full max-w-[720px] space-y-6">
        <div>
          <div className="h-8 w-40 animate-pulse rounded-lg bg-gray-200 dark:bg-dark-3" />
          <div className="mt-2 h-4 w-60 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
        </div>
        <div className="rounded-xl bg-white p-8 shadow-1 dark:bg-gray-dark dark:shadow-card">
          <div className="flex items-center gap-5">
            <div className="h-20 w-20 animate-pulse rounded-full bg-gray-200 dark:bg-dark-3" />
            <div className="space-y-2">
              <div className="h-5 w-48 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
              <div className="h-4 w-36 animate-pulse rounded bg-gray-200 dark:bg-dark-3" />
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (error || !user) {
    return (
      <div className="mx-auto w-full max-w-[720px]">
        <div className="flex items-center gap-3 rounded-xl border border-red-200 bg-red-50 px-5 py-4 dark:border-red-900/40 dark:bg-red-900/20">
          <svg className="h-5 w-5 shrink-0 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <p className="text-sm text-red-600 dark:text-red-300">{error || "Profil yüklenemedi."}</p>
        </div>
      </div>
    );
  }

  const initials = getInitials(user.displayName);
  const displayName = user.displayName || "İsimsiz Kullanıcı";

  return (
    <div className="mx-auto w-full max-w-[720px] space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="text-2xl font-bold text-dark dark:text-white">Profil</h1>
        <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
          Hesap bilgilerinizi görüntüleyin.
        </p>
      </div>

      {/* Profile Card */}
      <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card sm:p-8">
        <div className="flex flex-col items-center gap-5 sm:flex-row sm:items-start">
          {/* Avatar */}
          <div className="flex h-20 w-20 shrink-0 items-center justify-center rounded-full bg-primary text-2xl font-bold text-white">
            {initials}
          </div>

          {/* Info */}
          <div className="text-center sm:text-left">
            <h2 className="text-xl font-bold text-dark dark:text-white">
              {displayName}
            </h2>
            <p className="mt-1 text-sm text-dark-5 dark:text-dark-6">
              {user.email || "E-posta belirtilmemiş"}
            </p>
            {user.roles.length > 0 && (
              <div className="mt-3 flex flex-wrap justify-center gap-1.5 sm:justify-start">
                {user.roles.map((role) => (
                  <span
                    key={role}
                    className={`inline-flex rounded-full px-3 py-1 text-xs font-semibold ${roleBadgeClass(role)}`}
                  >
                    {role}
                  </span>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Account Details */}
      <div className="rounded-xl border border-stroke bg-white p-6 shadow-1 dark:border-dark-3 dark:bg-gray-dark dark:shadow-card sm:p-8">
        <h3 className="mb-5 text-lg font-semibold text-dark dark:text-white">
          Hesap Bilgileri
        </h3>

        <div className="space-y-4">
          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-4">
            <span className="w-36 shrink-0 text-sm font-medium text-dark-5 dark:text-dark-6">
              Ad Soyad
            </span>
            <span className="text-sm font-medium text-dark dark:text-white">
              {displayName}
            </span>
          </div>

          <hr className="border-stroke dark:border-dark-3" />

          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-4">
            <span className="w-36 shrink-0 text-sm font-medium text-dark-5 dark:text-dark-6">
              E-posta
            </span>
            <span className="text-sm font-medium text-dark dark:text-white">
              {user.email || "-"}
            </span>
          </div>

          <hr className="border-stroke dark:border-dark-3" />

          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-4">
            <span className="w-36 shrink-0 text-sm font-medium text-dark-5 dark:text-dark-6">
              Roller
            </span>
            <span className="text-sm font-medium text-dark dark:text-white">
              {user.roles.length > 0 ? user.roles.join(", ") : "-"}
            </span>
          </div>

          <hr className="border-stroke dark:border-dark-3" />

          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-4">
            <span className="w-36 shrink-0 text-sm font-medium text-dark-5 dark:text-dark-6">
              Kullanıcı ID
            </span>
            <span className="font-mono text-xs text-dark-5 dark:text-dark-6">
              {user.userId || "-"}
            </span>
          </div>

          <hr className="border-stroke dark:border-dark-3" />

          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-4">
            <span className="w-36 shrink-0 text-sm font-medium text-dark-5 dark:text-dark-6">
              Firma (Tenant) ID
            </span>
            <span className="font-mono text-xs text-dark-5 dark:text-dark-6">
              {user.tenantId || "-"}
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}