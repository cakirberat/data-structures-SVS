# Veri Yapilari ile BSP Agaci Tabanli Gorus Alani ve Carpisma Tespiti

Bu proje, PDF'teki **Proje Konu 6** gereksinimlerine uygun olarak C# ile gelistirilmis 2D (top-down) bir gizlilik oyunu simulasyonudur. Oyuncu engeller arasinda hedefe ulasmaya calisirken, dusman raycasting tabanli gorus sistemi ile oyuncuyu yakalamaya calisir.

## Proje Amaci

- Oyun haritasini uygun veri yapilariyla modellemek
- Gorus cizgisi ve gorus konisi hesaplamalarini verimli yapmak
- Duvar carpisma kontrolunu guvenilir sekilde yapmak
- Dusman hareketi icin A* tabanli yol bulma kullanmak

##  Faz 1 - Zorunlu Veri Yapilari

Bu projede asagidaki veri yapilari sifirdan implemente edilmistir:

- `BSP Tree` (`BspTree`, `BspNode`): Duvar segmentlerini uzamsal olarak boler, gorus/carpisma sorgularinda adaylari daraltir.
- `Graph` (`WaypointGraph`): Yurune bilir noktalar arasi baglantilari tutar.
- `Min-Heap` (`MinHeap<T>`): A* acik kumesinde en dusuk maliyetli dugumu secer.
- `Dynamic Array` (`DynamicArray<T>`): Duvarlar, isin sonuclari ve komsuluk listeleri icin kullanilir.

##  Faz 2 - Zorunlu Algoritmalar

- `Line of Sight`: Dusman ve oyuncu arasinda duvar kesisi var mi kontrol edilir.
- `Raycasting / Intersection`: Dusmanin FOV konisindeki isinlarin duvarlarla kesisim noktasi hesaplanir.
- `A* Pathfinding`: Dusmanin engelleri dolasarak hedefe ulasmasi icin kullanilir.

##  Faz 3 - Arayuz Gereksinimleri

Proje, WinForms tabanli 2D bir arayuz ile gelistirilmistir:

- Kusbakisi harita cizimi
- Duvarlarin ve yurune bilir graph'in gosterimi
- Dusman gorus konisi (ray + dolgulu fan) gosterimi
- Oyuncu/dusman hareketi ve durum paneli
- Canli metrik paneli (ray sayisi, path suresi, frame suresi)

## Oyun Davranisi

- Oyuncu `W`, `A`, `S`, `D` ile hareket eder.
- Oyuncu duvarlardan gecemez (segment kesisim tabanli carpisma kontrolu).
- Dusman devriye modunda rotalar arasinda gezer.
- Oyuncu gorus konisine ve LOS'e girerse dusman takip moduna gecer.
- Oyuncu hedefe ulasirsa kazanir; gorulurse yakalanir.
- `R` tusu oyunu sifirlar.

## Dosya Yapisi

- `New Proje/StealthVisionSystem/Program.cs`: Uygulama girisi, harita ve graph kurulumlari
- `New Proje/StealthVisionSystem/CoreTypes.cs`: Temel tipler (`Vector2`, `WallSegment`, `RayHit`, `Enemy`)
- `New Proje/StealthVisionSystem/DataStructures.cs`: `DynamicArray<T>`, `MinHeap<T>`
- `New Proje/StealthVisionSystem/SpatialAlgorithms.cs`: BSP, LOS, raycasting, collision, geometri
- `New Proje/StealthVisionSystem/Pathfinding.cs`: `WaypointGraph`, `AStarPathfinder`
- `New Proje/StealthVisionSystem/GameForm.cs`: Arayuz, cizim, oyun dongusu ve AI modlari

## Kurulum ve Calistirma

### Gereksinimler

- .NET 9 SDK
- Visual Studio 2022 (tercihen .NET desktop development workload ile)

### Terminal ile

```bash
cd "New Proje/StealthVisionSystem"
dotnet restore
dotnet run
```

### Visual Studio 2022 ile

1. `StealthVisionSystem.csproj` dosyasini ac
2. `StealthVisionSystem` icin `Set as Startup Project` yap
3. `Ctrl + F5` ile calistir

## Zaman Karmasikligi Ozeti

- `BSP Query`: Ortalama durumda tum duvarlari gezmeden aday duvar listesini cikarir
- `Raycasting`: `R` isin icin yaklasik `O(R * C)` (`C`: BSP'den gelen aday duvar sayisi)
- `A*`: Graph uzerinde standart olarak `O((V + E) log V)` (heap kullanimina bagli)
- `Collision`: Segment kesisim kontrolleri ile aday duvarlar uzerinden calisir

## Ekip Bilgileri

- **Oguz Eren** - 032290038
- **Zeynep Sude Kalkan** - 032290056
- **Baris Kabacaoglu** - 032290027
- **Berat Cakir** - 032290054

## Ekip Icindeki Modul Dagilimi

- Oguz Eren: `GameForm.cs` (arayuz, oyun dongusu, panel)
- Zeynep Sude Kalkan: `SpatialAlgorithms.cs` (BSP, LOS, raycasting, carpisma)
- Baris Kabacaoglu: `Pathfinding.cs` (graph ve A* pathfinding)
- Berat Cakir: `Program.cs`, `CoreTypes.cs`, `DataStructures.cs` (cekirdek ve veri yapilari)

