# SAKINCore-CS

[sakin-csharp](https://github.com/kaannsaydamm/sakin-csharp)'ın halefi. Core olarak çalışıyor.

## Mono-Repo Yapısı

Bu proje artık mono-repo mimarisi kullanmaktadır. Tüm SAKIN platform bileşenleri tek bir repository'de organize edilmiştir:

```
sakin-core/
├── README.md
└── services/
    └── network-sensor/          # Network traffic monitoring service (legacy SAKINCore-CS)
        ├── Handlers/
        ├── Utils/
        ├── Program.cs
        └── SAKINCore-CS.csproj

sakin-collectors/                # Data collection agents (placeholder)
sakin-ingest/                    # Data ingestion pipeline (placeholder)
sakin-msgbridge/                 # Message broker infrastructure (placeholder)
sakin-correlation/               # Event correlation engine (placeholder)
sakin-soar/                      # Security orchestration platform (placeholder)
sakin-panel/                     # Web UI and management console (placeholder)
sakin-utils/                     # Shared utilities (placeholder)
deployments/                     # Deployment configs and IaC (placeholder)
docs/                            # Platform documentation (placeholder)
```

### Bileşenler

#### sakin-core
Core servisler ve temel bileşenler. Şu anda network-sensor servisi içerir (eski SAKINCore-CS projesi).

#### sakin-collectors
Farklı kaynaklardan veri toplayan collector'lar ve connector'lar.

#### sakin-ingest
Gelen verilerin işlenmesi ve normalize edilmesi için veri alma pipeline'ı.

#### sakin-msgbridge
SAKIN bileşenleri arasında mesaj kuyruğu ve event dağıtımı sağlar.

#### sakin-correlation
Güvenlik tehditleri ve desenleri tespit etmek için çoklu kaynaklardan gelen event'leri analiz eder ve korelasyon yapar.

#### sakin-soar
Otomatik olay müdahale iş akışları ve güvenlik orkestrasyonu yetenekleri sağlar.

#### sakin-panel
SAKIN platformu için web tabanlı kullanıcı arayüzü ve yönetim konsolu.

#### sakin-utils
Birden fazla SAKIN bileşeni tarafından kullanılan ortak kod, utility'ler ve kütüphaneler.

#### deployments
Docker, Kubernetes ve CI/CD pipeline konfigürasyonları.

#### docs
Platform için kapsamlı dokümantasyon.

## Kurulum Adımları

1. Bu projeyi klonlayın:
   ```sh
   git clone https://github.com/kaannsaydamm/sakin-core.git
   cd sakin-core
   ```

2. Gerekli bağımlılıkları yükleyin:
   ```sh
   dotnet restore
   ```

3. Projeyi derleyin:
   ```sh
   dotnet build 
   ```

4. Network sensor'ü başlatın:
   ```sh
   cd sakin-core/services/network-sensor
   dotnet run
   ```


## Önyüz Kurulumu

1. Önyüz projesini klonlayın:
   ```sh
   git clone https://github.com/kaannsaydamm/sakin-panel.git
   cd sakin-panel
   ```

2. `.env` dosyasını oluşturun ve `DATABASE_URL` değişkenini ayarlayın:
   ```sh
   echo DATABASE_URL= > .env
   ```

3. Gerekli bağımlılıkları yükleyin:
   ```sh
   npm i
   ```

4. Projeyi derleyin:
   ```sh
   npm run build
   ```

5. Projeyi başlatın:
   ```sh
   npm run start
   ```

6. Önyüz çalışır durumda olacak: `http://localhost:3000`. Artık sakin hazır.
