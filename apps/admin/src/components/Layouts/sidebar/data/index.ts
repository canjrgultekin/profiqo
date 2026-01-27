import * as Icons from "../icons";

export const NAV_DATA = [
  {
    label: "MAIN MENU",
    items: [
      {
        title: "Dashboard",
        icon: Icons.HomeIcon,
        items: [{ title: "eCommerce", url: "/" }],
      },
      {
        title: "Customers",
        url: "/customers",
        icon: Icons.User,
        items: [],
      },
      {
        title: "Orders",
        url: "/orders",
        icon: Icons.Table,
        items: [],
      },
      {
        title: "Integrations",
        icon: Icons.Alphabet,
        items: [
          { title: "Overview", url: "/integrations" },
          { title: "Ikas", url: "/integrations/ikas" },
        ],
      },
    ],
  },
  {
    label: "OTHERS",
    items: [
      {
        title: "Authentication",
        icon: Icons.Authentication,
        items: [
          { title: "Sign In", url: "/auth/sign-in" },
          { title: "Sign Up", url: "/auth/sign-up" },
        ],
      },
    ],
  },
];
