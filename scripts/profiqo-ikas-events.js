/**
 * Profiqo — ikas Storefront Events Tracker v2.0
 * ================================================
 * 4 event tipi: ADD_TO_CART, REMOVE_FROM_CART, COMPLETE_CHECKOUT, ADD_TO_WISHLIST
 * Müşteri bazlı tracking, firma bilgisi, dinamik backend URL.
 *
 * Kullanım (ikas Storefront Events → Script Barındırma):
 *   Script URL: https://cdn.profiqo.com/ikas/v2/profiqo-ikas-events.min.js?apiKey=pfq_pub_XXXXX
 *
 * Parametreler (?query string ile):
 *   apiKey    — (zorunlu) Profiqo publicApiKey (pfq_pub_XXXXX)
 *   endpoint  — (opsiyonel) Custom backend URL (default: https://api.profiqo.com)
 *
 * Debug:
 *   window.__profiqo.getCustomer()  — Mevcut müşteri bilgisi
 *   window.__profiqo.getTenant()    — Mevcut firma bilgisi
 *   window.__profiqo.track(type, data) — Manuel event tetikle
 *   window.__profiqo.flush()        — Queue'yu hemen gönder
 *
 * @version 2.0.0
 * @author  Profiqo
 */
(function () {
  "use strict";

  // ──────────────────────────────────────────────────
  // 1. CONFIGURATION
  // ──────────────────────────────────────────────────
  var CONFIG = {
    DEFAULT_API_BASE: "https://api.profiqo.com",
    EVENTS_ENDPOINT: "/api/v1/events/storefront",
    CONFIG_ENDPOINT: "/api/v1/events/storefront/config",
    DEVICE_ID_COOKIE: "_pfq_did",
    SESSION_ID_KEY: "_pfq_sid",
    CUSTOMER_KEY: "_pfq_cust",
    TENANT_KEY: "_pfq_tenant",
    COOKIE_MAX_AGE_DAYS: 730,
    SESSION_TIMEOUT_MS: 30 * 60 * 1000,
    BATCH_INTERVAL_MS: 2000,
    MAX_QUEUE_SIZE: 20,
    MAX_RETRIES: 2,
    RETRY_DELAY_MS: 1000,
    SUBSCRIBE_ID: "profiqo_storefront_tracker_v2",
    POLL_INTERVAL_MS: 200,
    MAX_POLLS: 50,
    SUPPORTED_EVENTS: {
      "ADDTOCART": "ADD_TO_CART",
      "REMOVEFROMCART": "REMOVE_FROM_CART",
      "COMPLETECHECKOUT": "COMPLETE_CHECKOUT",
      "ADDTOWISHLIST": "ADD_TO_WISHLIST"
    },
    // ikas event type'ları numerik — sendEvent v3 formatı
    // Bu mapping ikas'ın gerçek payload'ından reverse-engineer edildi
    // t:4 = addToCart (doğrulandı), t:16 = addToWishlist (doğrulandı)
    // Diğerleri test ile doğrulanacak, birden fazla olası değer tutuyoruz
    IKAS_EVENT_TYPES: {
      1: "PAGE_VIEW",
      2: "PRODUCT_VIEW",
      3: "REMOVE_FROM_CART",
      4: "ADD_TO_CART",
      5: "BEGIN_CHECKOUT",
      6: "COMPLETE_CHECKOUT",
      7: "SEARCH",
      8: "ADD_TO_WISHLIST",
      // ikas bazı event'lerde daha yüksek numeric ID'ler kullanıyor
      12: "COMPLETE_CHECKOUT",
      16: "ADD_TO_WISHLIST"
    }
  };

  // ──────────────────────────────────────────────────
  // 2. SCRIPT PARAMS — publicApiKey ve endpointUrl al
  // ──────────────────────────────────────────────────
  var scriptEl = document.currentScript;
  var apiKey = null;
  var endpointBase = CONFIG.DEFAULT_API_BASE;

  if (scriptEl && scriptEl.src) {
    try {
      var params = new URLSearchParams(scriptEl.src.split("?")[1] || "");
      apiKey = params.get("apiKey") || params.get("publicApiKey") || params.get("key");
      var customEndpoint = params.get("endpoint") || params.get("url");
      if (customEndpoint) endpointBase = customEndpoint.replace(/\/+$/, "");
    } catch (e) {
      // URLSearchParams desteklenmiyorsa regex ile parse et
      var match = scriptEl.src.match(/[?&]apiKey=([^&]+)/);
      if (match) apiKey = decodeURIComponent(match[1]);
    }
  }

  if (!apiKey) {
    console.warn("[Profiqo] apiKey bulunamadı. Script src'ye ?apiKey=YOUR_KEY ekleyin.");
    return;
  }

  // ──────────────────────────────────────────────────
  // 3. UTILITY FUNCTIONS
  // ──────────────────────────────────────────────────

  /** UUID v4 üret — crypto.randomUUID varsa onu kullan, yoksa fallback */
  function generateId() {
    if (typeof crypto !== "undefined" && crypto.randomUUID) {
      return crypto.randomUUID();
    }
    return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
      var r = (Math.random() * 16) | 0;
      var v = c === "x" ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }

  /** Cookie oku */
  function getCookie(name) {
    var match = document.cookie.match(new RegExp("(^| )" + name + "=([^;]+)"));
    return match ? decodeURIComponent(match[2]) : null;
  }

  /** Cookie yaz (Secure + SameSite=Lax) */
  function setCookie(name, value, days) {
    var expires = "";
    if (days) {
      var d = new Date();
      d.setTime(d.getTime() + days * 24 * 60 * 60 * 1000);
      expires = "; expires=" + d.toUTCString();
    }
    document.cookie =
      name + "=" + encodeURIComponent(value) + expires +
      "; path=/; SameSite=Lax; Secure";
  }

  /** Device ID — 2 yıl geçerli cookie-based UUID */
  function getOrCreateDeviceId() {
    var did = getCookie(CONFIG.DEVICE_ID_COOKIE);
    if (!did) {
      did = generateId();
      setCookie(CONFIG.DEVICE_ID_COOKIE, did, CONFIG.COOKIE_MAX_AGE_DAYS);
    }
    return did;
  }

  /** Session ID — 30dk timeout, sessionStorage */
  function getOrCreateSessionId() {
    var now = Date.now();
    var raw = null;
    try { raw = sessionStorage.getItem(CONFIG.SESSION_ID_KEY); } catch (e) {}
    if (raw) {
      try {
        var parsed = JSON.parse(raw);
        if (now - parsed.ts < CONFIG.SESSION_TIMEOUT_MS) {
          parsed.ts = now;
          try { sessionStorage.setItem(CONFIG.SESSION_ID_KEY, JSON.stringify(parsed)); } catch (e) {}
          return parsed.id;
        }
      } catch (e) {}
    }
    var sid = generateId();
    try { sessionStorage.setItem(CONFIG.SESSION_ID_KEY, JSON.stringify({ id: sid, ts: now })); } catch (e) {}
    return sid;
  }

  /** Mevcut sayfa bilgisi */
  function getPageContext() {
    return {
      url: window.location.href,
      path: window.location.pathname,
      referrer: document.referrer || null,
      title: document.title || null,
      userAgent: navigator.userAgent,
      language: navigator.language || null,
      screenWidth: window.screen ? window.screen.width : null,
      screenHeight: window.screen ? window.screen.height : null
    };
  }

  // ──────────────────────────────────────────────────
  // 4. CUSTOMER CONTEXT — Logged-in müşteri bilgisi
  // ──────────────────────────────────────────────────

  var customerContext = null;
  var ikasRawPayload = null; // Son gelen ikas raw payload'ı (ud bilgisi için)

  /**
   * ikas storefront'undaki müşteri bilgisini çıkar.
   * Öncelik sırası:
   *   1) ikas raw payload'daki ud (user data) — en güvenilir kaynak
   *   2) window.__ikas__.customer
   *   3) window.__NEXT_DATA__.props
   *   4) ikas JWT cookie
   *   5) Profiqo sessionStorage cache
   */
  function extractCustomerContext() {
    // Yol 1: ikas raw event payload'daki ud (user data)
    // ikas sendEvent v3: { ud: { id, em, ph, fn, ln } }
    if (ikasRawPayload && ikasRawPayload.ud) {
      var ud = ikasRawPayload.ud;
      if (ud.id || ud.em) {
        return {
          id: ud.id || null,
          email: ud.em || null,
          firstName: ud.fn || null,
          lastName: ud.ln || null,
          phone: ud.ph || null,
          isGuest: false
        };
      }
    }

    // Yol 2: ikas window global'den
    if (window.__ikas__ && window.__ikas__.customer) {
      var c = window.__ikas__.customer;
      return {
        id: c.id || null,
        email: c.email || null,
        firstName: c.firstName || null,
        lastName: c.lastName || null,
        phone: c.phone || null,
        isGuest: false
      };
    }

    // Yol 3: Next.js page props'dan
    if (window.__NEXT_DATA__ && window.__NEXT_DATA__.props) {
      var pageProps = window.__NEXT_DATA__.props.pageProps || window.__NEXT_DATA__.props;
      var cust = pageProps.customer || pageProps.currentCustomer || pageProps.user;
      if (cust && cust.id) {
        return {
          id: cust.id,
          email: cust.email || null,
          firstName: cust.firstName || null,
          lastName: cust.lastName || null,
          phone: cust.phone || null,
          isGuest: false
        };
      }
    }

    // Yol 4: ikas'ın set ettiği JWT cookie'den
    var ikasToken = getCookie("ikas_ct") || getCookie("ikas_customer_token");
    if (ikasToken) {
      try {
        var parts = ikasToken.split(".");
        if (parts.length === 3) {
          var payload = JSON.parse(atob(parts[1].replace(/-/g, "+").replace(/_/g, "/")));
          if (payload.sub || payload.customerId) {
            return {
              id: payload.sub || payload.customerId || null,
              email: payload.email || null,
              firstName: payload.firstName || null,
              lastName: payload.lastName || null,
              phone: null,
              isGuest: false
            };
          }
        }
      } catch (e) {}
      return { id: null, email: null, firstName: null, lastName: null, phone: null, isGuest: false };
    }

    // Yol 5: Profiqo'nun kendi customer cache'i
    try {
      var cached = sessionStorage.getItem(CONFIG.CUSTOMER_KEY);
      if (cached) return JSON.parse(cached);
    } catch (e) {}

    return { id: null, email: null, firstName: null, lastName: null, phone: null, isGuest: true };
  }

  /**
   * Müşteri context'ini güncelle — event data'sı veya ikas ud field'ından.
   * ikas v3 formatında ud doğrudan raw payload'da geliyor, handleIkasEvent'te işleniyor.
   * Bu fonksiyon backward compat ve manuel track için kaldı.
   */
  function updateCustomerFromEvent(eventData) {
    if (!eventData) return;
    // ikas v3 ud formatı
    if (eventData.ud && (eventData.ud.id || eventData.ud.em)) {
      customerContext = {
        id: eventData.ud.id || null,
        email: eventData.ud.em || null,
        firstName: eventData.ud.fn || null,
        lastName: eventData.ud.ln || null,
        phone: eventData.ud.ph || null,
        isGuest: false
      };
      try { sessionStorage.setItem(CONFIG.CUSTOMER_KEY, JSON.stringify(customerContext)); } catch (e) {}
      return;
    }
    // Eski format compat
    var customer = eventData.customer || eventData.user;
    if (customer && (customer.id || customer.email)) {
      customerContext = {
        id: customer.id || null,
        email: customer.email || null,
        firstName: customer.firstName || null,
        lastName: customer.lastName || null,
        phone: customer.phone || null,
        isGuest: false
      };
      try { sessionStorage.setItem(CONFIG.CUSTOMER_KEY, JSON.stringify(customerContext)); } catch (e) {}
    }
  }

  // İlk yükleme sırasında customer context'i çıkar
  customerContext = extractCustomerContext();

  // ──────────────────────────────────────────────────
  // 5. FIRMA CONTEXT — apiKey ile tenant bilgisi
  // ──────────────────────────────────────────────────

  var tenantContext = null;

  /**
   * Backend'den tenant/firma bilgisi çek (publicApiKey ile).
   * Script yüklenirken 1 kez çağrılır, sessionStorage'a cache'lenir.
   * Config response: { tenantId, tenantName, storeDomain, enabledEvents[] }
   */
  function fetchTenantConfig() {
    var url = endpointBase + CONFIG.CONFIG_ENDPOINT + "?apiKey=" + encodeURIComponent(apiKey);
    if (typeof fetch === "function") {
      fetch(url, {
        method: "GET",
        mode: "cors",
        credentials: "omit"
      })
        .then(function (res) { return res.json(); })
        .then(function (data) {
          if (data && data.tenantId) {
            tenantContext = {
              tenantId: data.tenantId,
              tenantName: data.tenantName || null,
              storeDomain: data.storeDomain || window.location.hostname,
              enabledEvents: data.enabledEvents || Object.values(CONFIG.SUPPORTED_EVENTS)
            };
            try { sessionStorage.setItem(CONFIG.TENANT_KEY, JSON.stringify(tenantContext)); } catch (e) {}
          }
        })
        .catch(function () {
          // Sessizce devam et — config çekilemezse default ile çalış
          // Tüm event'ler enabled kabul edilir
        });
    }
  }

  // Cache'den tenant context yükle (sayfa geçişlerinde hız için)
  try {
    var cachedTenant = sessionStorage.getItem(CONFIG.TENANT_KEY);
    if (cachedTenant) tenantContext = JSON.parse(cachedTenant);
  } catch (e) {}

  // Background'da taze config çek (cache olsa bile güncellik için)
  fetchTenantConfig();

  // ──────────────────────────────────────────────────
  // 6. EVENT QUEUE & BATCH SENDER
  // ──────────────────────────────────────────────────

  var eventQueue = [];
  var batchTimer = null;
  var deviceId = getOrCreateDeviceId();

  /**
   * Biriken event'leri toplu olarak backend'e gönder.
   * Her batch'e güncel customer + tenant bilgisi eklenir.
   */
  function flushQueue() {
    if (eventQueue.length === 0) return;
    var batch = eventQueue.splice(0, CONFIG.MAX_QUEUE_SIZE);

    var currentCustomer = customerContext || extractCustomerContext();

    var payload = JSON.stringify({
      apiKey: apiKey,
      deviceId: deviceId,
      sessionId: getOrCreateSessionId(),
      sentAt: new Date().toISOString(),
      customer: currentCustomer,
      tenant: tenantContext ? {
        tenantId: tenantContext.tenantId,
        tenantName: tenantContext.tenantName,
        storeDomain: tenantContext.storeDomain
      } : {
        storeDomain: window.location.hostname
      },
      events: batch
    });

    sendPayload(payload, 0);
  }

  /**
   * Payload'u backend'e gönder.
   * Öncelik sırası: Beacon API → fetch (keepalive) → XHR fallback
   * Hata durumunda exponential backoff ile retry (max 2 deneme)
   */
  function sendPayload(payload, attempt) {
    var url = endpointBase + CONFIG.EVENTS_ENDPOINT;

    // 1. Beacon API (en güvenilir — sayfa kapansa bile gönderir)
    // text/plain kullanarak CORS preflight bypass — backend her iki Content-Type'ı da kabul eder
    if (navigator.sendBeacon && attempt === 0) {
      var blob = new Blob([payload], { type: "text/plain" });
      var sent = navigator.sendBeacon(url, blob);
      if (sent) return;
    }

    // 2. Fetch API (keepalive ile, credentials yok — cross-origin uyumluluğu için)
    if (typeof fetch === "function") {
      fetch(url, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: payload,
        keepalive: true,
        mode: "cors",
        credentials: "omit"
      }).catch(function () {
        if (attempt < CONFIG.MAX_RETRIES) {
          setTimeout(function () {
            sendPayload(payload, attempt + 1);
          }, CONFIG.RETRY_DELAY_MS * Math.pow(2, attempt));
        }
      });
      return;
    }

    // 3. XHR fallback (eski tarayıcılar için)
    try {
      var xhr = new XMLHttpRequest();
      xhr.open("POST", url, true);
      xhr.setRequestHeader("Content-Type", "application/json");
      xhr.send(payload);
    } catch (e) {}
  }

  /** Event'i queue'ya ekle — queue dolunca veya 2sn sonra otomatik flush */
  function enqueueEvent(event) {
    eventQueue.push(event);
    if (eventQueue.length >= CONFIG.MAX_QUEUE_SIZE) {
      clearTimeout(batchTimer);
      flushQueue();
      return;
    }
    if (!batchTimer) {
      batchTimer = setTimeout(function () {
        batchTimer = null;
        flushQueue();
      }, CONFIG.BATCH_INTERVAL_MS);
    }
  }

  // Sayfa kapatılırken veya gizlenirken kalan event'leri gönder
  window.addEventListener("beforeunload", function () { flushQueue(); });
  document.addEventListener("visibilitychange", function () {
    if (document.visibilityState === "hidden") flushQueue();
  });

  // ──────────────────────────────────────────────────
  // 7. EVENT DATA EXTRACTORS — ikas sendEvent v3 formatı
  // ──────────────────────────────────────────────────
  //
  // ikas raw event yapısı (v3):
  //   e[].t   → event type (numeric: 4=addToCart, 3=removeFromCart, 6=checkout, 8=wishlist)
  //   e[].d   → event data wrapper
  //   e[].d.d → actual data (nested)
  //     .d.d.crt    → cart object { id, c(currency), pr(price), il[](items) }
  //     .d.d.crtItm → the specific cart item { pid, vid, c(count), p(price) }
  //     .d.d.ord    → order object (checkout)
  //     .d.d.prd    → product object (product view / wishlist)
  //   e[].d.u  → page URL
  //   e[].d.r  → referrer
  //   ud       → user data { id, em(email), ph(phone), fn(firstName), ln(lastName) }
  //

  /**
   * ikas cart item'ı extract et
   * ikas formatı: { pid: "uuid", vid: "uuid", c: 3, p: 4500 }
   * pid = product ID, vid = variant ID, c = count/quantity, p = price (kuruş/cent değil, tam fiyat)
   */
  function extractIkasCartItem(crtItm) {
    if (!crtItm) return {};
    return {
      productId: crtItm.pid || null,
      variantId: crtItm.vid || null,
      quantity: crtItm.c || crtItm.q || 1,
      price: crtItm.p || null
    };
  }

  /**
   * ikas cart object'i extract et
   * ikas formatı: { id: "uuid", c: "TRY", pr: 41533, il: [{pid,vid,c,p},...] }
   */
  function extractIkasCart(crt) {
    if (!crt) return {};
    var result = {
      cartId: crt.id || null,
      currency: crt.c || "TRY",
      cartTotalPrice: crt.pr || null,
      cartItemCount: crt.il ? crt.il.length : null
    };

    if (crt.il && Array.isArray(crt.il)) {
      result.items = crt.il.map(function (item) {
        return extractIkasCartItem(item);
      });
    }

    return result;
  }

  /**
   * ADD_TO_CART / REMOVE_FROM_CART data extractor
   * ikas v3: e[].d.d = { crt: {cart}, crtItm: {the added/removed item} }
   */
  function extractCartEventData(innerData) {
    if (!innerData) return {};
    var result = {};

    // Sepete eklenen/çıkarılan ürün
    if (innerData.crtItm) {
      var item = extractIkasCartItem(innerData.crtItm);
      result.productId = item.productId;
      result.variantId = item.variantId;
      result.quantity = item.quantity;
      result.price = item.price;
    }

    // Sepet bilgisi
    if (innerData.crt) {
      var cart = extractIkasCart(innerData.crt);
      result.cartId = cart.cartId;
      result.currency = cart.currency;
      result.cartTotalPrice = cart.cartTotalPrice;
      result.cartItemCount = cart.cartItemCount;
      result.cartItems = cart.items;
    }

    return result;
  }

  /**
   * COMPLETE_CHECKOUT data extractor
   * ikas v3: e[].d.d = { ord: {order} } veya doğrudan order bilgileri
   */
  function extractCheckoutEventData(innerData) {
    if (!innerData) return {};
    var result = {};
    var ord = innerData.ord || innerData;

    result.orderId = ord.id || ord.oid || null;
    result.orderNumber = ord.on || ord.orderNumber || null;
    result.totalPrice = ord.pr || ord.tp || null;
    result.currency = ord.c || "TRY";

    // Sipariş kalemleri
    var items = ord.il || ord.items;
    if (items && Array.isArray(items)) {
      result.itemCount = items.length;
      result.items = items.map(function (item) {
        return extractIkasCartItem(item);
      });
    }

    return result;
  }

  /**
   * ADD_TO_WISHLIST data extractor
   * ikas v3: e[].d.d = { pr: { id: "product-uuid" } }
   */
  function extractWishlistEventData(innerData) {
    if (!innerData) return {};
    var prd = innerData.pr || innerData.prd || innerData;
    return {
      productId: prd.id || prd.pid || null,
      variantId: prd.vid || null
    };
  }

  // ──────────────────────────────────────────────────
  // 8. ikas v3 EVENT PARSER & HANDLER
  // ──────────────────────────────────────────────────

  /**
   * ikas numeric event type'ı Profiqo string type'a çevir.
   * Desteklenen: 3=REMOVE_FROM_CART, 4=ADD_TO_CART, 6=COMPLETE_CHECKOUT, 8=ADD_TO_WISHLIST
   */
  function resolveIkasEventType(numericType) {
    var mapped = CONFIG.IKAS_EVENT_TYPES[numericType];
    if (!mapped) return null;
    // Sadece desteklediğimiz 4 event tipini geçir
    var supported = ["ADD_TO_CART", "REMOVE_FROM_CART", "COMPLETE_CHECKOUT", "ADD_TO_WISHLIST"];
    return supported.indexOf(mapped) !== -1 ? mapped : null;
  }

  /**
   * String event tipini normalize et (window.__profiqo.track() ile kullanım için)
   */
  function normalizeEventType(type) {
    if (!type) return null;
    if (typeof type === "number") return resolveIkasEventType(type);
    var normalized = type.toUpperCase().replace(/[_\-\s]/g, "");
    return CONFIG.SUPPORTED_EVENTS[normalized] || null;
  }

  /** Tenant config'deki enabledEvents listesini kontrol et */
  function isEventEnabled(eventType) {
    if (!tenantContext || !tenantContext.enabledEvents) return true;
    return tenantContext.enabledEvents.indexOf(eventType) !== -1;
  }

  /** Event tipine göre uygun data extractor'ı seç */
  function extractEventData(eventType, innerData) {
    switch (eventType) {
      case "ADD_TO_CART":
      case "REMOVE_FROM_CART":
        return extractCartEventData(innerData);
      case "COMPLETE_CHECKOUT":
        return extractCheckoutEventData(innerData);
      case "ADD_TO_WISHLIST":
        return extractWishlistEventData(innerData);
      default:
        return innerData || {};
    }
  }

  /**
   * ikas v3 raw event handler.
   * ikas IkasEvents.subscribe callback'i şu yapıda payload gönderir:
   *   { e: [{id,t,ts,d:{u,r,d:{crt,crtItm,...}}}], ud: {id,em,ph,fn,ln}, scid, sid, ... }
   *
   * UYARI: Bu callback "type" field'lı obje DEĞİL, ikas'ın kendi minified formatı.
   */
  function handleIkasEvent(rawPayload) {
    try {
      if (!rawPayload) return;

      // ikas v3 formatı: { e: [...], ud: {...}, ... }
      // VEYA window.__profiqo.track() ile gelen: { type: "ADD_TO_CART", data: {...} }
      var isIkasV3 = rawPayload.e && Array.isArray(rawPayload.e);
      var isManualTrack = rawPayload.type && !rawPayload.e;

      if (isManualTrack) {
        // Manuel track — eski format
        var manualType = normalizeEventType(rawPayload.type);
        if (!manualType || !isEventEnabled(manualType)) return;
        enqueueEvent({
          eventId: generateId(),
          type: manualType,
          occurredAt: new Date().toISOString(),
          data: rawPayload.data || {},
          page: getPageContext(),
          customer: customerContext || extractCustomerContext()
        });
        return;
      }

      if (!isIkasV3) return;

      // ikas raw payload'ı sakla (ud bilgisi için)
      ikasRawPayload = rawPayload;

      // User data'dan müşteri bilgisi güncelle
      if (rawPayload.ud && (rawPayload.ud.id || rawPayload.ud.em)) {
        customerContext = {
          id: rawPayload.ud.id || null,
          email: rawPayload.ud.em || null,
          firstName: rawPayload.ud.fn || null,
          lastName: rawPayload.ud.ln || null,
          phone: rawPayload.ud.ph || null,
          isGuest: false
        };
        try { sessionStorage.setItem(CONFIG.CUSTOMER_KEY, JSON.stringify(customerContext)); } catch (ex) {}
      }

      var currentCustomer = customerContext || extractCustomerContext();

      // Her event'i işle
      rawPayload.e.forEach(function (ikasEvent) {
        var eventType = resolveIkasEventType(ikasEvent.t);
        if (!eventType) return;
        if (!isEventEnabled(eventType)) return;

        // ikas nested data: e[].d.d → actual event data
        var innerData = (ikasEvent.d && ikasEvent.d.d) ? ikasEvent.d.d : {};
        var eventData = extractEventData(eventType, innerData);

        // Sayfa bilgisi — ikas event'ten veya mevcut page'den
        var page = getPageContext();
        if (ikasEvent.d) {
          if (ikasEvent.d.u) page.url = ikasEvent.d.u;
          if (ikasEvent.d.r) page.referrer = ikasEvent.d.r;
        }

        enqueueEvent({
          eventId: ikasEvent.id || generateId(),
          type: eventType,
          occurredAt: ikasEvent.ts ? new Date(ikasEvent.ts).toISOString() : new Date().toISOString(),
          data: eventData,
          page: page,
          customer: currentCustomer
        });
      });

    } catch (e) {
      // Sessizce devam et — storefront deneyimini asla bozma
    }
  }

  // ──────────────────────────────────────────────────
  // 9. INITIALIZATION — ikas IkasEvents'e subscribe ol
  // ──────────────────────────────────────────────────

  /**
   * ikas'ın window.IkasEvents objesine subscribe ol.
   * ikas theme'ı async yüklendiği için IkasEvents hazır olmayabilir,
   * bu durumda 200ms aralıklarla 10sn boyunca poll yapıyoruz.
   */
  function subscribeToIkasEvents() {
    if (typeof window.IkasEvents !== "undefined" && typeof window.IkasEvents.subscribe === "function") {
      window.IkasEvents.subscribe({
        id: CONFIG.SUBSCRIBE_ID,
        callback: handleIkasEvent
      });
      return true;
    }
    return false;
  }

  // İlk deneme
  if (!subscribeToIkasEvents()) {
    var pollCount = 0;
    var pollInterval = setInterval(function () {
      pollCount++;
      if (subscribeToIkasEvents() || pollCount >= CONFIG.MAX_POLLS) {
        clearInterval(pollInterval);
        if (pollCount >= CONFIG.MAX_POLLS) {
          console.warn("[Profiqo] window.IkasEvents " + (CONFIG.MAX_POLLS * CONFIG.POLL_INTERVAL_MS) + "ms içinde bulunamadı.");
        }
      }
    }, CONFIG.POLL_INTERVAL_MS);
  }

  // ──────────────────────────────────────────────────
  // 10. GLOBAL API — Debug ve manuel event tetikleme
  // ──────────────────────────────────────────────────

  window.__profiqo = {
    version: "2.0.0",
    apiKey: apiKey,
    deviceId: deviceId,
    /** Mevcut müşteri bilgisini döner */
    getCustomer: function () { return customerContext || extractCustomerContext(); },
    /** Mevcut firma/tenant bilgisini döner */
    getTenant: function () { return tenantContext; },
    /** Queue'daki tüm event'leri hemen gönder */
    flush: flushQueue,
    /** Manuel event tetikle (test için) — örnek: window.__profiqo.track('ADD_TO_CART', { product: { id: '123', name: 'Test' }, quantity: 1, price: 99.90 }) */
    track: function (type, data) {
      handleIkasEvent({ type: type, data: data });
    }
  };

})();
