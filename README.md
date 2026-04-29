# Veri Yapilari ile BSP Agaci Tabanli Gorus Alani ve Carpisma Tespiti

Bu repo, PDF'teki **Proje Konu 6** icin C# ile gelistirilmis 2D (top-down) gizlilik oyunu simulasyonudur.

## Proje Ozeti

- Harita duvarlari `BSP Tree` ile tutulur.
- Dusman gorusu `Line of Sight` + `Raycasting` ile hesaplanir.
- Dusman hareketi `Graph + A* Pathfinding` ile yapilir.
- Oyuncu ve dusman duvarlardan gecemez (geometri tabanli carpisma kontrolu).

## PDF Uyum Ozeti

### A.1 Zorunlu Veri Yapilari

- `BspTree`, `BspNode`
- `WaypointGraph`
- `MinHeap<T>`
- `DynamicArray<T>`

### A.2 Zorunlu Algoritmalar

- `Line of Sight`
- `Raycasting / Intersection`
- `A* Pathfinding`

### A.3 Arayuz Gereksinimleri

- WinForms ile 2D kusbakisi cizim
- Dusman FOV (isinlar + dolgulu koni) gosterimi
- Dinamik durum paneli ve canli metrikler

## B.1 Takim Calismasi ve Teknolojik Altyapi

### Eszamanlilik / Asenkron Tasarim

- Oyun dongusu UI'den bagimsiz periyodik `Timer` tick akisi ile calisir.
- Simulasyon hesaplari (`LOS`, `A*`, `raycasting`) her tickte yeniden hesaplanir.
- Proje tek uygulama olarak teslim edilir; mimari servislesmeye uygun modullere ayrilmistir:
  - `GameForm.cs` (UI/simulasyon akisi)
  - `SpatialAlgorithms.cs` (BSP + gorus + carpisma)
  - `Pathfinding.cs` (Graph + A*)
  - `DataStructures.cs` (temel veri yapilari)

### Versiyon Kontrol Kurallari (Git)

- `main` dalina dogrudan push yapilmaz.
- Her uye kendi branch'inde calisir, PR ile birlestirilir.
- Onerilen branch adlari:
  - `feature/oguz-ui`
  - `feature/zeynep-bsp`
  - `feature/baris-astar`
  - `feature/berat-core`

## B.2 Teslim Edilecekler

### Repository ve Mimari

- Bu README proje mimarisini, calisma adimlarini ve modul dagilimini aciklar.

### Docker Konfigurasyonu

- Bu proje su an WinForms masaustu uygulamasidir.
- Docker teslimi icin eklenecek dosyalar:
  - `Dockerfile`
  - `docker-compose.yml`
- Not: WinForms UI'nin Docker icinde calistirilmasi yerine, proje kurali geregi backend/simulasyon katmani container edilip UI yerel calistirilabilir.

### Proje Raporu ve Analiz

- Eklenecek rapor dosyasi: `docs/ProjeRaporu.pdf`
- Rapor icerigi:
  - UML diyagramlari
  - Big-O analizleri
  - Kullanilan AI prompt dokumu

### Demo Videosu

- Eklenecek demo linki (max 10 dk): `[Demo Video Linki](VIDEO_LINK_BURAYA)`

## B.3 Teslim Kurallari ve Code Defense

### Isimlendirme Kurali

- Kodda Turkce karakter iceren sinif/fonksiyon/veritabani isimleri kullanilmamistir.
- Ekip uye isimleri README'de ASCII formatla yazilmistir.

### Code Defense Hazirlik Kontrolu

Her uye su yapilari aciklayabilmelidir:

- `BSP Tree` mantigi ve sorgu akisi
- `MinHeap` yapisi ve A* icindeki rolu
- `Graph` modeli ve komsuluk yapisi
- `Raycasting/Intersection` geometri adimlari
- Zaman karmasikliklari (Big-O)

## Dosya Yapisi

- `Program.cs`: Uygulama girisi, harita/graph kurulumlari
- `CoreTypes.cs`: `Vector2`, `WallSegment`, `RayHit`, `Enemy`
- `DataStructures.cs`: `DynamicArray<T>`, `MinHeap<T>`
- `SpatialAlgorithms.cs`: BSP, LOS, raycasting, collision, geometri
- `Pathfinding.cs`: `WaypointGraph`, `AStarPathfinder`
- `GameForm.cs`: UI, cizim, oyun dongusu, dusman davranisi

## Kurulum ve Calistirma

### Gereksinimler

- .NET 9 SDK
- Visual Studio 2022 (tercihen .NET desktop development workload)

### Terminal ile

```bash
cd "New Proje/StealthVisionSystem"
dotnet restore
dotnet run
```

### Visual Studio 2022 ile

1. `StealthVisionSystem.csproj` dosyasini ac
2. `StealthVisionSystem` projesini `Set as Startup Project` yap
3. `Ctrl + F5` ile calistir

## Zaman Karmasikligi Ozeti

- `Raycasting`: `O(R * C)` (`R`: isin sayisi, `C`: aday duvar sayisi)
- `A*`: `O((V + E) log V)`
- `Collision`: aday duvarlar uzerinde segment kesisim kontrolleri

## Ekip ve Modul Dagilimi

- **Oguz Eren** - 032290038 -> `GameForm.cs`
- **Zeynep Sude Kalkan** - 032290056 -> `SpatialAlgorithms.cs`
- **Baris Kabacaoglu** - 032290027 -> `Pathfinding.cs`
- **Berat Cakir** - 032290054 -> `Program.cs`, `CoreTypes.cs`, `DataStructures.cs`

