// Path: apps/admin/src/components/Layouts/sidebar/data/index.ts
import * as Icons from "../icons";

export const NAV_DATA = [
  {
    label: "MAIN MENU",
    items: [
      { title: "Dashboard", icon: Icons.HomeIcon, items: [{ title: "eCommerce", url: "/" }] },

      // ✅ Customers altına Identity Analyze eklendi
      {
        title: "Customers",
        url: "/customers",
        icon: Icons.User,
        items: [
          { title: "Customers", url: "/customers" },
          { title: "Identity Analyze", url: "/customers/dedupe" },
        ],
      },

      { title: "Orders", url: "/orders", icon: Icons.Table, items: [] },

      {
        title: "Reports",
        icon: Icons.Alphabet,
        items: [{ title: "Abandoned Carts", url: "/reports/abandoned-carts" }],
      },

      {
        title: "Integrations",
        icon: Icons.Alphabet,
        items: [
          { title: "Overview", url: "/integrations" },
          { title: "Ikas", url: "/integrations/ikas" },
          { title: "Trendyol", url: "/integrations/trendyol" },
        ],
      },
	  {
		  title: "Automation",
		  icon: Icons.Alphabet,
		  items: [
			{ title: "Sync Rules", url: "/automation/sync-rules" },
			{ title: "Sync Runs", url: "/automation/sync-runs" },
		  ],
	  },
      {
        title: "Settings",
        icon: Icons.Alphabet,
        items: [{ title: "Users", url: "/settings/users" }],
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
