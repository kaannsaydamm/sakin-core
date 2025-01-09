# SAKINCore-CS

[sakin-csharp](https://github.com/kaannsaydamm/sakin-csharp)'ın halefi. Core olarak çalışıyor.

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

4. Projeyi başlatın:
   ```sh
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
