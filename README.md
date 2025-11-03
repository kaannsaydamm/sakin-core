# Sakin Security Platform

[![CI](https://github.com/kaannsaydamm/sakin-core/actions/workflows/ci.yml/badge.svg)](https://github.com/kaannsaydamm/sakin-core/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

[sakin-csharp](https://github.com/kaannsaydamm/sakin-csharp)'ın halefi. Modern, modüler bir güvenlik platformu.

## 🏗️ Mono-Repo Yapısı

Bu repository, Sakin güvenlik platformunun tüm bileşenlerini içeren bir mono-repo olarak yapılandırılmıştır.

```
sakin-platform/
├── sakin-core/              # Core network monitoring services
│   └── services/
│       └── network-sensor/  # ✅ Network packet capture and analysis
├── sakin-collectors/        # 🚧 Additional data collectors
├── sakin-ingest/            # 🚧 Data ingestion and normalization
├── sakin-msgbridge/         # 🚧 Message broker integration
├── sakin-correlation/       # 🚧 Event correlation and threat detection
├── sakin-soar/              # 🚧 Security orchestration and automation
├── sakin-panel/             # 🚧 Web UI (currently separate repo)
├── sakin-utils/             # 🚧 Shared libraries and utilities
├── deployments/             # 🚧 Docker, K8s, IaC configurations
└── docs/                    # 🚧 Centralized documentation
```

**Durum:** ✅ Aktif | 🚧 Geliştirilecek

## 📦 Bileşenler

### sakin-core
Temel ağ izleme ve paket analizi servisleri.
- **network-sensor**: .NET 8 tabanlı ağ trafiği yakalama ve analiz servisi (SharpPcap, PacketDotNet)
- [Detaylı döküman](./sakin-core/README.md)

### sakin-collectors
Çeşitli kaynaklardan güvenlik verisi toplayan ajanlar ve eklentiler.
- [Detaylı döküman](./sakin-collectors/README.md)

### sakin-ingest
Veri alımı ve normalizasyon katmanı.
- [Detaylı döküman](./sakin-ingest/README.md)

### sakin-msgbridge
Servisler arası mesajlaşma ve event bus altyapısı.
- [Detaylı döküman](./sakin-msgbridge/README.md)

### sakin-correlation
Olay korelasyonu ve tehdit tespit motoru.
- [Detaylı döküman](./sakin-correlation/README.md)

### sakin-soar
Güvenlik orkestrasyon, otomasyon ve yanıt platformu.
- [Detaylı döküman](./sakin-soar/README.md)

### sakin-panel
Web tabanlı kullanıcı arayüzü ve yönetim paneli.
- [Detaylı döküman](./sakin-panel/README.md)

### sakin-utils
Paylaşılan kütüphaneler ve yardımcı araçlar.
- [Detaylı döküman](./sakin-utils/README.md)

### deployments
Deployment yapılandırmaları ve altyapı kodları.
- [Detaylı döküman](./deployments/README.md)

### docs
Platform dokümantasyonu.
- [Detaylı döküman](./docs/README.md)

## 🚀 Hızlı Başlangıç

### Docker Compose ile Tüm Platform (Önerilen)

**En hızlı yol:** Tüm altyapı servislerini Docker ile başlatın:

1. Repository'yi klonlayın:
   ```sh
   git clone https://github.com/kaannsaydamm/sakin-core.git
   cd sakin-core
   ```

2. Docker Compose ile altyapıyı başlatın:
   ```sh
   cd deployments
   docker compose -f docker compose.dev.yml up -d
   ```

3. Servislerin hazır olmasını bekleyin (1-2 dakika):
   ```sh
   ./scripts/verify-services.sh
   ```

4. OpenSearch indekslerini oluşturun:
   ```sh
   ./scripts/opensearch/init-indices.sh
   ```

5. Network sensor'ü çalıştırın:
   ```sh
   cd ../sakin-core/services/network-sensor
   export Database__Host=localhost
   export Database__Password=postgres_dev_password
   sudo dotnet run
   ```

**Başlatılan servisler:**
- ✅ PostgreSQL (5432) - Veritabanı
- ✅ Redis (6379) - Cache
- ✅ Kafka + Zookeeper (9092) - Message queue
- ✅ OpenSearch (9200) + Dashboards (5601) - Search & analytics
- ✅ ClickHouse (8123) - OLAP analytics

Detaylı kurulum ve kullanım için: [Docker Setup Guide](./deployments/DOCKER_SETUP.md)

### Manuel Kurulum (Network Sensor)

Docker kullanmadan sadece network sensor'ü çalıştırmak için:

1. Repository'yi klonlayın:
   ```sh
   git clone https://github.com/kaannsaydamm/sakin-core.git
   cd sakin-core
   ```

2. Solution'ı derleyin:
   ```sh
   dotnet restore
   dotnet build SAKINCore-CS.sln
   ```

3. PostgreSQL veritabanını hazırlayın:
   ```sh
   # PostgreSQL'e bağlanın ve veritabanı oluşturun
   createdb network_db
   psql network_db < deployments/scripts/postgres/01-init-database.sql
   ```

4. Network sensor'ü çalıştırın:
   ```sh
   cd sakin-core/services/network-sensor
   dotnet run
   ```
   
   **Not:** Network yakalama için yükseltilmiş izinler gerekir (sudo/admin).

5. Yapılandırma:
   - `sakin-core/services/network-sensor/appsettings.json` dosyasını düzenleyin
   - Veya environment variable kullanın: `Database__Password="your_password"`

Detaylı kurulum için: [network-sensor README](./sakin-core/services/network-sensor/README.md)

## 🏛️ Mimari

Sakin platformu mikroservis mimarisini takip eder:

```
[Collectors] ──▶ [Ingest] ──▶ [Message Bridge] ──▶ [Correlation] ──▶ [SOAR]
     │                                                      │              │
     ├──────────────────▶ [PostgreSQL] ◀──────────────────┘              │
     │                                                                     │
[Network Sensor]                                                          │
                                                                           │
                                    [Web Panel] ◀─────────────────────────┘
```

**Veri Akışı:**
1. **Network Sensor** ve **Collectors** güvenlik verisi toplar
2. **Ingest** katmanı veriyi normalize eder ve zenginleştirir
3. **Message Bridge** servisleri asenkron olarak bağlar
4. **Correlation** olayları analiz eder ve tehdit tespit eder
5. **SOAR** otomatik yanıt akışlarını yürütür
6. **Panel** görselleştirme ve yönetim sağlar

## 🛠️ Geliştirme

### Gereksinimler
- .NET 8 SDK
- PostgreSQL 13+
- Docker & Docker Compose (opsiyonel)
- Node.js 18+ (panel için)

### Solution Yapısı
```sh
dotnet build SAKINCore-CS.sln  # Tüm .NET projeleri derle
```

### Test
```sh
dotnet test
```

## 📚 Dokümantasyon

- [Mimari Dokümantasyon](./docs/README.md)
- [Migration Summary](./MIGRATION_SUMMARY.md)
- [Contributing Guidelines](./docs/README.md) (yakında)

## 🔐 Güvenlik

Güvenlik açıklarını lütfen GitHub Issues üzerinden değil, doğrudan proje sahiplerine bildirin.

## 📄 Lisans

[LICENSE](./LICENSE) dosyasına bakın.

## 🤝 Katkıda Bulunma

Katkılarınızı bekliyoruz! Lütfen önce bir issue açarak değişikliğinizi tartışın.

## 📧 İletişim

Proje sahibi: [@kaannsaydamm](https://github.com/kaannsaydamm)
