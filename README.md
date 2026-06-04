# Veri Yapıları ile BSP Ağacı Tabanlı Görüş Alanı ve Çarpışma Tespiti

Bu proje, C# ile geliştirilmiş 2D (top-down) bir gizlilik oyunu simülasyonudur.
Oyuncu engeller arasında çıkış noktasına ulaşmaya çalışırken, BSP ağacı tabanlı
raycasting görüş sistemi ve A* pathfinding ile donatılmış düşman devriyeleri
oyuncuyu yakalamaya çalışır.

**GitHub Repo:** https://github.com/cakirberat/data-structures-SVS

---

## Proje Amacı

- Oyun haritasını uygun veri yapılarıyla modellemek
- Görüş çizgisi ve görüş konisi hesaplamalarını BSP ile verimli yapmak
- Duvar çarpışma kontrolünü daire tabanlı (swept-circle) güvenilir şekilde yapmak
- Düşman hareketi için A* tabanlı yol bulma kullanmak
- Üç farklı seviyede artan zorluk sistemi sunmak

---

## Faz 1 — Zorunlu Veri Yapıları

Aşağıdaki veri yapıları sıfırdan implemente edilmiştir:

| Yapı | Dosya | Açıklama |
|---|---|---|
| `BspTree` / `BspNode` | `DataStructures.cs` | Duvar segmentlerini uzamsal olarak böler; LOS/çarpışma sorgularında adayları daraltır |
| `WaypointGraph` | `Pathfinding.cs` | Yürünebilir noktalar ve geçilebilir bağlantıları tutar |
| `MinHeap<T>` | `DataStructures.cs` | A* açık kümesinde en düşük f-maliyetli düğümü O(log n)'de seçer |
| `DynamicArray<T>` | `DataStructures.cs` | Duvarlar, FOV poligon noktaları ve komşuluk listeleri için dinamik dizi |

---

## Faz 2 — Zorunlu Algoritmalar

| Algoritma | Dosya | Açıklama |
|---|---|---|
| Line of Sight | `Geometry.cs` | Düşman ile oyuncu arasında duvar kesişimi `BspTree` ile sorgulanır |
| Raycasting / FOV | `Geometry.cs` | 68 ışın ile duvar kısıtlı görüş konisi poligonu oluşturulur |
| A* Pathfinding | `Pathfinding.cs` | `MinHeap` + `WaypointGraph` üzerinde engel dolaşan yol bulma |
| Çarpışma | `Geometry.cs` | Swept-circle + 8-yön sliding ile duvarlardan geçilmesi önlenir |

---

## Faz 3 — Arayüz

Proje WinForms tabanlı 2D arayüz ile çalışmaktadır:

- Kuşbakışı harita çizimi (duvarlar yuvarlak uçlu `LineCap.Round`)
- Düşman görüş konisi (raycasting ile oluşturulan dolgu poligonu, duvar arkası karanlık)
- Oyuncu yön göstergesi (hareket yönüne dönen ok)
- Düşman FOV'una girildiğinde kırmızı halo uyarısı
- Çıkış noktası animasyonlu nabız + EXIT etiketi
- HUD: seviye bilgisi + F1 kısayol ipucu
- Oyun sonu overlay (kazanma / kaybetme / seviye geçişi)

---

## Seviye Sistemi

| Seviye | Ad | Düşman | Düşman Hızı | FOV Açısı | FOV Menzili |
|---|---|---|---|---|---|
| 1 | Koridor Labirenti | 2 | 2.3 px/kare | ±28° | 240 px |
| 2 | Çapraz Odalar | 3 | 2.8 px/kare | ±33° | 265 px |
| 3 | Kale | 4 | 3.3 px/kare | ±38° | 290 px |

---

## Oyun Davranışı

- Oyuncu `W` `A` `S` `D` ile hareket eder
- Oyuncu duvarlardan geçemez (daire tabanlı çarpışma + sliding)
- Düşmanlar sabit waypoint rotaları üzerinde A* ile devriye atar
- Her düşmanın kendine özgü rotası vardır; rotalar çakışmaz
- Oyuncu bir düşmanın FOV poligonuna girerse yakalanır
- Oyuncu çıkış noktasına ulaşırsa seviye tamamlanır, 3. seviyeden sonra oyun kazanılır
- `F1` A* yol çizgilerini göster / gizle
- `R` oyunu mevcut seviyeden sıfırlar

---

## Dosya Yapısı

```
DataStructures_SVS/
├── Program.cs               — Giriş noktası (Main)
├── CoreTypes.cs             — Vector2D, Segment
├── DataStructures.cs        — DynamicArray<T>, MinHeap<T>, BspTree, BspNode
├── Geometry.cs              — BSP sorgulama, LOS, raycasting, FOV, çarpışma
├── Pathfinding.cs           — WaypointGraph, GraphEdge, AStarPathfinder
├── PatrolSystem.cs          — LevelNavigationGraph, PatrolSystem (8-yön sliding)
├── Levels.cs                — LevelDefinition, EnemySpawn, LevelManager (3 seviye)
├── GameForm.cs              — Oyun döngüsü, AI mantığı, render, input
├── GameForm.Designer.cs     — WinForms tasarımcı kodu
└── README.md                — Bu dosya
```

---

## Kurulum ve Çalıştırma

### Gereksinimler

- Windows 10 / 11
- .NET Framework 3.5 veya üstü (Windows ile birlikte gelir)
- Visual Studio 2022 (isteğe bağlı; MSBuild tek başına yeterlidir)

### Visual Studio 2022 ile

```
1. DataStructures_SVS.csproj dosyasını aç
2. F5 (Debug) veya Ctrl+F5 (çalıştır) ile başlat
```

### MSBuild CLI ile

```bat
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" ^
    DataStructures_SVS.csproj /p:Configuration=Release
bin\Release\DataStructures_SVS.exe
```

> **Not:** Proje .NET Framework 3.5 / WinForms tabanlıdır. WinForms uygulamaları
> Windows GDI+ sürücüsüne bağımlı olduğundan Linux Docker konteynerlerinde çalışmaz.

---

## Zaman Karmaşıklığı Özeti

| Yapı / Algoritma | İşlem | Karmaşıklık |
|---|---|---|
| `DynamicArray<T>` | Add (amortize) / Get | O(1) |
| `DynamicArray<T>` | RemoveAt | O(n) |
| `MinHeap<T>` | Push / Pop | O(log n) |
| `BspTree` | Build | O(n log n) ort. |
| `BspTree` | LOS / kesişim sorgusu | O(log n) ort. |
| `WaypointGraph` | BuildEdges | O(n² · log n) |
| `A*` | FindPath | O((V+E) log V) |
| Raycasting (68 ışın) | ComputeFieldOfView | O(R · log n) |
| Çarpışma | CircleHitsWalls | O(n) |

---

## Ekip ve Modül Dağılımı

| Ad Soyad | No | Dosyalar | Görev |
|---|---|---|---|
| Oguz Eren | 032290038 | `GameForm.cs`, `GameForm.Designer.cs` | UI, render, input, oyun döngüsü, HUD, overlay |
| Zeynep Sude Kalkan | 032290056 | `Geometry.cs`, `DataStructures.cs` (BspTree) | BSP, LOS, raycasting, FOV, çarpışma geometrisi |
| Baris Kabacaoglu | 032290027 | `Pathfinding.cs`, `PatrolSystem.cs` | WaypointGraph, A*, LevelNavigationGraph, devriye |
| Berat CAKIR | 032290054 | `Program.cs`, `CoreTypes.cs`, `DataStructures.cs` (DynArray/MinHeap), `Levels.cs`, `README.md` | Çekirdek tipler, veri yapıları, seviye tanımları, dokümantasyon |

---

## Git İş Akışı

```
Feature Branch → Commit → Pull Request → Review → Merge (main)
```

| Üye | Branch |
|---|---|
| Oguz Eren | `feature/oguz-ui` |
| Zeynep Sude Kalkan | `feature/zeynep-bsp` |
| Baris Kabacaoglu | `feature/baris-astar` |
| Berat CAKIR | `feature/berat-core` |

- `main` dalına doğrudan kod gönderilmez
- PR açıklamasında: ad soyad, öğrenci no, değiştirilen dosyalar, yapılan değişiklik
- Merge sonrası branch `main` ile güncellenir: `git merge main`
