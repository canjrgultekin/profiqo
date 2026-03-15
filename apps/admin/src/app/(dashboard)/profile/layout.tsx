// Path: apps/admin/src/app/(dashboard)/profile/layout.tsx
import type { PropsWithChildren } from "react";
import { Metadata } from "next";

export const metadata: Metadata = {
  title: "Profil",
};

export default function Layout({ children }: PropsWithChildren) {
  return children;
}