# 🎮 Veri Yapıları ile BSP Ağacı Tabanlı Görüş Alanı ve Çarpışma Tespiti

Bu proje, C# ile geliştirilmiş 2D (top-down) bir gizlilik oyunu simülasyonudur. Oyuncu engeller arasında hedefe ulaşmaya çalışırken, düşman raycasting tabanlı görüş sistemi ile oyuncuyu yakalamaya çalışır.

## 🎯 Proje Amacı

- Oyun haritasını uygun veri yapılarıyla modellemek 🗺️
- Görüş çizgisi ve görüş konisi hesaplamalarını verimli yapmak 👁️
- Duvar çarpışma kontrolünü güvenilir şekilde yapmak 🧱
- Düşman hareketi için A* tabanlı yol bulma kullanmak 🤖

## 🏗️ Faz 1 - Zorunlu Veri Yapıları

Bu projede aşağıdaki veri yapıları sıfırdan implemente edilmiştir:

- **BSP Tree** (`BspTree`, `BspNode`): Duvar segmentlerini uzamsal olarak böler, görüş/çarpışma sorgularında adayları daraltır. 🌳
- **Graph** (`WaypointGraph`): Yürünebilir noktalar arası bağlantıları tutar. 🕸️
- **Min-Heap** (`MinHeap<T>`): A* açık kümesinde en düşük maliyetli düğümü seçer. 🔢
- **Dynamic Array** (`DynamicArray<T>`): Duvarlar, ışın sonuçları ve komşuluk listeleri için kullanılır. 📋

## ⚙️ Faz 2 - Zorunlu Algoritmalar

- **Line of Sight**: Düşman ve oyuncu arasında duvar kesişimi var mı kontrol edilir. 📏
- **Raycasting / Intersection**: Düşmanın FOV konisindeki ışınların duvarlarla kesişim noktası hesaplanır. 🔦
- **A* Pathfinding**: Düşmanın engelleri dolaşarak hedefe ulaşması için kullanılır. 📍

## 🖥️ Faz 3 - Arayüz Gereksinimleri

Proje, WinForms tabanlı 2D bir arayüz ile geliştirilmiştir:

- Kuşbakışı harita çizimi 🗺️
- Duvarların ve yürünebilir graph'ın gösterimi 🧶
- Düşman görüş konisi (ray + dolgulu fan) gösterimi 📐
- Oyuncu/düşman hareketi ve durum paneli 👤
- Canlı metrik paneli (ray sayısı, path süresi, frame süresi) 📊

## 🕹️ Oyun Davranışı

- Oyuncu `W`, `A`, `S`, `D` ile hareket eder. ⌨️
- Oyuncu duvarlardan geçemez (segment kesişim tabanlı çarpışma kontrolü). 🛑
- Düşman devriye modunda rotalar arasında gezer. 🔄
- Oyuncu görüş konisine ve LOS'e girerse düşman takip moduna geçer. ⚠️
- Oyuncu hedefe ulaşırsa kazanır; görülürse yakalanır. 🏆
- `R` tuşu oyunu sıfırlar. 🔄

## 📂 Dosya Yapısı

- `New Proje/StealthVisionSystem/Program.cs`: Uygulama girişi, harita ve graph kurulumları
- `New Proje/StealthVisionSystem/CoreTypes.cs`: Temel tipler (`Vector2`, `WallSegment`, `RayHit`, `Enemy`)
- `New Proje/StealthVisionSystem/DataStructures.cs`: `DynamicArray<T>`, `MinHeap<T>`
- `New Proje/StealthVisionSystem/SpatialAlgorithms.cs`: BSP, LOS, raycasting, collision, geometri
- `New Proje/StealthVisionSystem/Pathfinding.cs`: `WaypointGraph`, `AStarPathfinder`
- `New Proje/StealthVisionSystem/GameForm.cs`: Arayüz, çizim, oyun döngüsü ve AI modları

## 🚀 Kurulum ve Çalıştırma

### 🛠️ Gereksinimler

- .NET 9 SDK
- Visual Studio 2022 (tercihen .NET desktop development workload ile)

### 💻 Terminal ile

bash
- `cd "New Proje/StealthVisionSystem"`
- `dotnet restore`
- `dotnet run`

### 🛠️ Visual Studio 2022 ile

- `StealthVisionSystem.csproj` dosyasını aç.

- StealthVisionSystem için Set as Startup Project yap.

- Ctrl + F5 ile çalıştır.

## ⏳ Zaman Karmaşıklığı Özeti

- **BSP Query:** Ortalama durumda tüm duvarları gezmeden aday duvar listesini çıkarır.

- **Raycasting:** R ışın için yaklaşık O(R * C) (C: BSP'den gelen aday duvar sayısı).

- **A*:** Graph üzerinde standart olarak O((V + E) log V) (heap kullanımına bağlı).

- **Collision:** Segment kesişim kontrolleri ile aday duvarlar üzerinden çalışır.

## 👥 Ekip Bilgileri

- **Oğuz Eren - 032290038**

- **Zeynep Sude Kalkan - 032290056**

- **Barış Kabacaoğlu - 032290027**

- **Berat Çakır - 032290054**

## 🛠️ Ekip İçindeki Modül Dağılımı

- **Oğuz Eren:** GameForm.cs (arayüz, oyun döngüsü, panel)

- **Zeynep Sude Kalkan:** SpatialAlgorithms.cs (BSP, LOS, raycasting, çarpışma)

- **Barış Kabacaoğlu:** Pathfinding.cs (graph ve A* pathfinding)

- **Berat Çakır:** Program.cs, CoreTypes.cs, DataStructures.cs (çekirdek ve veri yapıları)