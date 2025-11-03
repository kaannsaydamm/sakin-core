# Sakin Security Platform

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

### Network Sensor (Aktif)

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

3. Network sensor'ü çalıştırın:
   ```sh
   cd sakin-core/services/network-sensor
   dotnet run
   ```
   
   **Not:** Network yakalama için yükseltilmiş izinler gerekir (sudo/admin).

4. Yapılandırma:
   - `sakin-core/services/network-sensor/appsettings.json` dosyasını düzenleyin
   - Veya environment variable kullanın: `Database__Password="your_password"`

Detaylı kurulum için: [network-sensor README](./sakin-core/services/network-sensor/README.md)

### Tüm Platform (Gelecekte)

Platform tam olarak hazır olduğunda:

```bash
# Docker Compose ile tüm servisleri başlat
cd deployments
docker-compose up -d

# veya Kubernetes ile
kubectl apply -k kubernetes/overlays/dev
```

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
