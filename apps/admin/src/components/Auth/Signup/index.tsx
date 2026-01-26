import Link from "next/link";
import React from "react";
import SignupWithPassword from "@/components/Auth/SignupWithPassword";

const Signup = () => {
  return (
    <div className="rounded-[10px] bg-white shadow-1 dark:bg-gray-dark dark:shadow-card">
      <div className="flex flex-wrap items-center">
        <div className="w-full xl:w-1/2">
          <div className="w-full p-4 sm:p-12.5 xl:p-17.5">
            <h2 className="mb-3 text-2xl font-bold text-dark dark:text-white sm:text-title-xl2">
              ProfiQo Tenant Oluştur
            </h2>
            <p className="mb-7.5 text-base font-medium">
              Şirketini oluştur, owner kullanıcıyı yarat, içeri gir.
            </p>

            <SignupWithPassword />

            <div className="mt-6 text-center">
              <p>
                Zaten hesabın var mı?{" "}
                <Link href="/auth/sign-in" className="text-primary">
                  Giriş Yap
                </Link>
              </p>
            </div>
          </div>
        </div>

        <div className="hidden w-full p-7.5 xl:block xl:w-1/2">
          <div className="rounded-[10px] bg-primary p-12.5 text-white">
            <h3 className="mb-4 text-2xl font-bold">Retention, otomasyon, rapor</h3>
            <p className="text-white/90">
              Bu ekran register endpoint’ine bağlanır. Local’de backend: http://localhost:5164
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Signup;
