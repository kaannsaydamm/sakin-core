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

2. Environment dosyasını hazırlayın:
   ```sh
   cd deployments
   cp .env.example .env
   # Slack webhook URL'sini düzenleyin (opsiyonel):
   # SLACK_WEBHOOK_URL=https://hooks.slack.com/services/YOUR/WEBHOOK/URL
   ```

3. Docker Compose ile altyapıyı başlatın:
   ```sh
   docker compose -f docker-compose.dev.yml up -d
   ```

4. Servislerin hazır olmasını bekleyin (2-3 dakika):
   ```sh
   ./scripts/verify-services.sh
   ```

5. OpenSearch indekslerini oluşturun:
   ```sh
   ./scripts/opensearch/init-indices.sh
   ```

**Başlatılan servisler:**
- ✅ PostgreSQL (5432) - Veritabanı
- ✅ Redis (6379) - Cache
- ✅ Kafka + Zookeeper (9092) - Message queue
- ✅ OpenSearch (9200) + Dashboards (5601) - Search & analytics
- ✅ ClickHouse (8123) - OLAP analytics
- ✅ Prometheus (9090) - Metrics collection
- ✅ Grafana (3000) - Dashboards & visualization
- ✅ Alertmanager (9093) - Alert routing
- ✅ Jaeger (16686) - Distributed tracing
- ✅ SOAR (8080) - Security automation
- ✅ Baseline Worker - Anomaly detection

**Varsayılan Erişim Noktaları:**
- Panel UI: http://localhost:5173 (React)
- Panel API: http://localhost:5000 (Swagger)
- Grafana: http://localhost:3000 (admin / admin)
- Prometheus: http://localhost:9090
- Jaeger: http://localhost:16686
- OpenSearch: http://localhost:9200
- OpenSearch Dashboards: http://localhost:5601

Detaylı kurulum ve kullanım için: [Docker Setup Guide](./deployments/README.md)

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
[Collectors] ──▶ [Ingest] ──▶ [Kafka] ──▶ [Correlation] ──▶ [SOAR] ──▶ [Agents]
     │                            │             │
     ├────────────────────────────┼─────────────┼──────────────┐
     │                            │             │              │
[Network Sensor]          [Enrichment]  [ClickHouse Sink]  [Baseline Worker]
                                │             │
                          [GeoIP/TI]   [Anomaly Detection]
                                │             │
                          [PostgreSQL]   [Redis] ◀──────┐
                                │                       │
                                └──────────────▶ [Analytics Pipeline]

                    ┌─────────────────────────────────┐
                    │   Observability Stack           │
                    ├─────────────────────────────────┤
                    │ Prometheus (Metrics)            │
                    │ Jaeger (Tracing)                │
                    │ Serilog (Logs)                  │
                    │ Grafana (Dashboards)            │
                    └─────────────────────────────────┘
                                 │
                    [Web Panel] ◀─┘
```

**Sprint 7 Yenilikler:**
- OpenTelemetry entegrasyonu (Prometheus metrics, Jaeger traces, JSON logs)
- SOAR Security Automation servisi
- ClickHouse analitikleri ve Baseline Worker ile anomali tespiti
- Prometheus + Grafana monitoring stack
- Yapılandırılmış audit logging pipeline

**Veri Akışı:**
1. **Network Sensor** ve **Collectors** güvenlik verisi toplar
2. **Ingest** katmanı veriyi normalize eder, GeoIP ve Threat Intel ile zenginleştirir
3. **Kafka** servisleri asenkron olarak bağlar
4. **Correlation** olayları analiz eder ve risk skorlama ile tehdit tespit eder
5. **ClickHouse Sink** olayları analytics için depolar
6. **Baseline Worker** anomali tespiti için istatistiksel profil oluşturur
7. **SOAR** otomatik yanıt akışlarını yürütür ve playbook'ları çalıştırır
8. **Panel** görselleştirme, araştırma ve yönetim sağlar
9. **Observability Stack** tüm sistemi izler ve metrikleri toplar

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

**Sprint 7 (DevOps & Monitoring)**
- [Monitoring Stack Guide](./deployments/monitoring/README.md) - Prometheus, Grafana, Alertmanager setup
- [CHANGELOG.md](./CHANGELOG.md) - Tüm sürüm ve özellik değişiklikleri
- [Anomaly Detection Guide](./docs/anomaly-detection.md) - ML/Baseline mekanizması
- [Alert Lifecycle Guide](./docs/alert-lifecycle.md) - Alert durumu yönetimi
- [SOAR Documentation](./docs/sprint7-soar.md) - Playbook ve otomasyon

**Genel Dokümantasyon**
- [Mimari Dokümantasyon](./docs/README.md)
- [Migration Summary](./MIGRATION_SUMMARY.md)
- [Configuration Guide](./docs/configuration.md)
- [Contributing Guidelines](./docs/README.md) (yakında)

## 🔐 Güvenlik

Güvenlik açıklarını lütfen GitHub Issues üzerinden değil, doğrudan proje sahiplerine bildirin.

## 📄 Lisans

[LICENSE](./LICENSE) dosyasına bakın.

## 🤝 Katkıda Bulunma

Katkılarınızı bekliyoruz! Lütfen önce bir issue açarak değişikliğinizi tartışın.

## 📧 İletişim

Proje sahibi: [@kaannsaydamm](https://github.com/kaannsaydamm)
