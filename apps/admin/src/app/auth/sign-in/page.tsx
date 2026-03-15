// Path: apps/admin/src/app/auth/sign-in/page.tsx
import Signin from "@/components/Auth/Signin";
import type { Metadata } from "next";

export const metadata: Metadata = { title: "Giriş Yap | Profiqo" };

export default function SignInPage() {
  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-[#060E1A]">
      {/* Background gradient effects */}
      <div className="pointer-events-none absolute -left-40 -top-40 h-[500px] w-[500px] rounded-full bg-primary/10 blur-[120px]" />
      <div className="pointer-events-none absolute -bottom-32 -right-32 h-[400px] w-[400px] rounded-full bg-accent/8 blur-[100px]" />

      <div className="relative z-10 w-full max-w-md px-4">
        {/* Logo & Brand */}
        <div className="mb-8 text-center">
          <img
            src="/images/logo/profiqo-icon.png"
            alt="Profiqo"
            className="mx-auto mb-4 h-16 w-16"
          />
          <h1 className="text-2xl font-bold text-white">
            Profiqo&apos;ya Hoş Geldiniz
          </h1>
          <p className="mt-2 text-sm text-[#94A3B8]">
            Kontrol panelinize erişmek için giriş yapın.
          </p>
        </div>

        {/* Card */}
        <div className="rounded-2xl border border-[#1E3048] bg-[#0C1829] p-8 shadow-xl sm:p-10">
          <Signin />
        </div>

        {/* Footer */}
        <p className="mt-6 text-center text-xs text-[#64748B]">
          &copy; {new Date().getFullYear()} Profiqo. Smarter Customer Profits.
        </p>
      </div>
    </div>
  );
}