# Single multi-stage Dockerfile for the DbMigrator (one-shot console: applies EF
# migrations + runs seed contributors, then exits).
#   prod (DEFAULT): publish -> aspnet runtime image. CI / deploy.
#   dev:            SDK image; source bind-mounted at runtime so new migrations /
#                   seeds run from source. Re-run with
#                   `docker compose up db-migrator` (it exits on completion).

# ---- build (sdk): restore + publish; feeds prod ----
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
RUN dotnet publish src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj \
    -c Release -o /app/publish --no-restore

# ---- dev (sdk): bind-mounted source; restore + run on start ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dev
ARG ABP_NUGET_API_KEY
WORKDIR /app

# Redirect ALL build output (obj/bin) to a container-local path via .NET's
# artifacts output (see api/authserver Dockerfiles). Keeps the bind-mounted
# host src tree free of Linux build artifacts.
ENV UseArtifactsOutput=true
ENV ArtifactsPath=/artifacts

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
RUN dotnet build src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj \
    -c Debug --no-restore

ENTRYPOINT ["sh", "-c", "\
  dotnet restore HealthcareSupport.CaseEvaluation.slnx >/tmp/restore.log 2>&1 || (cat /tmp/restore.log; exit 1); \
  exec dotnet run --no-launch-profile --no-restore --project src/HealthcareSupport.CaseEvaluation.DbMigrator/HealthcareSupport.CaseEvaluation.DbMigrator.csproj"]

# ---- prod (DEFAULT): small aspnet runtime image ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS prod
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HealthcareSupport.CaseEvaluation.DbMigrator.dll"]
