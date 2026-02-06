import { Header } from "@/components/Layouts/header";
import { Sidebar } from "@/components/Layouts/sidebar";
import type { PropsWithChildren } from "react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";

const ACCESS_COOKIE = "profiqo_access_token";

export default async function DashboardLayout({ children }: PropsWithChildren) {
  const c = await cookies();
  const token = c.get(ACCESS_COOKIE)?.value;

  if (!token) {
    redirect("/auth/sign-in");
  }

  return (
    <div className="flex min-h-screen">
      <Sidebar inset />
      <div className="w-full bg-gray-2 dark:bg-[#020D1A]">
        <Header />
        <main className="mx-auto max-w-(--breakpoint-2xl) p-4 md:p-6 2xl:p-10">
          {children}
        </main>
      </div>
    </div>
  );
}
