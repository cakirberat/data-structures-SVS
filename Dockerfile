# ============================================================
# DataStructures_SVS — Windows Container Dockerfile
# .NET Framework 3.5 / WinForms tabanlı masaüstü uygulaması
#
# GEREKSINIM: Docker Desktop'ın "Windows Containers" modunda
#             çalışıyor olması gerekir.
#             (System Tray → Docker → Switch to Windows containers)
#
# KULLANIM:
#   docker build -t datastructures-svs .
#   docker-compose up --build
# ============================================================

# ──────────────────────────────────────────────────────────────
# Aşama 1 — BUILD
# MSBuild + .NET Framework 3.5 SDK içeren resmi Windows imajı.
# Proje bu aşamada derlenir; üretilen .exe ve bağımlılıklar
# /app/publish klasörüne kopyalanır.
# ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/framework/sdk:3.5 AS build

WORKDIR /src

# Bağımlılık katmanı önce kopyalanır; .csproj değişmediği
# sürece NuGet geri yükleme adımı Docker önbelleğinden gelir.
COPY DataStructures_SVS.csproj .

# Kaynak kodu kopyala
COPY . .

# MSBuild ile Release modunda derle
RUN msbuild DataStructures_SVS.csproj \
      /p:Configuration=Release \
      /p:OutputPath=C:\app\publish \
      /p:WarningLevel=0 \
      /nologo \
      /verbosity:minimal

# ──────────────────────────────────────────────────────────────
# Aşama 2 — RUNTIME
# Yalnızca .NET Framework 3.5 runtime içeren daha küçük imaj.
# Derleme araçları (MSBuild, SDK) bu aşamada bulunmaz;
# bu sayede son imaj boyutu önemli ölçüde küçülür.
# ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/framework/runtime:3.5 AS runtime

WORKDIR /app

# Build aşamasından sadece çalıştırılabilir dosyaları al
COPY --from=build C:\app\publish .

# Metadata: port açılmıyor (masaüstü uygulaması)
LABEL maintainer="Grup6-DataStructures-SVS"
LABEL description="BSP tabanlı görüş alanı ve A* devriye sistemi oyun simülasyonu"
LABEL version="1.0"

# WinForms GUI uygulaması; container başlatıldığında oyunu çalıştır.
# NOT: GUI gösterimi için Windows Container ortamında
#      "docker run" sırasında -it bayrağı veya RDP bağlantısı gereklidir.
ENTRYPOINT ["DataStructures_SVS.exe"]
