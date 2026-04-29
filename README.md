<div align="center">
  <h1>Veri Yapıları ile BSP Ağacı Tabanlı Görüş Alanı ve Çarpışma Tespiti</h1>
  <p>
    <b>C# ile geliştirilmiş 2D (top-down) gizlilik oyunu simülasyonu.</b>
  </p>
</div>

<br />

Bu projede oyuncu engeller arasında hedefe ulaşmaya çalışırken, düşman raycasting tabanlı görüş sistemi ile oyuncuyu yakalamaya çalışır.

## 🎯 Proje Amacı

- Oyun haritasını uygun veri yapılarıyla modellemek.
- Görüş çizgisi ve görüş konisi hesaplamalarını verimli yapmak.
- Duvar çarpışma kontrolünü güvenilir şekilde gerçekleştirmek.
- Düşman hareketi için **A*** tabanlı yol bulma (pathfinding) algoritmasını kullanmak.

---

## 🏗️ Faz 1 - Zorunlu Veri Yapıları

Bu projede aşağıdaki veri yapıları sıfırdan implemente edilmiştir:

- **`BSP Tree`** (`BspTree`, `BspNode`): Duvar segmentlerini uzamsal olarak böler, görüş/çarpışma sorgularında adayları daraltır.
- **`Graph`** (`WaypointGraph`): Yürünebilir noktalar arası bağlantıları tutar.
- **`Min-Heap`** (`MinHeap<T>`): A* açık kümesinde en düşük maliyetli düğümü seçer.
- **`Dynamic Array`** (`DynamicArray<T>`): Duvarlar, ışın sonuçları ve komşuluk listeleri için kullanılır.

---

## 🧠 Faz 2 - Zorunlu Algoritmalar

- **`Line of Sight (Görüş Çizgisi)`**: Düşman ve oyuncu arasında duvar kesişimi var mı kontrol edilir.
- **`Raycasting / Intersection`**: Düşmanın FOV (Görüş Alanı) konisindeki ışınların duvarlarla kesişim noktası hesaplanır.
- **`A* Pathfinding`**: Düşmanın engelleri dolaşarak hedefe ulaşması için kullanılır.

---

## 🖥️ Faz 3 - Arayüz Gereksinimleri

Proje, WinForms tabanlı 2D bir arayüz ile geliştirilmiştir:

- 🗺️ Kuşbakışı harita çizimi
- 🧱 Duvarların ve yürünebilir graph'ın gösterimi
- 🔦 Düşman görüş konisi (ışın + dolgulu fan) gösterimi
- 🏃‍♂️ Oyuncu/düşman hareketi ve durum paneli
- 📊 Canlı metrik paneli (ışın sayısı, path süresi, frame süresi)

---

## 🎮 Oyun Davranışı

- **Hareket:** Oyuncu `W`, `A`, `S`, `D` tuşları ile hareket eder.
- **Çarpışma:** Oyuncu duvarlardan geçemez (segment kesişim tabanlı çarpışma kontrolü).
- **Yapay Zeka:** Düşman devriye modunda önceden belirlenmiş rotalar arasında gezer.
- **Takip:** Oyuncu, düşmanın görüş konisine ve LOS'e (Görüş Çizgisi) girerse düşman **takip moduna** geçer.
- **Kazanma/Kaybetme:** Oyuncu hedefe ulaşırsa kazanır; düşman tarafından görülürse yakalanır.
- **Sıfırlama:** `R` tuşu oyunu sıfırlar.

---

## 📂 Dosya Yapısı

```text
📁 New Proje/StealthVisionSystem/
├── 📄 Program.cs           # Uygulama girişi, harita ve graph kurulumları
├── 📄 CoreTypes.cs         # Temel tipler (Vector2, WallSegment, RayHit, Enemy)
├── 📄 DataStructures.cs    # DynamicArray<T>, MinHeap<T>
├── 📄 SpatialAlgorithms.cs # BSP, LOS, raycasting, collision, geometri
├── 📄 Pathfinding.cs       # WaypointGraph, AStarPathfinder
└── 📄 GameForm.cs          # Arayüz, çizim, oyun döngüsü ve AI modları