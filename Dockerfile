# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first for layer-cached restores
COPY ["src/YoutubeDownload.Domain/YoutubeDownload.Domain.csproj", "src/YoutubeDownload.Domain/"]
COPY ["src/YoutubeDownload.Application/YoutubeDownload.Application.csproj", "src/YoutubeDownload.Application/"]
COPY ["src/YoutubeDownload.Infrastructure/YoutubeDownload.Infrastructure.csproj", "src/YoutubeDownload.Infrastructure/"]
COPY ["src/YoutubeDownload.Api/YoutubeDownload.Api.csproj", "src/YoutubeDownload.Api/"]
RUN dotnet restore "src/YoutubeDownload.Api/YoutubeDownload.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/YoutubeDownload.Api/YoutubeDownload.Api.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# yt-dlp for the real YtDlp provider (ffmpeg merged into yt-dlp package via apt)
RUN apt-get update \
    && apt-get install -y --no-install-recommends yt-dlp ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# Run as the built-in non-root 'app' user that ships with the .NET images
RUN mkdir -p /app/App_Data/jobs && chown -R app:app /app/App_Data
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build --chown=app:app /app/publish .

USER app

EXPOSE 8080

ENTRYPOINT ["dotnet", "YoutubeDownload.Api.dll"]
