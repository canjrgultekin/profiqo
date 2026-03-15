# Profiqo

Profiqo, çok kanallı e-ticaret operasyonları için “müşteri zekâsı + retention otomasyonu + net kâr odaklı analitik” yaklaşımıyla tasarlanmış çok kiracılı (multi-tenant) bir SaaS altyapısıdır. Bu repo, yönetim paneli (Next.js) ile backend (ASP.NET Core API + Worker’lar) tarafını aynı monorepo altında taşır, ikas başta olmak üzere Trendyol ve WhatsApp gibi kanallara entegrasyon için temel altyapıyı içerir.

Bu doküman “projeyi ayağa kaldır, veriyi topla, panelden yönet, operasyonu gözlemle” akışını uçtan uca anlatır. Üretimde çalıştırmadan önce tüm gizli anahtarların ve bağlantı bilgilerinin environment üzerinden verilmesi gerekir, repoda bulunan örnek değerleri asla gerçek ortamda kullanma.

## Repo yapısı

`apps/admin` altında Next.js App Router tabanlı admin panel bulunur. Panel cookie tabanlı auth ile çalışır ve backend’e doğrudan client-side çağrı atmak yerine Next API route’ları üzerinden proxy (BFF) mantığıyla gider. Böylece token, tenant bilgisi ve backend URL gibi detaylar tek yerde kontrol edilir.

`backend` altında .NET 10 hedefleyen çözüm yer alır. `src/Profiqo.Api` ana HTTP API host’tur. `src/Profiqo.Worker` entegrasyon job’larını ve sync otomasyon scheduler’ını çalıştıran background host’tur. `src/Profiqo.Whatsapp.Automation.Worker` WhatsApp otomasyon scheduler ve sender worker’larını çalıştırır. Domain–Application–Infrastructure ayrımı korunur, EF Core ile PostgreSQL kullanılır.

`/scripts` altında ikas Storefront Events (pixel) için JS tracker script’i bulunur. Bu script, mağaza tarafında oluşan event’leri Profiqo API’ye batch halinde taşır.

## Temel konseptler

Profiqo multi-tenant çalışır. API tarafında tenant, default olarak `X-Tenant-Id` header’ı ile çözülür. DbContext seviyesinde query filter ve SaveChanges interceptor’ları ile tenant izolasyonu enforce edilir. Admin panel, kullanıcı token’ını ve tenant bilgisini cookie’de taşır, backend’e proxy ederken tenant header’ını otomatik basar.

Event-driven dayanıklılık için Outbox/Inbox tabloları, idempotent dispatch mantığı ve “at-least-once” yaklaşımı benimsenir. Entegrasyonlar tarafında progress ve cursor mantığıyla incremental sync çalışır. Storefront event ingestion tarafında rate limit ve apiKey doğrulaması kullanılır.

## Gereksinimler

Backend için .NET SDK 10.0.x gerekir. Frontend için Node.js (LTS önerilir) ve npm gerekir. Veri katmanı için PostgreSQL gerekir. OpenTelemetry için OTLP endpoint opsiyoneldir, yoksa localde kapatabilir ya da bir collector ile çalıştırabilirsin.

## Hızlı başlangıç (lokal)

Aşağıdaki akış local geliştirme için güvenli bir başlangıç verir. PostgreSQL’i ister local kurulumla ister Docker ile kaldırabilirsin. Docker kullanacaksan bu compose’u kopyalayıp `docker-compose.dev.yml` gibi çalıştırman yeterli.

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: profiqo
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: your_password
    ports:
      - "5432:5432"
    volumes:
      - profiqo_pg:/var/lib/postgresql/data

  pgadmin:
    image: dpage/pgadmin4:8
    environment:
      PGADMIN_DEFAULT_EMAIL: admin@local
      PGADMIN_DEFAULT_PASSWORD: admin
    ports:
      - "5050:80"
    depends_on:
      - postgres

volumes:
  profiqo_pg:
```

Docker ayağa kalktıktan sonra backend migration’larını çalıştır.

```bash
cd backend
dotnet restore

# dotnet-ef yüklü değilse:
dotnet tool install --global dotnet-ef

# migration uygulama (Infrastructure migration assembly, Api startup)
dotnet ef database update --project src/Profiqo.Infrastructure --startup-project src/Profiqo.Api
```

Ardından API’yi çalıştır.

```bash
dotnet run --project src/Profiqo.Api
```

İsteğe bağlı olarak worker’ları da aynı şekilde çalıştır.

```bash
dotnet run --project src/Profiqo.Worker
dotnet run --project src/Profiqo.Whatsapp.Automation.Worker
```

Admin paneli çalıştırma:

```bash
cd ../apps/admin
npm ci
npm run dev
```

Panel default olarak `http://localhost:3000` üzerinde açılır. Backend default olarak `http://localhost:5164` üzerinde çalışır.

## Konfigürasyon

Backend konfigürasyonu `appsettings.json` + environment override ile yönetilir. Üretimde her şeyi environment üzerinden vermek gerekir. Örnek environment değişkenleri aşağıdaki gibidir.

```bash
# PostgreSQL
ConnectionStrings__ProfiqoDb=Host=localhost;Port=5432;Database=profiqo;Username=postgres;Password=YOUR_PASSWORD

# JWT
Profiqo__Auth__Issuer=profiqo-local
Profiqo__Auth__Audience=profiqo-admin
Profiqo__Auth__JwtSigningKey=PLEASE_SET_MIN_32_CHARS_SECRET

# Crypto (AES-GCM)
Profiqo__Crypto__MasterKey=PLEASE_SET_MIN_32_CHARS_SECRET

# Tenancy header adı
Profiqo__Tenancy__TenantHeaderName=X-Tenant-Id

# Observability
Profiqo__Observability__OtlpEndpoint=http://localhost:4317

# Integrations
Profiqo__Integrations__Ikas__GraphqlEndpoint=https://api.myikas.com/api/admin/graphql
Profiqo__Integrations__Ikas__DefaultPageSize=50
Profiqo__Integrations__Ikas__DefaultMaxPages=20
```

Frontend tarafında `apps/admin/.env.local` içinde backend URL tanımlıdır. Localde tipik kullanım aşağıdaki gibidir.

```bash
PROFIQO_BACKEND_URL=http://localhost:5164
```

## Auth ve tenant oluşturma (API)

İlk kullanımda tenant ve owner user oluşturmak için register endpoint’i kullanılır. Response içinde access token ve tenant bilgileri döner. Admin panel de aynı endpoint’i kullanır.

```bash
curl -X POST http://localhost:5164/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "tenantName":"Demo Store",
    "tenantSlug":"demo-store",
    "ownerEmail":"owner@demo.local",
    "ownerPassword":"ChangeMe_123!",
    "ownerDisplayName":"Demo Owner"
  }'
```

Login:

```bash
curl -X POST http://localhost:5164/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "tenantSlug":"demo-store",
    "email":"owner@demo.local",
    "password":"ChangeMe_123!"
  }'
```

Birçok business endpoint’i `Authorization: Bearer <token>` ve `X-Tenant-Id: <tenantGuid>` gerektirir. Admin panel bunu cookie + proxy ile yönetir, API’ye manuel istek atacaksan header’ları sen basarsın.

## ikas entegrasyonu

ikas entegrasyonu iki aşamalıdır. Önce connection oluşturulur, sonra sync başlatılır. Connection tarafında storeName ve private app clientId/clientSecret (ya da legacy token) kullanılır. Sync tarafında scope belirlenir. Tipik scope değerleri “customers”, “orders”, “abandoned”, “both” şeklindedir.

Connection bilgisi:

```bash
curl http://localhost:5164/api/integrations/ikas/connection \
  -H "Authorization: Bearer <token>" \
  -H "X-Tenant-Id: <tenantId>"
```

Connect:

```bash
curl -X POST http://localhost:5164/api/integrations/ikas/connect \
  -H "Authorization: Bearer <token>" \
  -H "X-Tenant-Id: <tenantId>" \
  -H "Content-Type: application/json" \
  -d '{
    "storeLabel":"My Store",
    "storeName":"mystore",
    "clientId":"<ikas_private_app_client_id>",
    "clientSecret":"<ikas_private_app_client_secret>"
  }'
```

Sync başlatma:

```bash
curl -X POST http://localhost:5164/api/integrations/ikas/sync/start \
  -H "Authorization: Bearer <token>" \
  -H "X-Tenant-Id: <tenantId>" \
  -H "Content-Type: application/json" \
  -d '{
    "connectionId":"<connectionGuid>",
    "scope":"both",
    "pageSize":50,
    "maxPages":20
  }'
```

Sync job takip:

```bash
curl http://localhost:5164/api/integrations/ikas/jobs/<jobId> \
  -H "Authorization: Bearer <token>" \
  -H "X-Tenant-Id: <tenantId>"
```

## Trendyol entegrasyonu

Trendyol tarafında sipariş verileri çoğu zaman maskeli email ile gelir. Profiqo, identity resolution ve dedupe hattıyla bu kimlikleri normalize edip müşteri profiline bağlamayı hedefler. Trendyol sync pipeline’ı da integration_jobs + cursor yaklaşımıyla çalışır. Endpoint’ler `api/integrations/trendyol` altında konumlanır.

## WhatsApp otomasyonları

WhatsApp otomasyonu iki parçadır. API, template ve rule yönetimini sağlar. Worker tarafı scheduler ve sender işini yürütür. Graph API erişimi için WhatsApp config bölümündeki token ve version alanları environment üzerinden set edilmelidir. Localde gerçek mesaj göndermeden test etmek için “test mode” opsiyonlarını kullanman gerekir.

## Storefront events (ikas pixel)

`/scripts/profiqo-ikas-events.min.js` dosyası ikas Storefront Events mekanizmasına entegre edilmek için hazırlanmıştır. Script ilk load’da `GET /api/v1/events/storefront/config?apiKey=...` çağırır, sonrasında batch event’leri `POST /api/v1/events/storefront` endpoint’ine yollar. Bu endpoint JWT istemez, tenant doğrulaması public apiKey ile yapılır. Public apiKey, DB’de `provider_connections` tablosunda ProviderType=Pixel kaydı üzerinden çözülür.

Örnek script include (cdn ya da kendi host’un):

```html
<script src="https://<your-cdn>/ikas/v2/profiqo-ikas-events.min.js?apiKey=pfq_pub_XXXXX"></script>
```

Local test için payload göndermek istersen:

```bash
curl -X POST http://localhost:5164/api/v1/events/storefront \
  -H "Content-Type: application/json" \
  -d '{
    "apiKey":"pfq_pub_XXXXX",
    "deviceId":"did-123",
    "sessionId":"sid-123",
    "sentAt":"2026-02-17T00:00:00Z",
    "customer":{"email":"a@b.com"},
    "events":[{"eventId":"e1","type":"ADD_TO_CART","occurredAt":"2026-02-17T00:00:00Z","data":{"productId":"p1"}}]
  }'
```

## Gözlemlenebilirlik ve logging

API Serilog ile yapılandırılmıştır. OpenTelemetry tracing ve metrics için OTLP exporter aktiftir. Localde OTLP endpoint yoksa ya bir collector kurup `Profiqo__Observability__OtlpEndpoint` üzerinden bağla, ya da endpoint’i erişilebilir bir host’a yönlendir. Prod ortamda correlation id middleware ile request bazlı izlenebilirlik sağlanır.

## Güvenlik notları

JWT signing key ve master key minimum 32 karakter olmalıdır. Repo içinde bulunan örnek değerler sadece local geliştirme içindir. Üretimde tüm secret’lar environment ya da secret manager üzerinden verilmelidir. Storefront ingestion endpoint’i public olduğu için apiKey rotasyonunu ve rate limit limitlerini prod’ta mutlaka operational seviyede yönet.

## Lisans

Bu repo özel bir ürün geliştirme repo’su olarak konumlanmıştır. Lisans ve kullanım koşulları organizasyon içinde belirlenir. Third-party template kullanımları ilgili lisanslarına tabidir.

