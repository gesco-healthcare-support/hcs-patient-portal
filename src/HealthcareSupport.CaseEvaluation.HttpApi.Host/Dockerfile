# Single multi-stage Dockerfile for the HttpApi.Host.
#   prod (DEFAULT): publish -> aspnet runtime image (~0.6 GB). CI / deploy.
#   dev:            SDK image; source is bind-mounted at runtime and NuGet lives
#                   in a named volume, so editing C# means
#                   `docker compose restart api` (no image rebuild). No
#                   `dotnet watch` -- Docker Desktop's bind-mount/inotify bridge
#                   on Windows drops events (OBS-22; GH #8694/#15171).

# ---- build (sdk): restore + publish; feeds the prod stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG ABP_NUGET_API_KEY
WORKDIR /src

COPY NuGet.Config.template .
RUN sed "s/\${ABP_NUGET_API_KEY}/$ABP_NUGET_API_KEY/" NuGet.Config.template > NuGet.Config

COPY Directory.Build.props .
COPY BannedSymbols.txt .
COPY HealthcareSupport.CaseEvaluation.slnx .
COPY src/ src/
COPY test/ test/

# Placeholder secrets files excluded by .dockerignore (ABP .csproj references them)
RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.DbMigrator/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/appsettings.secrets.json

RUN dotnet restore HealthcareSupport.CaseEvaluation.slnx
RUN dotnet publish src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj \
    -c Release -o /app/publish --no-restore

# ---- dev (sdk): bind-mounted source, restore + run on container start ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
ARG ABP_NUGET_API_KEY
WORKDIR /app

COPY NuGet.Config.template .
RUN sed "s/\${ABP_NUGET_API_KEY}/$ABP_NUGET_API_KEY/" NuGet.Config.template > NuGet.Config

COPY Directory.Build.props .
COPY BannedSymbols.txt .
COPY HealthcareSupport.CaseEvaluation.slnx .
COPY src/ src/
COPY test/ test/

RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.DbMigrator/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/appsettings.secrets.json

# Warm the image cache so the first container start is fast; the entrypoint
# re-restores from the bind-mounted source to pick up any csproj changes.
RUN dotnet restore HealthcareSupport.CaseEvaluation.slnx
RUN dotnet build src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj \
    -c Debug --no-restore

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Wipe restore artifacts that may encode Windows host paths (the bind-mounted
# host obj/ references C:\... NuGet fallback folders and breaks Linux restore
# with MSB4018). Then restore + run from the bind-mounted source.
ENTRYPOINT ["sh", "-c", "\
  find src -name 'project.assets.json' -delete 2>/dev/null; \
  find src -name '*.nuget.g.props' -delete 2>/dev/null; \
  find src -name '*.nuget.g.targets' -delete 2>/dev/null; \
  dotnet restore HealthcareSupport.CaseEvaluation.slnx >/tmp/restore.log 2>&1 || (cat /tmp/restore.log; exit 1); \
  exec dotnet run --no-launch-profile --no-restore --project src/HealthcareSupport.CaseEvaluation.HttpApi.Host/HealthcareSupport.CaseEvaluation.HttpApi.Host.csproj --urls http://+:8080"]

# ---- prod (DEFAULT): small aspnet runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS prod
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

# Do NOT add empty-stub source dirs: ABP's ReplaceEmbeddedByPhysical fully
# replaces (not merges) the embedded localization fileset when the physical path
# exists. The HttpApiHost module guards each replace with Directory.Exists, so
# missing host source dirs cleanly fall back to embedded resources.
ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.HttpApi.Host.dll"]
