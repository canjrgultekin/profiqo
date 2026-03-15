// Path: apps/admin/src/components/Layouts/sidebar/data/index.ts
import * as Icons from "../icons";

export const NAV_DATA = [
  {
    label: "Genel",
    items: [
      {
        title: "Kontrol Paneli",
        icon: Icons.HomeIcon,
        items: [{ title: "Dashboard", url: "/" }],
      },
    ],
  },
  {
    label: "İş Yönetimi",
    items: [
      {
        title: "Müşteriler",
        icon: Icons.UsersIcon,
        items: [
          { title: "Müşteri Listesi", url: "/customers" },
          { title: "Kimlik Analizi", url: "/customers/dedupe" },
        ],
      },
      {
        title: "Siparişler",
        url: "/orders",
        icon: Icons.OrdersIcon,
        items: [],
      },
      {
        title: "Ürünler",
        url: "/products",
        icon: Icons.ProductsIcon,
        items: [],
      },
      {
        title: "Raporlar",
        icon: Icons.ReportsIcon,
        items: [
          { title: "Terk Edilen Sepetler", url: "/reports/abandoned-carts" },
        ],
      },
    ],
  },
  {
    label: "Entegrasyonlar",
    items: [
      {
        title: "Bağlantılar",
        icon: Icons.IntegrationsIcon,
        items: [
          { title: "Genel Durum", url: "/integrations" },
          { title: "Ikas", url: "/integrations/ikas" },
          { title: "Trendyol", url: "/integrations/trendyol" },
          { title: "Hepsiburada", url: "/integrations/hepsiburada" },
          { title: "Shopify", url: "/integrations/shopify" },
        ],
      },
      {
        title: "WhatsApp",
        icon: Icons.MessageIcon,
        items: [
          { title: "Bağlantı Ayarları", url: "/integrations/whatsapp" },
          { title: "Şablonlar", url: "/integrations/whatsapp/templates" },
          { title: "Kurallar", url: "/integrations/whatsapp/rules" },
          { title: "Zamanlanmış İşler", url: "/integrations/whatsapp/jobs" },
          { title: "Gönderim Geçmişi", url: "/integrations/whatsapp/dispatch" },
        ],
      },
    ],
  },
  {
    label: "Sistem",
    items: [
      {
        title: "Otomasyon",
        icon: Icons.AutomationIcon,
        items: [
          { title: "Senkron Kuralları", url: "/automation/sync-rules" },
          { title: "Senkron Geçmişi", url: "/automation/sync-runs" },
        ],
      },
      {
        title: "Ayarlar",
        icon: Icons.SettingsIcon,
        items: [
          { title: "Kullanıcı Yönetimi", url: "/settings/users" },
        ],
      },
    ],
  },
];