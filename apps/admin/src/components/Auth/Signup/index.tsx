import Link from "next/link";
import React from "react";
import SignupWithPassword from "@/components/Auth/SignupWithPassword";

const Signup = () => {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-2 dark:bg-[#020D1A]">
      <div className="w-full max-w-md rounded-[10px] bg-white p-8 shadow-1 dark:bg-gray-dark dark:shadow-card sm:p-12">
        <div className="mb-8 text-center">
          <img
            src="/images/logo/profiqo-icon.png"
            alt="Profiqo"
            className="mx-auto mb-4 h-14 w-14"
          />
          <h2 className="text-2xl font-bold text-dark dark:text-white">
            ProfiQo Tenant Oluştur
          </h2>
          <p className="mt-2 text-sm text-body-color dark:text-dark-6">
            Şirketini oluştur, owner kullanıcıyı yarat, içeri gir.
          </p>
        </div>

        <SignupWithPassword />

        <div className="mt-6 text-center">
          <p className="text-sm text-body-color dark:text-dark-6">
            Zaten hesabın var mı?{" "}
            <Link href="/auth/sign-in" className="text-primary hover:underline">
              Giriş Yap
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
};

export default Signup;
