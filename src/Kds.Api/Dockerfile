# LST-KDS API — çok aşamalı imaj. Build bağlamı REPO KÖKÜ olmalı (proje referansları için).
# Kullanım (compose): context: ../.., dockerfile: src/Kds.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /kaynak

# Önce derleme ayarları + tüm kaynak → restore + publish (yalnız API'nin bağımlılık grafiği).
COPY Directory.Build.props nuget.config ./
COPY src/ src/
RUN dotnet restore src/Kds.Api/Kds.Api.csproj
RUN dotnet publish src/Kds.Api/Kds.Api.csproj -c Release -o /yayin --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# PDF dışa aktarım (S13): SistemFontCozucu OS'ten TrueType font okur. Debian tabanlı imaj →
# DejaVu (Türkçe glif kapsamı tam). Font paketi olmazsa PDF üretimi net hata verir.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /yayin ./
ENV ASPNETCORE_URLS=http://+:5210
EXPOSE 5210
ENTRYPOINT ["dotnet", "Kds.Api.dll"]
