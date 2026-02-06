import Signin from "@/components/Auth/Signin";
import type { Metadata } from "next";

export const metadata: Metadata = { title: "Sign in | Profiqo" };

export default function SignIn() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-2 dark:bg-[#020D1A]">
      <div className="w-full max-w-md rounded-[10px] bg-white p-8 shadow-1 dark:bg-gray-dark dark:shadow-card sm:p-12">
        <div className="mb-8 text-center">
          <img
            src="/images/logo/profiqo-icon.png"
            alt="Profiqo"
            className="mx-auto mb-4 h-14 w-14"
          />
          <h1 className="text-2xl font-bold text-dark dark:text-white">
            Profiqo&apos;ya Giriş Yap
          </h1>
          <p className="mt-2 text-sm text-body-color dark:text-dark-6">
            Devam etmek için giriş yapın.
          </p>
        </div>

        <Signin />
      </div>
    </div>
  );
}
