# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first for better layer caching.
COPY AI.TeachingAssistant.sln ./
COPY src/AI.TeachingAssistant.Domain/AI.TeachingAssistant.Domain.csproj src/AI.TeachingAssistant.Domain/
COPY src/AI.TeachingAssistant.Application/AI.TeachingAssistant.Application.csproj src/AI.TeachingAssistant.Application/
COPY src/AI.TeachingAssistant.Infrastructure/AI.TeachingAssistant.Infrastructure.csproj src/AI.TeachingAssistant.Infrastructure/
COPY src/AI.TeachingAssistant.Api/AI.TeachingAssistant.Api.csproj src/AI.TeachingAssistant.Api/
COPY src/AI.TeachingAssistant.Web/AI.TeachingAssistant.Web.csproj src/AI.TeachingAssistant.Web/
COPY src/AI.TeachingAssistant.Tests/AI.TeachingAssistant.Tests.csproj src/AI.TeachingAssistant.Tests/

RUN dotnet restore AI.TeachingAssistant.sln

# Copy the rest of the source.
COPY . .

# Publish the single-process host (Web serves UI + API).
RUN dotnet publish src/AI.TeachingAssistant.Web/AI.TeachingAssistant.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Persistent-data root. On Liara mount a Disk at /app/data.
#   SQLite : /app/data/madreseai.db              (ConnectionStrings__Default)
#   Media  : /app/data/generated-images          (AI_IMAGE_STORAGE_ROOT)
RUN mkdir -p /app/data/generated-images

COPY --from=build /app/publish .

# Listen on port 80 (Liara default target).
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Persist SQLite + generated images on the mounted disk (override anytime in dashboard).
ENV ConnectionStrings__Default="Data Source=/app/data/madreseai.db"
ENV AI_IMAGE_STORAGE_ROOT=/app/data/generated-images

EXPOSE 80

ENTRYPOINT ["dotnet", "AI.TeachingAssistant.Web.dll"]
