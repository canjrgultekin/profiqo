/**
 * Profiqo — ikas Storefront Events Tracker v2.1
 * ================================================
 * Event tipleri: ADD_TO_CART, REMOVE_FROM_CART, COMPLETE_CHECKOUT, ADD_TO_WISHLIST
 * Müşteri bazlı tracking, firma bilgisi, dinamik backend URL.
 *
 * Kullanım (ikas Storefront Events → Script Barındırma):
 *   Script URL: https://cdn.profiqo.com/ikas/v2/profiqo-ikas-events.min.js?apiKey=pfq_pub_XXXXX
 *
 * Parametreler (?query string ile):
 *   apiKey    — (zorunlu) Profiqo publicApiKey (pfq_pub_XXXXX)
 *   endpoint  — (opsiyonel) Custom backend URL (default: http://localhost:5164)
 *
 * Debug:
 *   window.__profiqo.getCustomer()  — Mevcut müşteri bilgisi
 *   window.__profiqo.getTenant()    — Mevcut firma bilgisi
 *   window.__profiqo.track(type, data) — Manuel event tetikle
 *   window.__profiqo.flush()        — Queue'yu hemen gönder
 *
 * @version 2.1.0
 * @author  Profiqo
 */
(function () {
  "use strict";

  // ──────────────────────────────────────────────────
  // 1. CONFIGURATION
  // ──────────────────────────────────────────────────
  var CONFIG = {
    DEFAULT_API_BASE: "http://localhost:5164",
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
    // ikas Storefront Events callback'i iki format kullanır:
    //
    // FORMAT A — Storefront Events API (tam obje):
    //   callback({ type: "addToCart", data: { item, quantity, cart: { customer, shippingAddress, ... } } })
    //   type string: "addToCart", "removeFromCart", "completeCheckout", "addToWishlist"
    //
    // FORMAT B — ikas sendEvent v3 (minified):
    //   callback({ e: [{ id, t: 4, ts, d: { u, r, d: { crt, crtItm } } }], ud: { id, em, ph, fn, ln } })
    //   t numeric: 4=ADD_TO_CART, 3=REMOVE_FROM_CART, 6=COMPLETE_CHECKOUT, 8=ADD_TO_WISHLIST

    // String type mapping (FORMAT A — Storefront Events API)
    IKAS_STRING_EVENTS: {
      "addtocart": "ADD_TO_CART",
      "removefromcart": "REMOVE_FROM_CART",
      "completecheckout": "COMPLETE_CHECKOUT",
      "addtowishlist": "ADD_TO_WISHLIST"
    },
    // Numeric type mapping (FORMAT B — sendEvent v3)
    IKAS_EVENT_TYPES: {
      1: "PAGE_VIEW",
      2: "PRODUCT_VIEW",
      3: "REMOVE_FROM_CART",
      4: "ADD_TO_CART",
      5: "BEGIN_CHECKOUT",
      6: "COMPLETE_CHECKOUT",
      7: "SEARCH",
      8: "ADD_TO_WISHLIST",
      12: "COMPLETE_CHECKOUT",
      16: "ADD_TO_WISHLIST"
    }
  };

  // ──────────────────────────────────────────────────
  // 2. SCRIPT PARAMS
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

  function getCookie(name) {
    var match = document.cookie.match(new RegExp("(^| )" + name + "=([^;]+)"));
    return match ? decodeURIComponent(match[2]) : null;
  }

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

  function getOrCreateDeviceId() {
    var did = getCookie(CONFIG.DEVICE_ID_COOKIE);
    if (!did) {
      did = generateId();
      setCookie(CONFIG.DEVICE_ID_COOKIE, did, CONFIG.COOKIE_MAX_AGE_DAYS);
    }
    return did;
  }

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
  // 4. CUSTOMER CONTEXT
  // ──────────────────────────────────────────────────

  var customerContext = null;
  var ikasRawPayload = null;

  /**
   * ikas FORMAT A event data'sından müşteri bilgisi çıkar.
   * Kaynak önceliği:
   *   1) cart.customer / order.customer (email, firstName, lastName, id)
   *   2) cart.shippingAddress / order.shippingAddress (phone, firstName, lastName)
   *   3) cart.customerId (sadece id)
   */
  function extractCustomerFromEventData(eventData) {
    if (!eventData) return null;

    var customer = null;
    var phone = null;
    var addrFirstName = null;
    var addrLastName = null;

    // customer objesini bul
    if (eventData.cart && eventData.cart.customer) {
      customer = eventData.cart.customer;
    } else if (eventData.order && eventData.order.customer) {
      customer = eventData.order.customer;
    } else if (eventData.customer && (eventData.customer.id || eventData.customer.email)) {
      customer = eventData.customer;
    }

    // shippingAddress'ten telefon ve isim al
    var addr = null;
    if (eventData.cart && eventData.cart.shippingAddress) {
      addr = eventData.cart.shippingAddress;
    } else if (eventData.order && eventData.order.shippingAddress) {
      addr = eventData.order.shippingAddress;
    } else if (eventData.shippingAddress) {
      addr = eventData.shippingAddress;
    }
    if (addr) {
      phone = addr.phone || null;
      addrFirstName = addr.firstName || null;
      addrLastName = addr.lastName || null;
    }

    // customerId fallback (cart.customerId direkt field olarak)
    var fallbackId = null;
    if (eventData.cart && eventData.cart.customerId) {
      fallbackId = eventData.cart.customerId;
    }

    if (customer && (customer.id || customer.email)) {
      return {
        id: customer.id || fallbackId || null,
        email: customer.email || null,
        firstName: customer.firstName || addrFirstName || null,
        lastName: customer.lastName || addrLastName || null,
        phone: customer.phone || phone || null,
        isGuest: customer.isGuestCheckout === true
      };
    }

    // customer objesi yok ama customerId var
    if (fallbackId) {
      return {
        id: fallbackId,
        email: null,
        firstName: addrFirstName || null,
        lastName: addrLastName || null,
        phone: phone || null,
        isGuest: false
      };
    }

    // Müşteri yok ama shipping address'te telefon var
    if (phone) {
      return {
        id: null,
        email: null,
        firstName: addrFirstName || null,
        lastName: addrLastName || null,
        phone: phone,
        isGuest: true
      };
    }

    return null;
  }

  /**
   * Fallback müşteri context'i — window global'lardan, cookie'den veya cache'den.
   */
  function extractCustomerContext() {
    // ikas raw payload ud
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

    // window.__ikas__.customer
    if (window.__ikas__ && window.__ikas__.customer) {
      var c = window.__ikas__.customer;
      if (c.id || c.email) {
        return {
          id: c.id || null,
          email: c.email || null,
          firstName: c.firstName || null,
          lastName: c.lastName || null,
          phone: c.phone || null,
          isGuest: false
        };
      }
    }

    // __NEXT_DATA__.props
    if (window.__NEXT_DATA__ && window.__NEXT_DATA__.props) {
      var pageProps = window.__NEXT_DATA__.props.pageProps || window.__NEXT_DATA__.props;
      var cust = pageProps.customer || pageProps.currentCustomer || pageProps.user;
      if (cust && (cust.id || cust.email)) {
        return {
          id: cust.id || null,
          email: cust.email || null,
          firstName: cust.firstName || null,
          lastName: cust.lastName || null,
          phone: cust.phone || null,
          isGuest: false
        };
      }
    }

    // ikas JWT cookie
    var ikasToken = getCookie("ikas_ct") || getCookie("ikas_customer_token");
    if (ikasToken) {
      try {
        var parts = ikasToken.split(".");
        if (parts.length === 3) {
          var payload = JSON.parse(atob(parts[1].replace(/-/g, "+").replace(/_/g, "/")));
          if (payload.sub || payload.customerId || payload.email) {
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
    }

    // sessionStorage cache
    try {
      var cached = sessionStorage.getItem(CONFIG.CUSTOMER_KEY);
      if (cached) {
        var parsed = JSON.parse(cached);
        if (parsed && (parsed.id || parsed.email || parsed.phone)) return parsed;
      }
    } catch (e) {}

    return { id: null, email: null, firstName: null, lastName: null, phone: null, isGuest: true };
  }

  /**
   * Müşteri context'ini güncelle — sadece daha iyi bilgi varsa.
   * null/guest üzerine yazmaz, zenginleştirir.
   */
  function updateCustomerContext(newCust) {
    if (!newCust) return;
    if (!newCust.id && !newCust.email && !newCust.phone) return;

    var prev = customerContext || {};
    customerContext = {
      id: newCust.id || prev.id || null,
      email: newCust.email || prev.email || null,
      firstName: newCust.firstName || prev.firstName || null,
      lastName: newCust.lastName || prev.lastName || null,
      phone: newCust.phone || prev.phone || null,
      isGuest: newCust.id ? false : (prev.id ? false : true)
    };

    try { sessionStorage.setItem(CONFIG.CUSTOMER_KEY, JSON.stringify(customerContext)); } catch (e) {}
  }

  // İlk yükleme
  customerContext = extractCustomerContext();

  // ──────────────────────────────────────────────────
  // 5. FIRMA CONTEXT
  // ──────────────────────────────────────────────────

  var tenantContext = null;

  function fetchTenantConfig() {
    var url = endpointBase + CONFIG.CONFIG_ENDPOINT + "?apiKey=" + encodeURIComponent(apiKey);
    if (typeof fetch === "function") {
      fetch(url, { method: "GET", mode: "cors", credentials: "omit" })
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
        .catch(function () {});
    }
  }

  try {
    var cachedTenant = sessionStorage.getItem(CONFIG.TENANT_KEY);
    if (cachedTenant) tenantContext = JSON.parse(cachedTenant);
  } catch (e) {}
  fetchTenantConfig();

  // ──────────────────────────────────────────────────
  // 6. EVENT QUEUE & BATCH SENDER
  // ──────────────────────────────────────────────────

  var eventQueue = [];
  var batchTimer = null;
  var deviceId = getOrCreateDeviceId();

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

  function sendPayload(payload, attempt) {
    var url = endpointBase + CONFIG.EVENTS_ENDPOINT;

    if (navigator.sendBeacon && attempt === 0) {
      var blob = new Blob([payload], { type: "text/plain" });
      var sent = navigator.sendBeacon(url, blob);
      if (sent) return;
    }

    if (typeof fetch === "function") {
      fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
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

    try {
      var xhr = new XMLHttpRequest();
      xhr.open("POST", url, true);
      xhr.setRequestHeader("Content-Type", "application/json");
      xhr.onerror = function () {
        if (attempt < CONFIG.MAX_RETRIES) {
          setTimeout(function () {
            sendPayload(payload, attempt + 1);
          }, CONFIG.RETRY_DELAY_MS * Math.pow(2, attempt));
        }
      };
      xhr.send(payload);
    } catch (e) {}
  }

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

  window.addEventListener("beforeunload", function () { flushQueue(); });
  document.addEventListener("visibilitychange", function () {
    if (document.visibilityState === "hidden") flushQueue();
  });

  // ──────────────────────────────────────────────────
  // 7. EVENT DATA EXTRACTORS
  // ──────────────────────────────────────────────────

  // ─── FORMAT B helpers (ikas sendEvent v3 minified) ───

  function extractIkasCartItem(crtItm) {
    if (!crtItm) return {};
    return {
      productId: crtItm.pid || null,
      variantId: crtItm.vid || null,
      quantity: crtItm.c || crtItm.q || 1,
      price: crtItm.p || null
    };
  }

  function extractIkasCart(crt) {
    if (!crt) return {};
    var result = {
      cartId: crt.id || null,
      currency: crt.c || "TRY",
      cartTotalPrice: crt.pr || null,
      cartItemCount: crt.il ? crt.il.length : null
    };
    if (crt.il && Array.isArray(crt.il)) {
      result.items = crt.il.map(function (item) { return extractIkasCartItem(item); });
    }
    return result;
  }

  function extractCartEventDataV3(innerData) {
    if (!innerData) return {};
    var result = {};
    if (innerData.crtItm) {
      var item = extractIkasCartItem(innerData.crtItm);
      result.productId = item.productId;
      result.variantId = item.variantId;
      result.quantity = item.quantity;
      result.price = item.price;
    }
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

  function extractCheckoutEventDataV3(innerData) {
    if (!innerData) return {};
    var ord = innerData.ord || innerData;
    var result = {};
    result.orderId = ord.id || ord.oid || null;
    result.orderNumber = ord.on || ord.orderNumber || null;
    result.totalPrice = ord.pr || ord.tp || null;
    result.currency = ord.c || "TRY";
    var items = ord.il || ord.items;
    if (items && Array.isArray(items)) {
      result.itemCount = items.length;
      result.items = items.map(function (item) { return extractIkasCartItem(item); });
    }
    return result;
  }

  function extractWishlistEventDataV3(innerData) {
    if (!innerData) return {};
    var prd = innerData.pr || innerData.prd || innerData;
    return {
      productId: prd.id || prd.pid || null,
      variantId: prd.vid || null
    };
  }

  function extractEventDataV3(eventType, innerData) {
    switch (eventType) {
      case "ADD_TO_CART":
      case "REMOVE_FROM_CART":
        return extractCartEventDataV3(innerData);
      case "COMPLETE_CHECKOUT":
        return extractCheckoutEventDataV3(innerData);
      case "ADD_TO_WISHLIST":
        return extractWishlistEventDataV3(innerData);
      default:
        return innerData || {};
    }
  }

  // ─── FORMAT A helpers (ikas Storefront Events API — tam obje) ───

  /**
   * FORMAT A: ADD_TO_CART / REMOVE_FROM_CART
   * ikas payload: { type: "addToCart", data: { item: {variant, price, quantity}, quantity, cart: {customer, orderLineItems, totalPrice, shippingAddress} } }
   */
  function extractCartEventDataFormatA(eventData) {
    if (!eventData) return {};
    var result = {};

    var item = eventData.item;
    if (item) {
      result.quantity = eventData.quantity || item.quantity || 1;
      result.price = item.finalPrice || item.price || null;
      result.currency = item.currencyCode || "TRY";

      if (item.variant) {
        result.productId = item.variant.productId || null;
        result.variantId = item.variant.id || null;
        result.productName = item.variant.name || null;
        result.productSlug = item.variant.slug || null;
        if (item.variant.brand) result.brandName = item.variant.brand.name || null;
        if (item.variant.categories && item.variant.categories.length > 0) {
          result.categoryName = item.variant.categories[0].name || null;
        }
      }
    }

    var cart = eventData.cart;
    if (cart) {
      result.cartId = cart.id || null;
      result.currency = cart.currencyCode || result.currency || "TRY";
      result.cartTotalPrice = cart.totalFinalPrice || cart.totalPrice || null;
      var lineItems = cart.orderLineItems;
      if (lineItems && Array.isArray(lineItems)) {
        result.cartItemCount = lineItems.length;
        result.cartItems = lineItems.map(function (li) {
          return {
            productId: li.variant ? li.variant.productId : null,
            variantId: li.variant ? li.variant.id : null,
            quantity: li.quantity || 1,
            price: li.finalPrice || li.price || null,
            productName: li.variant ? li.variant.name : null
          };
        });
      }
    }

    return result;
  }

  function extractCheckoutEventDataFormatA(eventData) {
    if (!eventData) return {};
    var order = eventData.order || eventData.cart || eventData;
    var result = {};
    result.orderId = order.id || null;
    result.orderNumber = order.orderNumber || null;
    result.totalPrice = order.totalFinalPrice || order.totalPrice || null;
    result.currency = order.currencyCode || "TRY";
    var lineItems = order.orderLineItems;
    if (lineItems && Array.isArray(lineItems)) {
      result.itemCount = lineItems.length;
      result.items = lineItems.map(function (li) {
        return {
          productId: li.variant ? li.variant.productId : null,
          variantId: li.variant ? li.variant.id : null,
          quantity: li.quantity || 1,
          price: li.finalPrice || li.price || null,
          productName: li.variant ? li.variant.name : null
        };
      });
    }
    return result;
  }

  function extractWishlistEventDataFormatA(eventData) {
    if (!eventData) return {};
    return {
      productId: eventData.productId || (eventData.product ? eventData.product.id : null) || null,
      variantId: eventData.variantId || null
    };
  }

  // ──────────────────────────────────────────────────
  // 8. EVENT HANDLER
  // ──────────────────────────────────────────────────

  function resolveIkasEventType(numericType) {
    var mapped = CONFIG.IKAS_EVENT_TYPES[numericType];
    if (!mapped) return null;
    var supported = ["ADD_TO_CART", "REMOVE_FROM_CART", "COMPLETE_CHECKOUT", "ADD_TO_WISHLIST"];
    return supported.indexOf(mapped) !== -1 ? mapped : null;
  }

  function normalizeEventType(type) {
    if (!type) return null;
    if (typeof type === "number") return resolveIkasEventType(type);
    var lower = type.toLowerCase().replace(/[_\-\s]/g, "");
    var fromStringMap = CONFIG.IKAS_STRING_EVENTS[lower];
    if (fromStringMap) return fromStringMap;
    var upper = type.toUpperCase().replace(/[_\-\s]/g, "");
    return CONFIG.SUPPORTED_EVENTS[upper] || null;
  }

  function isEventEnabled(eventType) {
    if (!tenantContext || !tenantContext.enabledEvents) return true;
    return tenantContext.enabledEvents.indexOf(eventType) !== -1;
  }

  /**
   * Ana event handler — 3 format destekler:
   *
   * FORMAT A — ikas Storefront Events API (gerçek payload'dan doğrulanmış):
   *   { type: "addToCart", data: { item, quantity, cart: { customer, shippingAddress, orderLineItems } } }
   *   customer bilgisi: data.cart.customer (email, firstName, lastName, id)
   *   telefon bilgisi:  data.cart.shippingAddress.phone
   *
   * FORMAT B — ikas sendEvent v3 (minified):
   *   { e: [{ t: 4, d: { d: { crt, crtItm } } }], ud: { id, em, ph, fn, ln } }
   *
   * FORMAT C — window.__profiqo.track() (manuel):
   *   handleIkasEvent({ type: "ADD_TO_CART", data: {...} })
   */
  function handleIkasEvent(rawPayload) {
    try {
      if (!rawPayload) return;

      var isFormatB = rawPayload.e && Array.isArray(rawPayload.e);
      var hasType = typeof rawPayload.type === "string" || typeof rawPayload.type === "number";
      var isFormatA = hasType && rawPayload.data && typeof rawPayload.data === "object" && !isFormatB;

      // ── FORMAT A: ikas Storefront Events API ──
      if (isFormatA) {
        var eventType = normalizeEventType(rawPayload.type);
        if (!eventType || !isEventEnabled(eventType)) return;

        var eventData = rawPayload.data;

        // ★ Müşteri bilgisini event data'sından çıkar
        //   cart.customer → email, firstName, lastName, id
        //   cart.shippingAddress → phone
        var eventCustomer = extractCustomerFromEventData(eventData);
        if (eventCustomer) {
          updateCustomerContext(eventCustomer);
        }

        // Event data extract (zengin format)
        var extractedData;
        switch (eventType) {
          case "ADD_TO_CART":
          case "REMOVE_FROM_CART":
            extractedData = extractCartEventDataFormatA(eventData);
            break;
          case "COMPLETE_CHECKOUT":
            extractedData = extractCheckoutEventDataFormatA(eventData);
            break;
          case "ADD_TO_WISHLIST":
            extractedData = extractWishlistEventDataFormatA(eventData);
            break;
          default:
            extractedData = eventData || {};
        }

        enqueueEvent({
          eventId: generateId(),
          type: eventType,
          occurredAt: new Date().toISOString(),
          data: extractedData,
          page: getPageContext(),
          customer: customerContext || extractCustomerContext()
        });
        return;
      }

      // ── FORMAT C: Manuel track ──
      if (hasType && !isFormatB) {
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

      // ── FORMAT B: ikas sendEvent v3 ──
      if (!isFormatB) return;

      ikasRawPayload = rawPayload;

      if (rawPayload.ud && (rawPayload.ud.id || rawPayload.ud.em)) {
        updateCustomerContext({
          id: rawPayload.ud.id || null,
          email: rawPayload.ud.em || null,
          firstName: rawPayload.ud.fn || null,
          lastName: rawPayload.ud.ln || null,
          phone: rawPayload.ud.ph || null,
          isGuest: false
        });
      }

      var currentCustomerB = customerContext || extractCustomerContext();

      rawPayload.e.forEach(function (ikasEvent) {
        var evtType = resolveIkasEventType(ikasEvent.t);
        if (!evtType) return;
        if (!isEventEnabled(evtType)) return;

        var innerData = (ikasEvent.d && ikasEvent.d.d) ? ikasEvent.d.d : {};
        var evtData = extractEventDataV3(evtType, innerData);

        var page = getPageContext();
        if (ikasEvent.d) {
          if (ikasEvent.d.u) page.url = ikasEvent.d.u;
          if (ikasEvent.d.r) page.referrer = ikasEvent.d.r;
        }

        enqueueEvent({
          eventId: ikasEvent.id || generateId(),
          type: evtType,
          occurredAt: ikasEvent.ts ? new Date(ikasEvent.ts).toISOString() : new Date().toISOString(),
          data: evtData,
          page: page,
          customer: currentCustomerB
        });
      });

    } catch (e) {
      // Sessizce devam et
    }
  }

  // ──────────────────────────────────────────────────
  // 9. INITIALIZATION
  // ──────────────────────────────────────────────────

  function subscribeToIkasEvents() {
    if (typeof window.IkasEvents !== "undefined" && typeof window.IkasEvents.subscribe === "function") {
      window.IkasEvents.subscribe({ id: CONFIG.SUBSCRIBE_ID, callback: handleIkasEvent });
      return true;
    }
    return false;
  }

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
  // 10. GLOBAL API
  // ──────────────────────────────────────────────────

  window.__profiqo = {
    version: "2.1.0",
    apiKey: apiKey,
    deviceId: deviceId,
    getCustomer: function () { return customerContext || extractCustomerContext(); },
    getTenant: function () { return tenantContext; },
    flush: flushQueue,
    track: function (type, data) {
      handleIkasEvent({ type: type, data: data });
    }
  };

})();