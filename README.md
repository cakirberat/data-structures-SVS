# Veri Yapıları ile BSP Ağacı Tabanlı Görüş Alanı ve Çarpışma Tespiti

> **Bursa Uludağ Üniversitesi — Bilgisayar Mühendisliği**  
> Veri Yapıları Dersi Bitirme Projesi · 2025–2026 Bahar · Grup 6 · Konu 6  
> GitHub: <https://github.com/cakirberat/data-structures-SVS>
> Rapor: <https://drive.google.com/drive/folders/1IcNR6AW7b7o_IT7-qJa0x9FfDj5ftFC6?usp=drive_link>

---

## Proje Nedir?

Bu proje, C# programlama dili ve WinForms kütüphanesi kullanılarak geliştirilmiş, **2 boyutlu kuşbakışı (top-down) bir gizlilik oyunu simülasyonudur**.

Oyuncunun görevi basittir: engel dolu haritalarda düşmanlara yakalanmadan çıkış noktasına ulaşmak. Ancak düşmanlar sıradan takip mantığıyla değil; **BSP ağacı tabanlı görüş hesabı**, **raycasting**, **A* yol bulma algoritması** ve **daire tabanlı çarpışma sistemi** gibi gerçek dünya oyun motoru bileşenleriyle donatılmıştır.

Projenin temel felsefesi, derste öğrenilen veri yapılarını (BSP ağacı, yığın/heap, dinamik dizi, çizge) soyut örneklerle değil; **çalışan ve oynanabilir bir sistemin içinde** göstermektir.

---

## Ekip

| Ad Soyad | Öğrenci No | Sorumlu Olduğu Alan |
|---|---|---|
| Oguz Eren | 032290038 | Oyun döngüsü, render, input, HUD, overlay |
| Zeynep Sude Kalkan | 032290056 | BSP, görüş alanı (LOS/FOV), çarpışma geometrisi |
| Baris Kabacaoglu | 032290027 | A* yol bulma, devriye sistemi, navigasyon çizgesi |
| Berat CAKIR | 032290054 | Çekirdek tipler, veri yapıları, seviye tanımları, dokümantasyon |

---

## Proje Amacı ve Teknik Hedefler

Bu projede aşağıdaki teknik hedefler belirlenmiş ve tamamı hayata geçirilmiştir:

1. **Uzamsal Veri Yapısı (BSP Ağacı):** Haritadaki yüzlerce duvar segmentini doğrusal tarama yapmadan sorgulamak için Binary Space Partitioning ağacı kullanılması.
2. **Görüş Çizgisi (Line of Sight):** İki nokta arasında duvar olup olmadığının BSP ağacı üzerinden O(log n) karmaşıklıkta sorgulanması.
3. **Görüş Konisi (Field of View):** Düşmanın gerçekçi görüş alanının raycasting ile duvar kısıtlı poligon olarak hesaplanması; oyuncunun bu poligonun içinde olup olmadığının belirlenmesi.
4. **Yol Bulma (A\*):** Düşmanların devriye rotalarında engellere takılmadan, waypoint çizgesi ve A* algoritması ile akıllıca hareket etmesi.
5. **Güvenilir Çarpışma:** Oyuncu ve düşmanların duvarlardan geçememesi için nokta tabanlı değil, daire tabanlı (swept-circle) çarpışma kontrolü ve 8 yönlü kaydırma (sliding) mekanizması.
6. **Seviye Sistemi:** Artan zorlukla birbirinden farklı tasarımlara sahip üç harita.

---

## Kullanılan Veri Yapıları

Aşağıdaki tüm yapılar .NET kütüphanelerine (List, Dictionary vb.) bağımlılık olmadan **sıfırdan yazılmıştır**.

### 1. `DynamicArray<T>` — `DataStructures.cs`

`List<T>`'nin manuel implementasyonudur. İçinde bir ham dizi tutar; dizi dolduğunda kapasitesini iki katına çıkararak yeniden tahsis eder. `IEnumerable<T>` arayüzü implement edilmiştir, bu sayede `foreach` ile dolaşılabilir.

**Nerede kullanılır?**
- Tüm duvar segmentleri (`DynamicArray<Segment>`)
- FOV poligonunun köşe noktaları (`DynamicArray<Vector2D>`)
- Komşuluk listeleri (navigasyon çizgesindeki kenarlar)

| İşlem | Zaman Karmaşıklığı | Açıklama |
|---|---|---|
| `Add` (amortize) | **O(1)** | Kapasite dolmadan ekleme anlık; dolduğunda tek seferlik kopyalama |
| `Get` / `Set` | **O(1)** | Dizi indeksiyle doğrudan erişim |
| `RemoveAt` | **O(n)** | Sonraki elemanlar sola kaydırılır |
| `RemoveLast` | **O(1)** | Sadece sayaç düşürülür |

---

### 2. `MinHeap<T>` — `DataStructures.cs`

Dizi üzerine kurulu ikili minimum yığın (binary min-heap). `BubbleUp` (yukarı kabarcık) ve `BubbleDown` (aşağı kabarcık) metodları ile heap özelliği korunur: her düğüm çocuklarından küçük veya eşittir.

**Nerede kullanılır?**
- A* algoritmasının **açık kümesi (open set)** — her adımda en düşük `f = g + h` maliyetli düğümü O(log n)'de seçmek için.

| İşlem | Zaman Karmaşıklığı | Açıklama |
|---|---|---|
| `Push` (ekle) | **O(log n)** | Sona eklenir, BubbleUp ile yerine taşınır |
| `Pop` (çıkar) | **O(log n)** | Kök çıkarılır, son eleman köke alınır, BubbleDown ile yerleşir |
| `Peek` (bak) | **O(1)** | Kök doğrudan okunur |
| `IsEmpty` | **O(1)** | Eleman sayısı kontrolü |

---

### 3. `BspTree` / `BspNode` — `DataStructures.cs`

Binary Space Partitioning ağacı, haritadaki duvar segmentlerini bir bölme düzlemi etrafında özyinelemeli olarak ikiye böler. Sonuç: hangi duvarların ilgili bölgede olduğu yalnızca birkaç ağaç geçişiyle bulunur.

**Build aşaması:** İlk segment bölme düzlemi seçilir; diğer segmentler öndeki/arkadaki olarak ayrılır. Özyinelemeli olarak alt ağaçlar oluşturulur.

**LOS sorgusu:** Sorgu segmentinin hangi tarafta olduğu `cross product` ile hesaplanır; sadece ilgili alt ağaç taranır (prune). `da >= 0 || db >= 0` ve `da <= 0 || db <= 0` kontrolleriyle gereksiz dallar kesilir.

**Nerede kullanılır?**
- `HasLineOfSight` — iki nokta arası duvar var mı?
- `ComputeFieldOfView` — ışın duvarla nerede kesişiyor?
- `PathClearForEntity` — nav çizgesi kenarı geçilebilir mi?

| İşlem | Ortalama | En kötü |
|---|---|---|
| `Build` | **O(n log n)** | O(n²) |
| `SegmentIntersectsNode` (LOS sorgusu) | **O(log n)** | O(n) |

---

### 4. `WaypointGraph` / `GraphNode` / `GraphEdge` — `Pathfinding.cs`

Yürünebilir alanları modelleyen ağırlıklı, yönsüz çizge (graph). Her düğüm haritadaki bir waypoint'i, her kenar iki waypoint arasında engelsiz geçilebilen bir yolu temsil eder.

**Kenar kurma mantığı:** İki düğüm arasındaki yol `PathClearForEntity` ile kontrol edilir. Bu fonksiyon BSP'yi kullanarak LOS'u sorgulamanın yanı sıra yol boyunca 6 ara noktada da entity yarıçapı (10 px) kadar bir mesafe boş mu diye kontrol eder. `GraphPathRadius = 12f` olarak ayarlanmıştır — entity yarıçapından biraz büyük tutularak dar geçitler otomatik olarak nav çizgesinden dışlanır; bu sayede A*'ın fiziksel olarak geçilemeyen yolları planlaması önlenir.

| İşlem | Zaman Karmaşıklığı |
|---|---|
| `AddNode` | **O(1)** |
| `BuildEdges` (tüm çift kombinasyonları) | **O(n² · log n)** |
| `GetNeighbors` | **O(1)** |

---

### 5. `AStarPathfinder` — `Pathfinding.cs`

Standart A* algoritması, `MinHeap<AStarHeapNode>` üzerinde çalışır. Sezgi fonksiyonu (heuristic) olarak **Öklid mesafesi** kullanılır; bu fonksiyon hem kabul edilebilir (admissible) hem de tutarlıdır (consistent), dolayısıyla bulunan yol her zaman en kısasıdır.

**Optimizasyonlar:**
- Başlangıç ile hedef arasında doğrudan LOS varsa A* hiç çalıştırılmaz → **O(log n) erken çıkış**
- `FindNearestReachableNode`: Başlangıç pozisyonuna en yakın, LOS açık düğümü bulur; yoksa salt en yakın düğüme geri döner (fallback)
- `FindNearestConnectedNode`: Hedef seçiminde izole (kenar-sız) waypoint'leri atlayarak A*'ın çıkmaz sokağa girmesini önler

| İşlem | Zaman Karmaşıklığı |
|---|---|
| `FindPathBetweenNodes` | **O((V + E) log V)** |
| `FindPathToPosition` | **O((V + E) log V + n)** |
| `ReconstructPath` | **O(V)** |

---

## Algoritmalar

### Görüş Çizgisi — `Geometry.HasLineOfSight`

İki nokta arasında bir segment oluşturulur ve `BspTree.SegmentIntersectsNode` ile bu segmentin herhangi bir duvarı kesip kesmediği sorgulanır. BSP ağacı sayesinde tüm duvarlar dolaşılmak yerine yalnızca o bölgedeki duvarlar kontrol edilir. Düşmanın oyuncuyu "görmesi" bu fonksiyonun `true` döndürmesiyle belirlenir.

---

### Görüş Konisi (FOV) — `Geometry.ComputeFieldOfView`

Düşmanın önünde dinamik, duvar kısıtlı bir görüş konisi poligonu oluşturulur. İşlem adımları:

1. Düşmanın baktığı yönden başlayarak görüş açısı (±28°–38°) **68 eşit parçaya bölünür**.
2. Her parça için bir ışın (ray) oluşturulur ve `RaySegmentIntersect` ile en yakın duvar kesişimi bulunur.
3. Kesişim noktaları `DynamicArray<Vector2D>` içine toplanır ve kapalı bir poligon oluşturulur.
4. Oyuncunun bu poligon içinde olup olmadığı `IsPointInFovPolygon` → `PointInTriangle` fan-testi ile kontrol edilir.

Sonuç: Düşmanın arkasındaki veya duvarın ötesindeki alanlar karanlık kalır; ışığın gerçekçi yayıldığı bir görüş efekti elde edilir.

---

### Çarpışma Tespiti — `Geometry.CircleHitsWalls` + `PatrolSystem.MoveTowardTarget`

Oyuncu ve düşmanların içinden geçmemesi için **swept-circle (süpürülen daire)** yöntemi kullanılır: hareket eden dairenin yarıçapı ile duvar segmenti arasındaki mesafe hesaplanır; mesafe yarıçaptan küçükse çarpışma vardır.

Çarpışma sonrası **8 yönlü kaydırma (sliding)** mekanizması devreye girer:
- 0° (asıl yön) denenır
- Başarısızsa ±30°, ±60°, ±90°, ±120° açılarında alternatif yönler sırayla denenir
- İlk geçen açı uygulanır

Bu sayede entity'ler duvara çarpınca durmaz; duvar yüzeyinde kayarak hareket etmeye devam eder.

---

### Devriye Sistemi — `GameForm.cs` + `PatrolSystem.cs`

Her düşmana `Levels.cs` dosyasında önceden belirlenmiş, duvarlardan uzak **sabit waypoint rotaları** tanımlanmıştır. Rotalar çakışmaz; her düşmanın kendine özgü güzergahı vardır.

Hareket akışı:

```
[Hedef waypoint] → AStarPathfinder.FindPathToPosition()
    → Nav çizgesi üzerinde engel-dolaşan yol
        → PatrolSystem.MoveTowardTarget() ile adım adım ilerleme
            → Waypoint'e varılınca sıradaki waypoint'e geçiş
```

**Takılma kurtarma mekanizması:**

Bir düşman 60 kare boyunca (≈1 saniye) hareket edemezse yolunu yeniden planlar. 120 kare (≈2 saniye) boyunca hareket yoksa bir sonraki patrol noktasına atlayarak rotasına devam eder. Bu iki aşamalı sistem hem performansı korur (sürekli A* çağırmaz) hem de düşmanın kalıcı olarak takılıp kalmasını önler.

---

## Seviye Sistemi

Proje, artan zorlukta tasarlanmış üç farklı seviye içerir:

| Seviye | Ad | Düşman Sayısı | Harita Özellikleri | Düşman Hızı | FOV Açısı | FOV Menzili |
|---|---|---|---|---|---|---|
| **1** | Koridor Labirenti | 2 | Kapalı merkez ada, yatay/dikey siperler, dar geçitler | 2.3 px/kare | ±28° | 240 px |
| **2** | Çapraz Odalar | 3 | Merkez dikey kolon, köşeli L-siperler, yatay bariyer | 2.8 px/kare | ±33° | 265 px |
| **3** | Kale | 4 | Üç kare ada, dar kanal geçitleri, dört bağımsız devriye | 3.3 px/kare | ±38° | 290 px |

**Artan zorluk mantığı:** Her seviyede düşmanlar hem daha hızlı hem daha geniş ve uzak görür. Harita tasarımları da karmaşıklaşır; oyuncunun saklanabileceği alan daralır.

---

## Oyun Kontrolleri ve Mekanikler

| Tuş | İşlev |
|---|---|
| `W` `A` `S` `D` | Oyuncuyu hareket ettir (8 yönlü) |
| `F1` | A* yol çizgilerini göster / gizle (debug görünümü) |
| `R` | Mevcut seviyeyi baştan başlat |

**Oyun durumları:**

- **Oynuyor:** Oyuncu hareket eder, düşmanlar devriye atar
- **Yakalandı:** Oyuncu bir düşmanın FOV poligonuna girerse kırmızı overlay gösterilir, oyun durur
- **Seviye Tamamlandı:** Oyuncu çıkış noktasına ulaşırsa animasyonlu geçiş ekranı açılır
- **Oyun Kazanıldı:** 3. seviye tamamlandıktan sonra kazanma ekranı gösterilir

---

## Proje Dosya Yapısı

```
DataStructures_SVS/
│
├── Program.cs
│     Uygulamanın giriş noktası. GameForm penceresini oluşturur ve başlatır.
│
├── CoreTypes.cs
│     Tüm projenin temel geometrik tipleri: Vector2D (2D vektör, nokta, yön),
│     Segment (iki nokta arası duvar parçası). Matematiksel yardımcı metodlar burada.
│
├── DataStructures.cs
│     Sıfırdan yazılmış veri yapıları:
│       - DynamicArray<T>   → C# List<T> yerine kullanılan dinamik dizi
│       - MinHeap<T>        → A* açık kümesi için ikili minimum yığın
│       - BspTree / BspNode → Uzamsal bölümleme ağacı (LOS ve FOV için)
│
├── Geometry.cs
│     Tüm geometrik algoritmalar:
│       - HasLineOfSight()       → BSP üzerinden görüş çizgisi sorgusu
│       - ComputeFieldOfView()   → Raycasting ile duvar kısıtlı FOV poligonu
│       - CircleHitsWalls()      → Swept-circle çarpışma tespiti
│       - PathClearForEntity()   → Nav çizgesi kenar doğrulama
│       - IsPointInFovPolygon()  → Oyuncu yakalama testi
│
├── Pathfinding.cs
│     Navigasyon ve yol bulma:
│       - WaypointGraph          → Waypoint düğümleri ve geçilebilir kenarlar
│       - GraphNode / GraphEdge  → Çizge elemanları
│       - AStarPathfinder        → MinHeap tabanlı A* uygulaması
│
├── PatrolSystem.cs
│     Düşman hareketi altyapısı:
│       - LevelNavigationGraph   → Seviyeye ait waypoint pozisyonları + çizge
│       - PatrolSystem           → MoveTowardTarget (8-yön sliding hareketi)
│       - FindNearestReachableNode / FindNearestConnectedNode → A* başlangıç/hedef seçimi
│
├── Levels.cs
│     Seviye tanımları ve düşman yapılandırması:
│       - LevelDefinition        → Duvarlar, waypoint'ler, oyuncu başlangıcı, çıkış
│       - EnemySpawn             → Düşman başlangıç konumu, rengi, açısı, patrol rotası
│       - LevelManager           → GetLevel(1/2/3) fabrika metodu
│
├── GameForm.cs
│     Ana oyun mantığı:
│       - LoadLevel()            → Seviyeyi yükle, nav çizgesini inşa et
│       - UpdateEnemyPatrol()    → Düşman AI adımı (A*, hareket, stuck kurtarma)
│       - CheckGameConditions()  → FOV ile oyuncu tespiti, çıkış kontrolü
│       - OnPaint()              → Tüm render: harita, düşmanlar, FOV, HUD, overlay
│       - OnKeyDown()            → Input işleme
│
├── GameForm.Designer.cs
│     Visual Studio tarafından yönetilen WinForms tasarımcı kodu.
│
├── Dockerfile
│     2 aşamalı (multi-stage) Windows Container yapısı:
│       - Aşama 1 (build)   → mcr.microsoft.com/dotnet/framework/sdk:3.5
│                              MSBuild ile projeyi derler, Release .exe üretir
│       - Aşama 2 (runtime) → mcr.microsoft.com/dotnet/framework/runtime:3.5
│                              Yalnızca çalışma zamanını içerir; SDK araçları son
│                              imaja dahil edilmez — imaj boyutu küçülür
│
├── docker-compose.yml
│     Tek komutla (docker-compose up --build) derle-ve-çalıştır servisi.
│       - Servis adı   : game  (container: svs_game)
│       - Platform     : windows/amd64 (Windows Container zorunlu)
│       - Genişletme   : ileride backend / frontend / ai-service eklenmesine
│                        hazır yorum taslağı içerir
│
├── .dockerignore
│     Build context'ten dışlanan dosyalar:
│       - bin/ obj/ .vs/        → Derleme çıktıları ve IDE geçici dosyaları
│       - README.md RAPOR.*     → Dokümantasyon (imaj içinde gereksiz)
│       - *.pdf *.ps1 *.tmp     → Geçici ve belge dosyaları
│
└── README.md
      Bu dosya.
```

---

## Kurulum ve Çalıştırma

### Gereksinimler

- **İşletim Sistemi:** Windows 10 veya Windows 11
- **.NET Framework:** 3.5 veya üstü (Windows ile birlikte kurulu gelir, ek yükleme gerekmez)
- **IDE (isteğe bağlı):** Visual Studio 2022 — yalnızca derleme yapılacaksa MSBuild tek başına yeterlidir

### Visual Studio 2022 ile Çalıştırma

```
1. Visual Studio 2022'yi aç
2. "Dosya → Aç → Proje/Çözüm" menüsünden DataStructures_SVS.csproj dosyasını seç
3. F5 tuşuna bas (Debug modu) veya Ctrl+F5 (Debug olmadan çalıştır)
```

### MSBuild Komut Satırı ile Derleme

```bat
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ^
    DataStructures_SVS.csproj /p:Configuration=Release

bin\Release\DataStructures_SVS.exe
```

> **Docker Notu:** Proje .NET Framework 3.5 / WinForms tabanlıdır. WinForms uygulamaları
> Windows GDI+ grafik sürücüsüne bağımlı olduğundan **Linux** tabanlı Docker konteynerlerinde
> çalışmaz; **Windows Containers** kullanılması gerekir.
> Docker Desktop'ta `System Tray → Docker → Switch to Windows containers` seçeneğini etkinleştirin.

---

## Docker ile Çalıştırma

Proje, `Dockerfile` ve `docker-compose.yml` dosyalarıyla tüm bağımlılıklar dahil
**tek komutla derlenip çalıştırılabilir** hale getirilmiştir.

### Ön Koşul

Docker Desktop'ı **Windows Containers** moduna alın:

```
System Tray → Docker simgesi (sağ tık) → Switch to Windows containers
```

### Tek Komutla Derle ve Çalıştır

```bat
docker-compose up --build
```

Bu komut sırasıyla şunları yapar:

1. `mcr.microsoft.com/dotnet/framework/sdk:3.5` imajını kullanarak projeyi **MSBuild ile derler**
2. Derleme araçlarını bırakıp yalnızca runtime içeren `mcr.microsoft.com/dotnet/framework/runtime:3.5` imajına geçer (multi-stage build)
3. `datastructures-svs:latest` imajını oluşturur
4. `svs_game` adlı container'ı başlatır ve oyunu çalıştırır

### Yalnızca Derleme (çalıştırmadan)

```bat
docker-compose build
```

### Manuel Derleme ve Çalıştırma

```bat
REM İmajı oluştur
docker build -t datastructures-svs .

REM Container'ı başlat
docker run -it datastructures-svs
```

### Durdurma ve Temizleme

```bat
docker-compose down
```

### Docker Dosya Yapısı

| Dosya | Açıklama |
|---|---|
| `Dockerfile` | 2 aşamalı Windows Container yapısı: build (SDK) → runtime |
| `docker-compose.yml` | Tek komutla derle-çalıştır servisi; ileride servis genişlemesine hazır taslak |
| `.dockerignore` | `bin/`, `obj/`, `.vs/`, belgeler ve geçici dosyalar build context'ten dışlanır |

---

## Zaman Karmaşıklığı Özeti

| Yapı / Algoritma | İşlem | Karmaşıklık | Neden Önemli? |
|---|---|---|---|
| `DynamicArray<T>` | Add (amortize) / Get | **O(1)** | Sık ekleme ve okuma işlemleri maliyetsiz |
| `DynamicArray<T>` | RemoveAt | **O(n)** | Sıralı hafıza; kayma gerektirir |
| `MinHeap<T>` | Push / Pop | **O(log n)** | A* açık kümesini verimli yönetir |
| `BspTree` | Build | **O(n log n)** ort. | Seviye yüklenirken tek seferlik maliyet |
| `BspTree` | LOS / Kesişim sorgusu | **O(log n)** ort. | Her karede onlarca kez çağrılır; hız kritik |
| `WaypointGraph` | BuildEdges | **O(n² · log n)** | Yine tek seferlik; runtime'da değil |
| `A*` | FindPath | **O((V+E) log V)** | LOS yoksa çalışır; LOS varsa O(log n) erken çıkış |
| Raycasting (68 ışın) | ComputeFieldOfView | **O(R · log n)** | Her düşman her karede; BSP'nin hızı kritik |
| Çarpışma | CircleHitsWalls | **O(n)** | Tüm duvar listesi taranır; n seviye başına sabit |

---

## Git İş Akışı

```
Feature Branch  →  Commit  →  Pull Request  →  Review  →  Merge (main)
```

Her ekip üyesi kendi özellik branch'inde çalışır ve işi bittiğinde Pull Request (PR) açar. `main` dalına doğrudan kod gönderilmez. PR açıklamasında mutlaka şunlar yer alır: ad soyad, öğrenci numarası, değiştirilen dosyalar ve yapılan değişikliğin özeti.

| Üye | Branch |
|---|---|
| Oguz Eren | `feature/oguz-ui` |
| Zeynep Sude Kalkan | `feature/zeynep-bsp` |
| Baris Kabacaoglu | `feature/baris-astar` |
| Berat CAKIR | `feature/berat-core` |

**Branch güncelleme kuralı:** Merge sonrası tüm branch'ler `main` ile senkronize edilir:

```bash
git checkout feature/oguz-ui
git merge main
```
