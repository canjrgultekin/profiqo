// Path: apps/admin/src/components/Auth/Signin/index.tsx
import Link from "next/link";
import SigninWithPassword from "../SigninWithPassword";

export default function Signin() {
  return (
    <>
      <div>
        <SigninWithPassword />
      </div>

      <div className="mt-6 text-center">
        <p className="text-sm text-body-color dark:text-dark-6">
          Henüz hesabın yok mu?{" "}
          <Link href="/auth/sign-up" className="text-primary hover:underline">
            Kayıt Ol
          </Link>
        </p>
      </div>
    </>
  );
}