# Single multi-stage Dockerfile for the AuthServer (OpenIddict + Razor identity UI).
#   prod (DEFAULT): publish -> aspnet runtime (~0.6 GB). CI / deploy.
#   dev:            SDK image; source bind-mounted at runtime, NuGet in a named
#                   volume; entrypoint restores + runs. Iterate with
#                   `docker compose restart authserver`.
#
# AuthServer is special: the Razor login pages need client-side libs (LeptonX,
# jQuery, Bootstrap) under wwwroot/libs, produced by `abp install-libs`. That dir
# is gitignored, so in dev (where source is bind-mounted) the entrypoint must
# regenerate it -- otherwise the login UI loses its theme.

# ---- build (sdk): restore + install-libs + publish; feeds prod ----
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

RUN echo '{}' > src/HealthcareSupport.CaseEvaluation.AuthServer/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.HttpApi.Host/appsettings.secrets.json \
 && echo '{}' > src/HealthcareSupport.CaseEvaluation.DbMigrator/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.TestBase/appsettings.secrets.json \
 && echo '{}' > test/HealthcareSupport.CaseEvaluation.HttpApi.Client.ConsoleTestApp/appsettings.secrets.json

RUN dotnet restore HealthcareSupport.CaseEvaluation.slnx

# Node + ABP CLI to restore client-side libraries (wwwroot/libs is gitignored).
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*
RUN dotnet tool install -g Volo.Abp.Cli --version 10.0.2
ENV PATH="$PATH:/root/.dotnet/tools"
WORKDIR /src/src/HealthcareSupport.CaseEvaluation.AuthServer
RUN abp install-libs
WORKDIR /src

RUN dotnet publish src/HealthcareSupport.CaseEvaluation.AuthServer/HealthcareSupport.CaseEvaluation.AuthServer.csproj \
    -c Release -o /app/publish --no-restore

# ---- dev (sdk): bind-mounted source; install-libs + restore + run on start ----
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

RUN dotnet restore HealthcareSupport.CaseEvaluation.slnx
RUN dotnet build src/HealthcareSupport.CaseEvaluation.AuthServer/HealthcareSupport.CaseEvaluation.AuthServer.csproj \
    -c Debug --no-restore

# Node + ABP CLI baked so the entrypoint can (re)generate wwwroot/libs at runtime.
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*
RUN dotnet tool install -g Volo.Abp.Cli --version 10.0.2
ENV PATH="$PATH:/root/.dotnet/tools"

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# On start: ensure the bind-mounted source has wwwroot/libs (gitignored, so a
# fresh host checkout lacks it); wipe Windows-stale restore artifacts; restore +
# run from the bind-mounted source.
ENTRYPOINT ["sh", "-c", "\
  AUTH=src/HealthcareSupport.CaseEvaluation.AuthServer; \
  if [ ! -d \"$AUTH/wwwroot/libs\" ]; then (cd \"$AUTH\" && abp install-libs); fi; \
  find src -name 'project.assets.json' -delete 2>/dev/null; \
  find src -name '*.nuget.g.props' -delete 2>/dev/null; \
  find src -name '*.nuget.g.targets' -delete 2>/dev/null; \
  dotnet restore HealthcareSupport.CaseEvaluation.slnx >/tmp/restore.log 2>&1 || (cat /tmp/restore.log; exit 1); \
  exec dotnet run --no-launch-profile --no-restore --project $AUTH/HealthcareSupport.CaseEvaluation.AuthServer.csproj --urls http://+:8080"]

# ---- prod (DEFAULT): small aspnet runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS prod
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

# Do NOT add empty-stub source dirs: ABP's ReplaceEmbeddedByPhysical replaces
# (not merges) the embedded localization fileset when the physical path exists.
# The AuthServer module guards each replace with Directory.Exists + empty-check,
# so missing host source dirs fall back to the embedded fileset baked in the DLL.
ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.AuthServer.dll"]
